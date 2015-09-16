
#include <stdint.h>


typedef struct thread_t {
	uintptr_t stackpointer;		// holds the current stack pointer of the thread
	int wakeUp;					// timestamp when the thread should be resumed
	struct thread_t *previous;	// points to the previous running thread (only valid while the thread is active)
	struct thread_t *next;		// points to the next running thread (only valid while the thread is active)
} thread_t;



void threading_init(void);
void threading_thread_init(thread_t *thread, void(*threadEntry), void *context, uintptr_t stackpointer);
void threading_thread_resume(thread_t *thread);
void threading_thread_suspend(thread_t *thread);
void threading_return_control(void);
void threading_sleep(int delay);


