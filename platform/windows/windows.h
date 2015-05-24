/*
*
*
* created: 31.03.15
*
*/
#ifndef __WINDOWS_PLATFORM__
#define __WINDOWS_PLATFORM__


#include <windows.h>

void windows_init(void);
#define io_init() windows_init()

#define _delay_ms(ms)	Sleep(ms)

// can't control interrupts in windows
#define interrupts_on()
#define interrupts_off()

void __attribute__((__noreturn__)) __reset(int code); // defined in windows.c

extern CRITICAL_SECTION universalLock;

// Enters a section of mutual exclusion.
static inline void atomic_enter(void) {
	EnterCriticalSection(&universalLock);
}

// Releases the application's mutual exclusion.
static inline void atomic_exit(void) {
	LeaveCriticalSection(&universalLock);
}


// atomic() { statements; } executes a code block with interrupts temporarily disabled
#define		atomic()	for (bool CONCAT(__atomic_, __LINE__) = (atomic_enter(), 1); CONCAT(__atomic_, __LINE__); CONCAT(__atomic_, __LINE__) = (atomic_exit(), 0))



#include "timer.h"
#ifdef USING_NVM
#  include "nvm.h"
#endif


#endif // __WINDOWS_PLATFORM__
