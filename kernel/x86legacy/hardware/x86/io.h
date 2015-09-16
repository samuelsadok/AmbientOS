


#include <stdint.h>



#define CPUID_FLAG_APIC		(1 << 9)




static inline void cpuid(int code, uint32_t * a, uint32_t * b, uint32_t * c, uint32_t * d) {
	//__asm volatile("cpuid"
	//: "=a"(*a), "=b"(*b), "=c"(*c), "=d"(*d) : "a"(code) : "eax", "ebx", "ecx", "edx");
	__asm volatile("cpuid"
	: "=a"(*a), "=b"(*b), "=c"(*c), "=d"(*d) : "a"(code) : "memory");
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

static inline uint64_t read_msr(uint32_t msr_id) {
	uint64_t		msr_value;

	__asm volatile ("	rdmsr"
	: "=A" (msr_value)
		: "c" (msr_id)
		);

	return msr_value;
}

static inline void write_msr(uint32_t msr_id, uint64_t msr_value) {
	__asm volatile ("	wrmsr"
	:
	: "c" (msr_id), "A" (msr_value)
		);
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


static inline unsigned long write_cr0(unsigned long val) {
	__asm volatile("mov cr0, %0\n\t" : : "r" (val));
}

static inline unsigned long write_cr2(unsigned long val) {
	__asm volatile("mov cr2, %0\n\t" : : "r" (val));
}

static inline unsigned long write_cr3(unsigned long val) {
	__asm volatile("mov cr3, %0\n\t" : : "r" (val));
}

static inline unsigned long write_cr4(unsigned long val) {
	__asm volatile("mov cr4, %0\n\t" : : "r" (val));
}
