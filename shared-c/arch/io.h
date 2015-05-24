/*
*
* Includes all header files that are relevant for the architecture that is being compiled for.
*
* created: 08.02.15
*
*/

#ifndef __GLOBAL_IO_H__
#define __GLOBAL_IO_H__


#ifndef __SYSTEM_H__
#	error "include system.h instead of this file"
#endif


#if defined(WINNT) || defined(WIN32)

/*
* if we're in an OS, don't include an architecture layer - we won't have IO access anyway
*/

#elif defined(__i386__) || defined(__x86_64__) || defined(__amd64__) // x86 family
#	include <arch/x86/io.h>
#elif defined(__AVR_ARCH__) // AVR microcontrollers
#	include <arch/avr/avr_io.h>
#	define io_init() avr_init()
#elif defined(__XAP__) // CSR bluetooth chips
#	include <arch/csr/csr1010.h>
#	define io_init() csr1010_init()
#else
#	error "architecture not supported"
#endif


#endif // __GLOBAL_IO_H__
