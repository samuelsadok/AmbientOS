

#include <stdlib.h>
#include <hardware\teletype.h>
#include <hardware\x86\interrupt.h>
#include "threading.h"




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









thread_t systemThread = {
	.stackpointer = 0,
	.previous = &systemThread,
	.next = &systemThread,
};


thread_t *currentThread = &systemThread;


void threading_init(void) {
	//systemThread.next = &systemThread;
	//systemThread.previous = &systemThread;
}


void thread_switch_done(void);


// Initializes the thread structure. Interrupts must be ENABLED.
//	thread: pointer to the thread structure to initialize
//	threadEntry: address where the thread should start execution
//	stackpointer: address of the first byte after the stack
void threading_thread_init(thread_t *thread, void (* threadEntry), void *context, uintptr_t stackpointer) {
	__asm volatile (
		"mov edx, esp	\n" // temporarily switch to the new stack
		"mov esp, %1	\n"
		"push %4		\n"
		"push ss		\n" // imitate all pushes that wîll happen before a thread switch
		//"push esp		\n"
		"pushf			\n"
		"push cs		\n"
		"push %2		\n"
		"pusha			\n"
		"push %3		\n"
		"push ebp		\n" // function prologue of threading_switch()
		"mov %0, esp	\n"
		"mov esp, edx	\n"
		: "=a" (thread->stackpointer)
		: "a" (stackpointer), "b" (threadEntry), "r" (thread_switch_done), "c" (context)
		: "edx", "memory");
}


// Starts or resumes a thread on the local processor. Interrupts of the local processor must be disabled.
void threading_thread_resume(thread_t *thread) {
	// insert the thread into the scheduling circle directly after the current thread
	thread->next = currentThread->next;
	thread->previous = currentThread;
	currentThread->next = thread;
	thread->next->previous = thread;
}


//// Suspends a running thread. The thread can be resumed later. Interrupts of the local processor must be disabled.
//void threading_thread_suspend(thread_t *thread) {
//	// extract the thread from the scheduling circle
//	thread->previous->next = thread->next;
//	thread->next->previous = thread->previous;
//}


// Makes the current thread give up the rest of its current quantum and suspends the thread
void threading_return_control(void) {
	__asm volatile ("int 0xF1"); // potential issue: a EOI will be sent
}


extern int systemTicks;


// Puts the current thread to sleep for the specified number of system ticks
void threading_sleep(int delay) {
	currentThread->wakeUp = systemTicks + delay;
	threading_return_control();
}


// Switches to the next thread in the loop. Interrupts of the local processor must be disabled.
void threading_switch(void) {
	__asm volatile("mov %0, esp" : "=a" (currentThread->stackpointer));  // read stack pointer

	do {
		currentThread = currentThread->next;
	} while (currentThread->wakeUp >= systemTicks);

	__asm volatile("mov esp, %0" : : "a" (currentThread->stackpointer));  // write stack pointer
}




__asm(
".globl _scheduler_manual \n"
"_scheduler_manual:		 \n"

".globl _scheduler_isr \n"
"_scheduler_isr:		 \n"

"pusha			\n" // Pushes edi, esi, ebp, esp, ebx, edx, ecx, eax

"mov ax, 0x10	\n" // load the kernel data segment descriptor
"mov ds, ax		\n"
"mov es, ax		\n"
"mov fs, ax		\n"
"mov gs, ax		\n"

"call _threading_switch \n"	// invoke the actual handler
"_thread_switch_done: \n"	// needed to create a new thread

"popa		\n" // Pops edi, esi, ebp...

"mov eax, [_apic_base]\n"
"mov [eax + 0xB0], eax\n"	// a write to this register signals End-Of-Interrupt to the APIC
"iret		\n" // pops 5 things at once : CS, EIP, EFLAGS, SS, and ESP
);

