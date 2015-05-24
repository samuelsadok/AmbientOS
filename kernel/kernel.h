/*
* Contains vital system functions that are required when running on bare metal hardware.
*
* created: 08.02.15
*
*/

#ifndef __KERNEL_H__
#define __KERNEL_H__


typedef uintptr_t system_ticks_t;			// must not be larger than uintptr_t
extern volatile system_ticks_t systemTicks; // defined in the architecure specific timer header

extern volatile int didBugcheck;

extern volatile int shuttingDown;

#include "heap.h"
#include "memory.h"
#include "ntfs.h"
#include "threading.h"


#endif // __KERNEL_H__
