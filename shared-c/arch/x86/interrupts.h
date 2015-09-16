
#ifndef __INTERRUPT_H__
#define __INTERRUPT_H__


enum {
	INTERRUPT_NUMBER_DIV_BY_0 = 0x00,	// division by zero
	INTERRUPT_NUMBER_DEBUG = 0x01,	// debug exception
	INTERRUPT_NUMBER_NMI = 0x02,	// non-maskable interrupt
	INTERRUPT_NUMBER_BREAK = 0x03,	// breakpoint exception
	INTERRUPT_NUMBER_INTO = 0x04,	// overflow during INTO instruction ??
	INTERRUPT_NUMBER_BOUNDS = 0x05,	// out of bounds exception (bound instruction)
	INTERRUPT_NUMBER_OPCODE = 0x06,	// invalid opcode
	INTERRUPT_NUMBER_NOCOPROC = 0x07,	// no coprocessor
	INTERRUPT_NUMBER_DOUBLE = 0x08,	// double fault (interrupted program not recoverable!)
	INTERRUPT_NUMBER_COSEGMENT = 0x09,	// coprocessor segment overrun
	INTERRUPT_NUMBER_BADTSS = 0x0A,	// bad TSS
	INTERRUPT_NUMBER_SEGMENT = 0x0B,	// segment not present
	INTERRUPT_NUMBER_STACK = 0x0C,	// stack fault
	INTERRUPT_NUMBER_GP = 0x0D,	// general protection fault
	INTERRUPT_NUMBER_PAGEFAULT = 0x0E,	// page fault
	INTERRUPT_NUMBER_UNKNOWN = 0x0F,	// unknown interrupt
	INTERRUPT_NUMBER_COPROC = 0x10,	// coprocessor fault
	INTERRUPT_NUMBER_ALIGN = 0x11,	// alignment check exception
	INTERRUPT_NUMBER_MACHINE = 0x12,	// machine check exception (interrupted program not recoverable!)
	INTERRUPT_NUMBER_SIMD = 0x13,	// SIMD floating point exception
	//	0x14 - 0x1D reserved
	INTERRUPT_NUMBER_SECURITY = 0x1E	// security exception
	//	0x1F reserved
};


enum {
	INTERRUPT_TYPE_TASK = 0x85,	// something fancy involving TSS entries in the GDT (unsupported in long mode)
	INTERRUPT_TYPE_INT = 0x8E,	// interrupt gate (interrupts are disabled)
	INTERRUPT_TYPE_TRAP = 0x8F	// trap gate (interrupts remain enabled)
};


#define STACK_NUM_CURRENT		(0)	// no stack switch
#define STACK_NUM_KERNEL		(1)	// root thread stack
#define STACK_NUM_INT			(2)	// stack used during interrupts (empty otherwise)
#define STACK_NUM_FALLBACK		(3)	// stack used for non-recoverable faults


#define interrupts_off()	__asm("cli" : : : "memory", "cc")
#define interrupts_on()		__asm("sti" : : : "memory", "cc")

// Switches interrupts off. Returns 1 if interrupts were on.
static inline int atomic_enter(void) {
	int intFlag = (get_flags() >> 9) & 1;
	interrupts_off();
	return intFlag;
}

// Switches interrupts off.
static inline void atomic_exit(int intFlag) {
	if (intFlag)
		interrupts_on();
}

// atomic() { statements; } executes a code block with interrupts temporarily disabled
#define		atomic()	for (int __atomic_ ## __LINE__ = atomic_enter() + 1; (__atomic_ ## __LINE__); __atomic_ ## __LINE__ = (atomic_exit(__atomic_ ## __LINE__ - 1), 0))





//	intNumber: The interrupt that led to the execution of the handler
//	errCode: An error code specific to some interrupts (undefined for interrupts that don't define an error code)
//	context: A pointer to a structure that describes the interrupted execution context.
// An interrupt handler must not enable interrupts or generate exceptions or software interrupts.
// Usage of SIMD and segment registers are not allowed.
// Stack usage must not exceed 4kB.
// The interrupted execution context. This structure may be swapped or edited to achieve a context switch.
// SIMD registers and segment registers are not saved in the context and must be handled separately.
typedef void(*interrupt_handler_t)(uint64_t intNumber, uint64_t errCode, execution_context_t *context);


void interrupt_init(void *tssDescriptor);
void interrupt_register(int number, interrupt_handler_t handler);



#endif // __INTERRUPT_H__
