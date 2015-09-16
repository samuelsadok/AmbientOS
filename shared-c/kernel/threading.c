

#include <system.h>
#include "threading.h"



/*
typedef struct list_element_t {
	struct list_element_t *next;		// only used if the element is free
	struct list_element_t *previous;	// only used if the element is free
	char content[];
} list_element_t;


typedef struct {
	int capacity;				// the current list capacity
	int elementSize;			// the size of one list element
	list_element_t dummy;		// used to find the fist free element
	char content[];
} list_t;


// Creates a new list of elements of the specified size
list_t * list_new(int elementSize, int initialCapacity) {
	list_t *list;// = (list_t *)malloc(sizeof(list_t)+initialCapacity * (sizeof(list_element_t)+elementSize));
	list->capacity = initialCapacity;
	list->elementSize = sizeof(list_element_t) + elementSize;
	(list->dummy).next = (list_element_t *)(list->content);
	// todo: link empty elements
}


// Returns the index of the first free element in the list
int list_new_element(list_t *list) {

}


// Returns a pointer to the data in the specified element.
void * list_element(list_t *list, int index) {
	return list->content + index * list->elementSize + sizeof(list_element_t);
}




//void * malloc(long size) {
//	return 0; // todo
//}

//void * free(void);





typedef struct {
	int owner;					// the process that owns this object
	int type;					// the type of the object
	void (*dispose_callback);	// a pointer to a function that should be called before the object is released (can be null)
	void *data;					// a pointer to the actual data
} object_t;

*/



#ifdef USING_THREADING



thread_t systemThread = {
	.state = THREAD_RUNNING,
	.previous = &systemThread,
	.next = &systemThread,
};

thread_t idleThread;


// Doubly linked list (circle) that contains all threads running threads.
// The head of the list is always the active thread.
volatile thread_t *currentThread = &systemThread;

// Singly linked list of all suspended threads that are scheduled to wake up at some time, sorted by wake up time.
volatile thread_t *scheduledThreads;


// code for the idle thread (requires no stack)
void idle_loop(void *param);
__asm (
".global idle_loop		\n"
"idle_loop:				\n"
"hlt					\n"
"jmp idle_loop			\n"
);


// Switches to the next thread in the loop. Interrupts of the local processor must be disabled.
void thread_switch(execution_context_t *context) {
	thread_t *oldThread = currentThread;
	oldThread->context = *context;
	currentThread = oldThread->next;


	if (oldThread == &idleThread) {
		// if we were idling but another thread was resumed, remove idle thread
		if (currentThread != &idleThread) {
			idleThread.previous->next = idleThread.next;
			idleThread.next->previous = idleThread.previous;
		}

	} else if (oldThread->state != THREAD_RUNNING) { // suspend old thread if necessary
		if (oldThread == currentThread) { // if this was the only thread, invoke idle thread
			currentThread = &idleThread;
			idleThread.previous = idleThread.next = &idleThread;
		} else {
			oldThread->previous->next = oldThread->next;
			oldThread->next->previous = oldThread->previous;
		}

		thread_t * volatile *nextNodePtr;

		switch (oldThread->state) {
			case THREAD_SUSPENDED:
				break;

			case THREAD_SCHEDULED:
				// insert current thread into list of scheduled threads (sorted by wake up time)
				nextNodePtr = &scheduledThreads;
				while (*nextNodePtr) {
					if ((*nextNodePtr)->suspendInfo > oldThread->suspendInfo)
						break;
					nextNodePtr = &((*nextNodePtr)->next);
				}
				oldThread->next = *nextNodePtr;
				*nextNodePtr = oldThread;
				break;

			default:
				bug_check(STATUS_NOT_IMPLEMENTED, oldThread->state);

		}
	}


	// resume scheduled threads
	while (scheduledThreads) {
		if (scheduledThreads->suspendInfo > systemTicks)
			break;
		thread_t *t = scheduledThreads;
		scheduledThreads = scheduledThreads->next; // remove from list before resuming
		thread_resume(t);
	}


	// restore context of new thread
	*context = currentThread->context;
}


// apic_init must be called prior to this functions
void threading_init(void) {
	thread_init(&idleThread, idle_loop, NULL, 0);
	apic_timer_config(thread_switch);
	apic_timer_start(20000, 6);
}


// Initializes a thread structure. The thread will be placed in suspended state.
//	thread: pointer to the thread structure to be initialized
//	threadEntry: address where the thread should start execution
//	context: this address will be passed as an argument to the thread entry function.
//	stackpointer: address of the first byte after the new threads stack
void thread_init(thread_t *thread, thread_start_t threadStart, void *param, uintptr_t stackpointer) {
	assert(thread);
	assert(threadStart);
	//assert(stackpointer); idleThread has no stack

	thread->context.ss = STACK_SEGMENT_SELECTOR;
	thread->context.rsp = stackpointer;
	thread->context.cs = CODE_SEGMENT_SELECTOR;
	thread->context.rip = (uint64_t)threadStart;
	thread->context.rdi = (uint64_t)param;
	thread->context.rflags = (1UL << 9); // enable interrupts in the new context
	thread->state = THREAD_SUSPENDED;
}


// Starts or resumes a thread on the local processor.
void thread_resume(thread_t *thread) {
	assert(thread);
	atomic() {
		// insert the thread into the scheduling circle directly after the current thread
		thread->previous = currentThread;
		thread->next = currentThread->next;
		currentThread->next->previous = thread;
		currentThread->next = thread;
		thread->state = THREAD_RUNNING;
	}
}


// Makes the current thread give up the rest of its current time slice
void thread_yield(void) {
	apic_timer_trigger();
}


// Suspends the calling thread using the specified trigger mode to wake the thread up.
void thread_suspend_ex(thread_state_t suspendMode, uintptr_t suspendInfo) {
	atomic() {
		currentThread->state = suspendMode;
		currentThread->suspendInfo = suspendInfo;
	}
	thread_yield();
}


// Suspends the calling thread. The thread can be resumed later by another thread.
void thread_suspend(void) {
	thread_suspend_ex(THREAD_SUSPENDED, 0);
}


// Puts the calling thread to sleep for the specified number of system ticks.
void thread_sleep(system_ticks_t delay) {
	thread_suspend_ex(THREAD_SCHEDULED, systemTicks + delay);
}

#endif // USING_THREADING
