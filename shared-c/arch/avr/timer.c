/*
*
* Implements the support functions for the time functions in system/time.h for AVR MCUs
* This implementation uses the built in RTC. It works as long as the overflow interrupt
* can be serviced every now and then (at least once per minute).
*
* created: 10.03.15
*
*/

#include <system.h>
#include <rtc.h>

volatile bool armed = 0;

void timer_update(void); // defined in system/time.c

void tick_handler(ticks_t t) {
	if (armed) {
		armed = 0;
		timer_update();
	}
}

void timer_init(void) {
	rtc_init();
	rtc_set_callback(tick_handler);
	rtc_set_time(0);
}

void timer_set_alarm(ticks_t interval) {
	atomic() {
		rtc_set_alarm(system_ticks() + max(2, interval)); // set the alarm at least 2 ticks in the future (otherwise we might miss it)
		armed = 1;
	}
}

ticks_t system_ticks(void) {
	return rtc_get_time();
}
