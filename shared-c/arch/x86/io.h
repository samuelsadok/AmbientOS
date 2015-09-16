
#ifndef __x86_IO_H__
#define __x86_IO_H__

#if !defined(__x86_64)
#	error "there is currently no support for the legacy x86 16-bit and 32-bit architectures"
#endif



#define REGISTER_INIT0(func)			\
__attribute__((__section__(".init0")))	\
void(* func ## Ptr)(void) = func

#define REGISTER_INIT1(func)			\
__attribute__((__section__(".init1")))	\
void(* func ## Ptr)(void) = func

#define REGISTER_INIT2(func)			\
__attribute__((__section__(".init2")))	\
void(* func ## Ptr)(void) = func

#define REGISTER_INIT3(func)			\
__attribute__((__section__(".init3")))	\
void(* func ## Ptr)(void) = func

#define REGISTER_INIT4(func)			\
__attribute__((__section__(".init4")))	\
void(* func ## Ptr)(void) = func

#define REGISTER_INIT5(func)			\
__attribute__((__section__(".init5")))	\
void(* func ## Ptr)(void) = func


/*
*
* init0: nothing is available (only the stack is initialized)
* init1: architecture specific features available
* init2: integrated ports are initialized
* init3: stdout and stderr available
* init4 (global constructors): stdlib available
* init5: all kernel functions available
*
*/



#define CPUID_FLAG_APIC		(1 << 9)


// The segments are set up by the bootloader
#define CODE_SEGMENT_SELECTOR	(0x28)
#define DATA_SEGMENT_SELECTOR	(0x30)
#define TASK_SEGMENT_SELECTOR	(0x38)
#define STACK_SEGMENT_SELECTOR	(DATA_SEGMENT_SELECTOR)



static inline void cpu_halt(void) {
	__asm volatile("hlt" : : : "memory");
}

static inline void cpuid(int code, uint32_t *a, uint32_t *b, uint32_t *c, uint32_t *d) {
	__asm volatile("cpuid"
	: "=a"(*a), "=b"(*b), "=c"(*c), "=d"(*d) : "a"(code), "c"(0) : "memory");
}

static inline int cpuid_test(int code, uint32_t maskA, uint32_t maskB, uint32_t maskC, uint32_t maskD) {
	uint32_t a, b, c, d;
	cpuid(code & 0x80000000, &a, &b, &c, &d); // check highest available function
	if (a < code) return 0;
	cpuid(code, &a, &b, &c, &d);
	return ((a & maskA) == maskA) && ((b & maskB) == maskB) && ((c & maskC) == maskC) && ((d & maskD) == maskD);
}


static inline unsigned long get_flags() {
	unsigned long flags;
	__asm volatile("pushf \n"
	"pop %0"
	: "=g"(flags) : : "cc" );
	return flags;
}


static inline void out(unsigned short port, unsigned char val)
{
	__asm volatile("outb %1, %0"
	: : "a"(val), "Nd"(port));
}

static inline unsigned char in(unsigned short port)
{
	unsigned char ret;
	__asm volatile("inb %0, %1"
	: "=a"(ret) : "Nd"(port));
	return ret;
}

static inline void io_wait(void) {
	// port 0x80 is used for 'checkpoints' during POST.
	// The Linux kernel seems to think it is free for use...
	out(0x80, 0);
}

static inline uint64_t read_msr(uint32_t msrId) {
	uint32_t msrLo, msrHi;

	__asm volatile ("	rdmsr"
	: "=a" (msrLo), "=d" (msrHi) : "c" (msrId)
		);

	return ((uint64_t)msrHi << 32) | (uint64_t)msrLo;
}

static inline void write_msr(uint32_t msrId, uint64_t msrValue) {
	__asm volatile ("wrmsr"
	: : "c" (msrId), "a" (msrValue & 0xFFFFFFFF), "d" (msrValue >> 32)
		);
}

typedef volatile struct __attribute__((aligned(4))) __attribute__((packed)) {
	uint16_t limit;
	void *address;
} idtr_t;

static inline idtr_t get_idtr(void) {
	idtr_t idtr;
	__asm volatile ("sidt [rax]"
	: : "a" (&idtr) : "memory");
	return idtr;
}

static inline void set_idtr(idtr_t idtr) {
	__asm volatile ("lidt [rax]"
	: : "a" (&idtr) : "memory");
}


static inline void set_tr(short selector) {
	__asm volatile ("ltr ax"
	: : "a" (selector) : "memory");
}


static inline unsigned long read_cr0(void) {
	unsigned long val;
	__asm volatile("mov %0, cr0\n\t" : "=r" (val));
	return val;
}

static inline unsigned long read_cr2(void) {
	unsigned long val;
	__asm volatile("mov %0, cr2\n\t" : "=r" (val));
	return val;
}

static inline unsigned long read_cr3(void) {
	unsigned long val;
	__asm volatile("mov %0, cr3\n\t" : "=r" (val));
	return val;
}

static inline unsigned long read_cr4(void) {
	unsigned long val;
	__asm volatile("mov %0, cr4\n\t" : "=r" (val));
	return val;
}


static inline void write_cr0(unsigned long val) {
	__asm volatile("mov cr0, %0\n\t" : : "r" (val));
}

static inline void write_cr2(unsigned long val) {
	__asm volatile("mov cr2, %0\n\t" : : "r" (val));
}

static inline void write_cr3(unsigned long val) {
	__asm volatile("mov cr3, %0\n\t" : : "r" (val));
}

static inline void write_cr4(unsigned long val) {
	__asm volatile("mov cr4, %0\n\t" : : "r" (val));
}




typedef struct  __attribute__((packed)) {
	// pushed by the ISR preamble:
	uint64_t rax, rbx, rcx, rdx, r8, r9, r10, r11, r12, r13, r14, r15, rbp, rdi, rsi;
	// pushed by the processor on any interrupt:
	uint64_t rip, cs, rflags, rsp, ss;
} execution_context_t;






extern int cpuFeatureLevel;


#define CPUACCELDECL(returnType, funcName, funcParams)				\
extern returnType(*funcName)funcParams


#define CPULEVEL0

#define CPULEVEL1_MASK_EDX	((1 << 25) | (1 << 26))
#define CPULEVEL1_MASK_ECX	((1 << 0) | (1 << 9) | (1 << 19) | (1 << 20))
#define CPULEVEL1 CPULEVEL0											\
__attribute__((__target__("sse")))									\
__attribute__((__target__("sse2")))									\
__attribute__((__target__("sse3")))									\
__attribute__((__target__("ssse3")))								\
__attribute__((__target__("sse4")))									\
__attribute__((__target__("sse4.1")))								\
__attribute__((__target__("sse4.2")))

#define CPULEVEL2_MASK_EDX	(CPULEVEL1_MASK_EDX)
#define CPULEVEL2_MASK_ECX	(CPULEVEL1_MASK_ECX | (1 << 29) | (1 << 28) | (1 << 23))
#define CPULEVEL2_MASK_EBX	((1 << 5) | (1 << 3) | (1 << 8))				// on CPUID page 7
#define CPULEVEL2 CPULEVEL1											\
__attribute__((__target__("f16c")))									\
__attribute__((__target__("avx")))									\
__attribute__((__target__("avx2")))									\
__attribute__((__target__("abm")))									\
__attribute__((__target__("popcnt")))								\
/*__attribute__((__target__("bmi1")))*/								\
__attribute__((__target__("bmi2")))


#define CPUACCELFUNC(returnType, funcName, funcParams, funcBody)	\
returnType															\
CPULEVEL0															\
funcName ## _level0 funcParams										\
funcBody															\
returnType															\
CPULEVEL1															\
funcName ## _level1 funcParams										\
funcBody															\
returnType															\
CPULEVEL2															\
funcName ## _level2 funcParams										\
funcBody															\
returnType(*funcName)funcParams;									\
void __attribute__((constructor)) funcName ## _init (void) {		\
	funcName = ((cpuFeatureLevel > 0) ? ((cpuFeatureLevel > 1) ? funcName ## _level2 : funcName ## _level1) : funcName ## _level0); \
}





#include "apic.h"
#include "interrupts.h"
#include "mmu.h"
#include "realmode.h"
//#include "vmx.h"



#endif // __x86_IO_H__
