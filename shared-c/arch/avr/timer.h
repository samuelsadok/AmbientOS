/*
*
*
* created: 10.03.15
*
*/
#ifndef __AVR_TIMER_H__
#define __AVR_TIMER_H__

typedef uint32_t ticks_t;
#define TICKS_BITS	32			// the MCU uses 16-bit RTC internally but the driver handles the expansion to 32-bit
#define TICKS_MAX	0xFFFFFFFF	// ~ 50 days

void timer_init(void);
void timer_set_alarm(ticks_t interval);
ticks_t system_ticks(void);

#endif // __AVR_TIMER_H__
