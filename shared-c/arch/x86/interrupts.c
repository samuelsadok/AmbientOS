
#include <system.h>
#include "interrupts.h"




typedef struct __attribute__((aligned(16))) __attribute__((packed)) {
	uint16_t	base_low;		// lower 16 bits of isr address
	uint16_t	selector;		// selector for the corresponding code segment
	uint8_t		stackNumber;	// a number between 0 and 7 to select a stack pointer from the TSS
	uint8_t		flags;			// properties of the interrupt gate
	uint16_t	base_middle;	// middle 16 bits of isr address
	uint32_t	base_high;		// upper 32 bits of isr address
	uint32_t	reserved;		// must be 0
} interrupt_descriptor_t;


// represents the TSS entry in the GDT
typedef struct __attribute__((packed)) {
	uint16_t limit;
	uint16_t base1;
	uint8_t base2;
	uint16_t attributes;
	uint8_t base3;
	uint32_t base4;
	uint32_t reserved;
} tss_descriptor_t;


struct __attribute__((packed)) __attribute__((aligned(4))) {
	uint32_t reserved1;
	uint64_t rsp[3]; // stack pointer for ring 0 - 2
	uint64_t ist[8]; // several stack pointers to be used for interrupts (entry 0 is reserved)
	uint64_t reserved2;
	uint16_t reserved3;
	uint16_t ioBitmapOffset; // offset of the IO permission bitmap relative to the TSS address (not used)
} tss;



interrupt_descriptor_t interrupt_descriptor_table[256];

// the kernel needs several stacks for different purposes
char *kernel_stack = (void *)0xFFFFFFFFFFFFF000UL; // set up by bootloader
char interrupt_stack[PAGE_SIZE];
char fallback_stack[PAGE_SIZE];





extern char isr_prototype;
extern char isr_prototype_end;
extern char isr_push_instr;
extern char isr_after_number_field;
extern char isr_after_address_field;

__asm (
".global isr_push_instr				\n"
".global isr_after_number_field		\n"
".global isr_after_address_field	\n"
".global isr_prototype				\n"
".global isr_prototype_end			\n"

"isr_prototype:						\n"

// save rdi, rsi and load them with interrupt number and error code

"isr_push_instr:					\n"
"push	rsi							\n" // replaced with NOP for interrupts that push an error code
"xchg	rsi, [rsp]					\n"
"push	rdi							\n"
"mov	rdi, 0x0123456789ABCDEF		\n" // set to the according 64-bit interrupt number
"isr_after_number_field:			\n"


// save the rest of the registers
"push	rbp							\n"
"push	r15							\n"
"push	r14							\n"
"push	r13							\n"
"push	r12							\n"
"push	r11							\n"
"push	r10							\n"
"push	r9							\n"
"push	r8							\n"
"push	rdx							\n"
"push	rcx							\n"
"push	rbx							\n"
"push	rax							\n"

// invoke interrupt handler
"mov	rdx, rsp					\n" // rsp points to the saved registers
"mov	rax, 0x0123456789ABCDEF		\n" // set to the according 64-bit handler address
"isr_after_address_field:			\n"
"call	rax							\n"

// restore registers
"pop	rax							\n"
"pop	rbx							\n"
"pop	rcx							\n"
"pop	rdx							\n"
"pop	r8							\n"
"pop	r9							\n"
"pop	r10							\n"
"pop	r11							\n"
"pop	r12							\n"
"pop	r13							\n"
"pop	r14							\n"
"pop	r15							\n"
"pop	rbp							\n"
"pop	rdi							\n"
"pop	rsi							\n"
"iretq								\n"

"isr_prototype_end:					\n"
);







// Registers an interrupt handler. For each interrupt number only one handler can be registered.
// malloc must be initialized fist.
//	number: the interrupt number (between 0 and 255)
//	handler: the address of the interrupt handler
//	stackNumber: the stack to be used for this interrupt (between 0 and 7)
void interrupt_register_ex(int number, int hasErrorCode, interrupt_handler_t handler, int stackNumber) {
	size_t isrPrototypeLength = (uintptr_t)&isr_prototype_end - (uintptr_t)&isr_prototype;
	uintptr_t isrPushInstrOffset = (uintptr_t)&isr_push_instr - (uintptr_t)&isr_prototype;
	uintptr_t isrNumberFieldOffset = (uintptr_t)&isr_after_number_field - 8UL - (uintptr_t)&isr_prototype;
	uintptr_t isrAddressFieldOffset = (uintptr_t)&isr_after_address_field - 8UL - (uintptr_t)&isr_prototype;

	// copy prototype code to new location
	uintptr_t newIsr = (uintptr_t)malloc(isrPrototypeLength);
	memcpy((void *)newIsr, &isr_prototype, isrPrototypeLength);

	// setup new code by binary patching
	if (hasErrorCode)
		*(uint8_t *)(newIsr + isrPushInstrOffset) = 0x90;
	*(uint64_t *)(newIsr + isrNumberFieldOffset) = number;
	*(uint64_t *)(newIsr + isrAddressFieldOffset) = (uintptr_t)handler;


	// create descriptor in the IDT
	interrupt_descriptor_t *entry = &interrupt_descriptor_table[number];
	entry->base_low = (newIsr >> 0) & 0xFFFFUL;
	entry->base_middle = (newIsr >> 16) & 0xFFFFUL;
	entry->base_high = (newIsr >> 32) & 0xFFFFFFFFUL;
	entry->selector = CODE_SEGMENT_SELECTOR;
	entry->flags = INTERRUPT_TYPE_INT; // we always need to disable interrutps, as we only have one interrupt stack
	entry->stackNumber = stackNumber;
	entry->reserved = 0;
}

// Registers an interrupt handler for an interrupt that pushes no error code.
// For each interrupt, only one handler may be registerd.
// The default interrupt stack will be used.
//	number: the interrupt number (between 0 and 255)
//	handler: the interrupt handler
void interrupt_register(int number, interrupt_handler_t handler) {
	interrupt_register_ex(number, 0, handler, STACK_NUM_FALLBACK);
}


static inline void setup_stack(int number, void *start, size_t size) {
	tss.ist[number] = (uintptr_t)start + size;
}




void panic_handler(uint64_t intNumber, uint64_t errCode, execution_context_t *context) {
	LOGE("unhandled interrupt %d, error code %x64", (int)intNumber, errCode);
	LOGE("  rax: %x64  rbx: %x64", context->rax, context->rbx);
	LOGE("  rcx: %x64  rdx: %x64", context->rcx, context->rdx);
	LOGE("   r8: %x64   r9: %x64", context->r8, context->r9);
	LOGE("  r10: %x64  r11: %x64", context->r10, context->r11);
	LOGE("  r12: %x64  r13: %x64", context->r12, context->r13);
	LOGE("  r14: %x64  r15: %x64", context->r14, context->r15);
	LOGE("  rsi: %x64  rdi: %x64", context->rsi, context->rdi);
	LOGE("  rsp: %x64  rbp: %x64", context->rsp, context->rbp);
	LOGE("  rip: %x64  cr2: %x64", context->rip, (uint64_t)read_cr2());
	LOGE("execution halted");
	for (;;)
		/*cpu_halt()*/;
}

void backup_handler(uint64_t intNumber, uint64_t errCode, execution_context_t *context) {
	panic_handler(intNumber, errCode, context);
}

// Loads the interrupt descriptor table
void interrupt_init(void *tssDescriptor) {
	
	// set up some stacks in TSS
	setup_stack(STACK_NUM_KERNEL, kernel_stack, PAGE_SIZE);
	setup_stack(STACK_NUM_INT, interrupt_stack, PAGE_SIZE);
	setup_stack(STACK_NUM_FALLBACK, fallback_stack, PAGE_SIZE);

	// set up TSS descriptor in GDT
	// this descriptor is used to locate the TSS whenever an interrupt occurs
	tss_descriptor_t *tssDscr = (tss_descriptor_t *)tssDescriptor;
	tssDscr->base1 = ((uintptr_t)&tss >> 0) & 0xFFFF;
	tssDscr->base2 = ((uintptr_t)&tss >> 16) & 0xFF;
	tssDscr->base3 = ((uintptr_t)&tss >> 24) & 0xFF;
	tssDscr->base4 = ((uintptr_t)&tss >> 32) & 0xFFFFFFFF;
	tssDscr->limit = sizeof(tss) - 1;
	tssDscr->attributes = 0x0089;
	tssDscr->reserved = 0;
	set_tr(TASK_SEGMENT_SELECTOR);
	
	// set idtr before setting up the handlers, so there's a chance we can catch some memory allocation issues
	set_idtr((idtr_t) {
		.address = interrupt_descriptor_table,
			.limit = sizeof(interrupt_descriptor_table) - 1
	});

	// install some essential handlers for non recoverable system states
	interrupt_register_ex(INTERRUPT_NUMBER_DEBUG, 0, panic_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(INTERRUPT_NUMBER_NMI, 0, panic_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(INTERRUPT_NUMBER_DOUBLE, 1, panic_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(INTERRUPT_NUMBER_BADTSS, 1, panic_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(INTERRUPT_NUMBER_STACK, 1, panic_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(INTERRUPT_NUMBER_MACHINE, 0, panic_handler, STACK_NUM_FALLBACK);

	// install backup handlers for all other built in interrupts (for debugging)
	interrupt_register_ex(0x00, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x03, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x04, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x05, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x06, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x07, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x09, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x0B, 1, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x0D, 1, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x0E, 1, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x0F, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x10, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x11, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x13, 0, backup_handler, STACK_NUM_FALLBACK);
	interrupt_register_ex(0x1E, 0, backup_handler, STACK_NUM_FALLBACK);

	// install dummy handlers for debugging
	for (uint8_t i = 0x14; i; i++)
		interrupt_register_ex(i, 0, backup_handler, STACK_NUM_FALLBACK);
}
