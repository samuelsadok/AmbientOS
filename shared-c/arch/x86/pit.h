
#ifndef __PIT_H__
#define __PIT_H__

typedef uint32_t ticks_t;
#define TICKS_BITS	32
#define TICKS_MAX	0xFFFFFFFF	// ~ 50 days

void timer_init(void);
void timer_set_alarm(ticks_t interval);
ticks_t system_ticks(void);

#endif
