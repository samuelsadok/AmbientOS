/*
*
* This file contains essential code for initializing the architecture and
* standard library functions, executing the global constructors and finally
* calling main.
*
* created: 07.02.15
*
*/


#if defined(MAKE_CRTI) // make crti.o

__asm (
".section .init				\n"
".global __init				\n"
".type __init, @function	\n"
"__init :					\n"
"push	rbp					\n"
"mov	rbp, rsp			\n"
// .init section of crtbegin.o is placed here


".section .bootref		\n"
".globl __bootref_list	\n"
"__bootref_list:		\n"
// bootloader references are placed here

".section .init0	\n"
".globl __init0		\n"
"__init0:			\n"
// init0 sections of all source code files are placed here (same for init1...)

".section .init1	\n"
".globl __init1		\n"
"__init1:			\n"

".section .init2	\n"
".globl __init2		\n"
"__init2:			\n"

".section .init3	\n"
".globl __init3		\n"
"__init3:			\n"

".section .init4	\n"
".globl __init4		\n"
"__init4:			\n"

".section .init5	\n"
".globl __init5		\n"
"__init5:			\n"


".section .fini				\n"
".global __fini				\n"
".type __fini, @function	\n"
"__fini :					\n"
"push	rbp					\n"
"mov	rbp, rsp			\n"
// .fini section of crtbegin.o is placed here
);


#elif defined(MAKE_CRTN) // make crtn.o


__asm (
".section .init		\n"
// .init section of crtend.o is placed here
"pop rbp			\n"
"ret				\n"


".section .bootref	\n"
".quad 0			\n"

".section .init0	\n"
".quad 0			\n"

".section .init1	\n"
".quad 0			\n"

".section .init2	\n"
".quad 0			\n"

".section .init3	\n"
".quad 0			\n"

".section .init4	\n"
".quad 0			\n"

".section .init5	\n"
".quad 0			\n"


".section .fini		\n"
// .fini section of crtend.o is placed here
"pop rbp			\n"
"ret				\n"
);


#else // make crt0.o


__asm(
".section .start		\n" // .start is placed at location 0 in the raw binary

".globl _start			\n"
"_start:				\n"
"	jmp	__start			\n"	// this instruction is where the bootloader jumps, it will be loaded at 0xFFFFFFFF80100000

".org 0x8				\n" // the two following fields are read by the bootloader to map and clear the .bss section
".quad __bss			\n" // virtual address of the .bss section
".quad __bss_end		\n" // virtual address of the end of the kernel

".section .text			\n"
);


#include <system.h>


void __init(void);
void __fini(void);
void main(void);

extern uintptr_t __bootref_list[]; // an array of bootloader references
extern void(*__init0[])(void); // an array of funtion pointers, terminated by NULL
extern void(*__init1[])(void);
extern void(*__init2[])(void);
extern void(*__init3[])(void);
extern void(*__init4[])(void);
extern void(*__init5[])(void);

int cpuFeatureLevel = 0;


void __start(uintptr_t bootloaderBase) {
	// invoke early init functions
	for (int i = 0; __init0[i]; i++)
		__init0[i]();

	// add bootloader offset to all bootloader references
	for (int i = 0; __bootref_list[i]; i++)
		__bootref_list[i] += bootloaderBase;

	// enable SSE
	write_cr0((read_cr0() & ~(1 << 2)) | (1 << 1));
	write_cr4(read_cr4() | (1 << 9) | (1 << 10));

	// init x87 floating-point unit
	__asm volatile("finit" : : : "memory");

	// determine level of CPU features (required by accelerated functions)
	if (cpuid_test(1, 0, 0, CPULEVEL1_MASK_ECX, CPULEVEL1_MASK_EDX)) {
		if (cpuid_test(1, 0, 0, CPULEVEL2_MASK_ECX, CPULEVEL2_MASK_EDX) && cpuid_test(7, 0, CPULEVEL2_MASK_EBX, 0, 0)) {
			cpuFeatureLevel = 2;
		} else {
			cpuFeatureLevel = 1;
		}
	} else {
		cpuFeatureLevel = 0;
	}


	// initialize built in architecture features
	for (int i = 0; __init1[i]; i++)
		__init1[i]();

	// initialize standard output
	for (int i = 0; __init2[i]; i++)
		__init2[i]();

	// initialize platform hardware
	for (int i = 0; __init3[i]; i++)
		__init3[i]();

	// initialize standard library
	for (int i = 0; __init4[i]; i++)
		__init4[i]();

	// init architecture specific features
	interrupt_init(tssDescriptor);
	apic_init();

	// Init C runtime (global constructors, ...)
	// Global constructors are allowed to use any standard library functions.
	__init();

	// initialize advanced hardware and mount services
	for (int i = 0; __init5[i]; i++)
		__init5[i]();

	main();

	// tear down C runtime (global destructors, ...)
	__fini();
}

#endif
