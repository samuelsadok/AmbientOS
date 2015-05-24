

#include <stdint.h>

enum {
	INTERRUPT_NUMBER_DIV_BY_0 = 0x00,	// division by zero
	INTERRUPT_NUMBER_DEBUG = 0x01,	// debug exception
	INTERRUPT_NUMBER_NMI = 0x02,	// non-maskable interrupt
	INTERRUPT_NUMBER_BREAK = 0x03,	// breakpoint exception
	INTERRUPT_NUMBER_INTO = 0x04,	// overflow during INTO instruction ??
	INTERRUPT_NUMBER_BOUNDS = 0x05,	// out of bounds exception
	INTERRUPT_NUMBER_OPCODE = 0x06,	// invalid opcode
	INTERRUPT_NUMBER_NOCOPROC = 0x07,	// no coprocessor
	INTERRUPT_NUMBER_DOUBLE = 0x08,	// double fault
	INTERRUPT_NUMBER_COSEGMENT = 0x09,	// coprocessor segment overrun
	INTERRUPT_NUMBER_BADTSS = 0x0A,	// bad TSS
	INTERRUPT_NUMBER_SEGMENT = 0x0B,	// segment not present
	INTERRUPT_NUMBER_STACK = 0x0C,	// stack fault
	INTERRUPT_NUMBER_GP = 0x0D,	// general protection fault
	INTERRUPT_NUMBER_PAGEFAULT = 0x0E,	// page fault
	INTERRUPT_NUMBER_UNKNOWN = 0x0F,	// unknown interrupt
	INTERRUPT_NUMBER_COPROC = 0x10,	// coprocessor fault
	INTERRUPT_NUMBER_ALIGN = 0x11,	// alignment check exception
	INTERRUPT_NUMBER_MACHINE = 0x12	// machine check exception
	//	0x13 - 0x1F reserved
};


enum {
	INTERRUPT_TYPE_TASK = 0x85,	// something fancy involving TSS entries in the GDT
	INTERRUPT_TYPE_INT = 0x8E,	// interrupt gate (interrupts are disabled)
	INTERRUPT_TYPE_TRAP = 0x8F	// trap gate (interrupts remain enabled)
};



#define interrupts_off()	__asm("cli" : : : "memory")
#define interrupts_on()		__asm("sti" : : : "memory")



typedef struct __attribute__((aligned(8))) {
	uint16_t	base_low;	// lower 16 bits of jump address
	uint16_t	selector;	// selector for the corresponding code segment
	uint8_t		reserved;	// must be 0
	uint8_t		flags;		// properties of the interrupt gate
	uint16_t	base_high;	// upper 16 bits of jump address
} interrupt_idt_entry_t;


typedef struct {
	uint32_t ds;                  // Data segment selector
	uint32_t edi, esi, ebp, esp, ebx, edx, ecx, eax; // Pushed by pusha.
	uint32_t int_no, err_code;    // Interrupt number and error code (if applicable)
	uint32_t eip, cs, eflags, useresp, ss; // Pushed by the processor automatically.
} saved_registers_t;

extern interrupt_idt_entry_t	interrupt_descriptor_table[256];

void interrupt_set_isr(int number, int callback_ptr, uint8_t flags);
void interrupt_init();

