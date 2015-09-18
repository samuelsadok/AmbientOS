/*
*
* Implements the support functions for the time functions in system/time.h for x86 systems.
* This implementation uses the built in PIT. It works as long as the overflow interrupt
* can be serviced every now and then (at least once per minute).
*
* created: 17.09.15
*
*/

#include <system.h>

#define PIT_CH0_PORT	0x40
#define PIT_CMD_PORT	0x43
#define PIT_RELOAD_VALUE 	64432	// counter frequency is 1193182Hz, so this interval is pretty close to 54ms

volatile uint32_t internal_ticks;
volatile char triggered = 0;
volatile char fetching = 0;
lock_t pit_lock = CREATE_LOCK;

void timer_update(void); // defined in system/time.c

// this should become an IRQ0 handler
void tick_handler(ticks_t t) {
	if (fetching_ticks)
		triggered = 1;
	else
		internal_ticks += 54;
}

void timer_init(void) {
	out(PIT_CMD_PORT,
		(0 << 6) |	// channel 0
		(3 << 4) |	// hi-lo access mode
		(2 << 1) |	// rate generator mode
		(0 << 0)	// 16-bit digit
		);

	uint16_t interval = PIT_RELOAD_VALUE;
	out(PIT_CH0_PORT, interval & 0xFF);
	out(PIT_CH0_PORT, (interval >> 8) & 0xFF);

	// todo: set up IRQ0 handler
}

// not supported
void timer_set_alarm(ticks_t interval) {
	__debug();
	kernel_panic();
}

ticks_t system_ticks(void) {
	ticks_t val;
	lock(pit_lock) {
		fetching = 1;
		out(PIT_CMD_PORT, 0); // latch command for channel 0
		uint16_t count = in(PIT_CH0_PORT);
		count |= in(PIT_CH0_PORT) << 8;
		val = internal_ticks + ((PIT_RELOAD_VALUE - count) / 54);
		if (triggered)
			internal_ticks += 54;
		// if the timer triggers at this point, we lose 54 ticks
		fetching = 0;
		triggered = 0;
	}
	return val;
}
