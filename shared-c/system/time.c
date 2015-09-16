/*
*
* Provides platform-independent functions related to timers and clock.
*
*	- Timers
*		slightly inaccurate (±3%)
*		resolution: 1 millisecond
*		maximum interval of ~50 days
*		meant for timing real-time events
*
*	- Real Time Clock
*		accurate measure of the current time and date
*		resolution: 1 second
*		may change unpredictably at any time
*
* Timers are currently not thread safe (the only guaranteed safety is that the timer_update does not interfere with timer_start and timer_stop).
*
* Architecture-specific support required:
*	timer_register_callback:
*		shall register a callback to be called exactly once after the specified interval (multiple calls override previous callbacks)
*	system_ticks:
*		shall return a 16-bit number that increments by 1 every ~1ms
*
* created: 09.03.15
*
*/

#include <system.h>


bool timerLock = 0;					// todo: make timer lists lock-free
timer_t *timerList = NULL;			// linked list of active timers, sorted by their time of expiry


// Triggers all timers in a linked list.
void timer_trigger(timer_t *timer) {
	while (timer) {
		timer->state.running = 0;
		timer->state.hasExpired = 1;
		if (timer->callback)
			timer->callback(timer->context);
		timer = timer->next;
	}
}


// If an overflow happens between two calls to this function, it returns a list of all timers that should have
// expired before the overflow and removes them from the active timer list
timer_t *timer_check_overflow(ticks_t ticks) {
	static ticks_t oldTicks = 0;

	timer_t *expiredTimers = NULL;
	if (ticks < oldTicks) {
		// an overflow has occurred

#if TICKS_BITS < 32
		timer_t *timer;
		while (!(timerList->expiry >> TICKS_BITS)) {
			timerList = (timer = timerList)->next;
			timer->next = expiredTimers;
			expiredTimers = timer;
		}

		for (timer = timerList; timer; timer = timer->next)
			timer->expiry -= 1UL << TICKS_BITS;
#endif
	}
	oldTicks = ticks;
	return expiredTimers;
}


// Triggers all timers that have expired by now.
// This function is invoked by the platform specific timer driver after the interval set by timer_set_alarm.
void timer_update(void) {
	timer_t *expiredTimers = NULL;

	atomic() {
		// if some function holds the timer lock, it is guaranteed to call this function afterwards
		if (__sync_bool_compare_and_swap(&timerLock, 0, 1)) {
			// take all timers that should have expired
			ticks_t currentTime = system_ticks();
			expiredTimers = timer_check_overflow(currentTime);
			timer_t *timer;
			while (timerList ? timerList->expiry <= (uint32_t)currentTime : 0) {
				timerList = (timer = timerList)->next;
				timer->next = expiredTimers;
				expiredTimers = timer;
			}

			// register handler for the next timer that fires (if any)
			if (timerList)
				timer_set_alarm(min(timerList->expiry - (uint32_t)currentTime, TICKS_MAX)); // we may theoretically be 1 tick late

			// release timer lock
			timerLock = 0;
		}
	}

	timer_trigger(expiredTimers);
}


// Starts the specified timer.
// The pointer passed to this function must point to a valid timer struct
// until the timer expires or is stopped.
// Starting a timer that has already been started or expired has no effect.
void timer_start(timer_t *timer) {
	timer_t *expiredTimers = NULL;

	// acquire timer lock
	if (!__sync_bool_compare_and_swap(&timerLock, 0, 1))
		bug_check(STATUS_SYNC_ERROR, timerLock);

	if (!timer->state.running && !timer->state.hasExpired) {
		ticks_t currentTime = system_ticks(); // need to fetch ticks atomically
		expiredTimers = timer_check_overflow(currentTime);
		uint32_t deadline = (timer->expiry += (uint32_t)currentTime);
		timer->state.running = 1;

		// find the field that should point to the new timer
		timer_t **timerPtr = &timerList;
		while (*timerPtr ? ((*timerPtr)->expiry <= deadline) : 0)
			timerPtr = &((*timerPtr)->next);

		// insert new timer
		timer->next = *timerPtr;
		*timerPtr = timer;
	}

	// release timer lock
	timerLock = 0;
	timer_update();

	timer_trigger(expiredTimers);
}


// Pauses the specified timer. The timer can later be resumed using timer_start.
// Stopping a timer that has already been stopped or expired has no effect.
void timer_stop(timer_t *timer) {
	ticks_t currentTime = system_ticks();

	// acquire timer lock
	if (!__sync_bool_compare_and_swap(&timerLock, 0, 1))
		bug_check(STATUS_SYNC_ERROR, timerLock);

	// find the field that points to the timer
	timer_t **timerPtr = &timerList;
	while (*timerPtr && *timerPtr != timer) // traverse normal list
		timerPtr = &((*timerPtr)->next);

	// if we found the timer, unlink
	if (*timerPtr == timer)
		*timerPtr = (*timerPtr)->next;

	// release timer lock
	timerLock = 0;
	timer_update();

	if (timer->state.running) {
		timer->state.running = 0;
		timer->expiry -= currentTime;
	}
}


// Returns true if the timer has expired.
// This works even if interrupts are disabled during the lifetime of the timer
// (in this case, the function must be called at an interval of at least TICKS_MAX / 2).
bool timer_has_expired(timer_t *timer) {
	timer_update();
	return timer->state.hasExpired;
}
