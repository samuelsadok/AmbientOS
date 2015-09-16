
#ifndef __THREADING_H__
#define __THREADING_H__

#ifdef USING_THREADING

typedef enum
{
	THREAD_RUNNING,
	THREAD_SUSPENDED,
	THREAD_SCHEDULED,	// resume the thread at a scheduled time (suspendInfo: wake up time in system ticks)
} thread_state_t;


typedef volatile struct thread_t {
	thread_state_t state;
	intptr_t suspendInfo;					// meaning depends on the thread state
	execution_context_t context;			// execution context
	volatile struct thread_t *previous;		// points to the previous running thread (only valid while the thread is active)
	volatile struct thread_t *next;			// points to the next running thread (only valid while the thread is active)
} thread_t;


typedef void(*thread_start_t)(void *param);

void threading_init(void);
void thread_init(thread_t *thread, thread_start_t threadStart, void *param, uintptr_t stackpointer);
void thread_resume(thread_t *thread);
void thread_yield(void);
void thread_suspend_ex(thread_state_t suspendMode, uintptr_t suspendInfo);
void thread_suspend(void);
void thread_sleep(system_ticks_t delay);

#endif // USING_THREADING

#endif // __THREADING_H__
