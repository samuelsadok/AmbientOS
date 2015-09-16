/*
*
*
* created: 01.04.15
*
*/
#ifndef __WINDOWS_TIMER_H__
#define __WINDOWS_TIMER_H__

typedef uint32_t ticks_t;
#define TICKS_BITS	21
#define TICKS_MAX	0x1FFFFF	// ~35min (32 bits shifted by 10 (�s -> ms) + 1 (the MSB should not be used in timer intervals))

void timer_init(void);
void timer_set_alarm(ticks_t interval);
ticks_t system_ticks(void);

#endif // __WINDOWS_TIMER_H__
