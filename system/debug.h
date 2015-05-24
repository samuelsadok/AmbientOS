
#ifndef __DEBUG_H__
#define __DEBUG_H__


// Triggers the BOCHS magic breakpoint
static inline void debug(uint64_t rax, uint64_t rbx) {
	__asm volatile ("xchg bx, bx" : : "a" (rax), "b" (rbx) : "memory");
}

void debug_report_features();


#endif
