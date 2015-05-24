/*
*
*
* created: 01.04.15
*
*/

#include <system.h>
#include <sys/timeb.h>


UINT_PTR timer = 0;
volatile bool armed = 0;

void timer_update(void); // defined in system/time.c

VOID CALLBACK tick_handler(HWND hwnd, UINT uMsg, UINT_PTR idEvent, DWORD dwTime) {
	if (__sync_bool_compare_and_swap(&armed, 1, 0))
		timer_update();
}


void timer_set_alarm(ticks_t interval) {
	if (timer)
		KillTimer(NULL, timer);
	armed = 1;
	if (!(timer = SetTimer(NULL, timer, interval & TICKS_MAX, tick_handler)))
		bug_check(STATUS_ERROR, timer);
}


ticks_t system_ticks(void) {
	struct _timeb time;
	_ftime(&time);

	return (1000UL * time.time + time.millitm) & TICKS_MAX;
}
