/*
*
* Implements the support functions for the time functions in system/time.h for the CSR1010.
* This implementation is based on a single firmware timer (they are not used directly as they aren't
* flexible enough).
*
* created: 09.03.15
*
*/

#include <system.h>
#include <timer.h>


#define MAX_APP_TIMERS	(1) // one timer is used by the framework to handle all timers
uint16_t app_timers[SIZEOF_APP_TIMER * MAX_APP_TIMERS];
volatile timer_id timer;

volatile bool armed = 0;

void timer_update(void); // defined in system/time.c

void tick_handler(timer_id t) {
	if ((t == timer) && armed) {
		armed = 0;
		timer_update();
	}
}

void timer_init(void) {
	TimerInit(MAX_APP_TIMERS, (void *)app_timers);
}

void timer_set_alarm(ticks_t interval) {
	TimerDelete(timer);
	armed = 1;
	if ((timer = TimerCreate((interval & TICKS_MAX) << 10, TRUE, tick_handler)) == TIMER_INVALID)
		bug_check(STATUS_ERROR, timer);
}

ticks_t system_ticks(void) {
	return (TimeGet32() >> 10) & TICKS_MAX;
}
