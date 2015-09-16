/*
*
*
* created: 09.03.15
*
*/
#ifndef __TIME_H__
#define __TIME_H__


typedef struct timer_t
{
	uint32_t expiry;			// when stopped: the remaining time until expiration, when running: the absolute time of expiration (in system tick units), when expired: undefined
	void(*callback)(void *);	// the expiration handler (can be NULL)
	void *context;				// a context for the expiration callback
	struct
	{
		unsigned int running : 1;
		unsigned int hasExpired : 1;
	} state;
	struct timer_t *next; // pointer used internally to organize the timers
} timer_t;


// Creates a new timer with the specified interval.
#define CREATE_TIMER(_interval, _callback, _context) {	\
	.expiry = (_interval),								\
	.callback = (_callback),							\
	.context = (_context),								\
	.state = {											\
		.running = 0,									\
		.hasExpired = 0									\
	},													\
	.next = NULL										\
}


void timer_start(timer_t *timer);
void timer_stop(timer_t *timer);
bool timer_has_expired(timer_t *timer);


#endif // __TIME_H__
