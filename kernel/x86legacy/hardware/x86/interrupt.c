
#include <stdint.h>
#include <hardware\teletype.h>
#include "interrupt.h"

#define null ((void *)0)

interrupt_idt_entry_t	interrupt_descriptor_table[256];

extern void lidt(void);
extern int *isr_linked_list;


// Sets an interrupt entry
void interrupt_set_isr(int number, int callback_ptr, uint8_t flags) {
	interrupt_idt_entry_t *entry = &(interrupt_descriptor_table[number]);
	entry->base_low = (callback_ptr & 0xFFFF);
	entry->base_high = ((callback_ptr >> 16) & 0xFFFF);
	entry->selector = (callback_ptr ? 0x08 : 0x00); // standard code selector or null selector
	entry->flags = flags;
	entry->reserved = 0;
}


// Loads the interrupt descriptor table
void interrupt_init() {
	for (int i = 0; i < (sizeof(interrupt_descriptor_table) / sizeof(interrupt_idt_entry_t)); i++)
		interrupt_set_isr(i, 0, 0);

	//int *current = isr_linked_list;
	//while (current) {
	//	interrupt_set_isr(current[1], (int)(&(current[2])), INTERRUPT_TYPE_INT);
	//	current = (int *)(*current);
	//}
	for (int *current = isr_linked_list; current; current = (int *)(*current))
		interrupt_set_isr(current[1], (int)(&(current[2])), INTERRUPT_TYPE_INT);

	lidt();
}



__asm(
".globl _cpu_isr_common_stub \n"
"_cpu_isr_common_stub:		 \n"

"pusha			\n" // Pushes edi, esi, ebp, esp, ebx, edx, ecx, eax

"mov ax, ds		\n" // Lower 16 - bits of eax = ds.
"push eax		\n" // save the data segment descriptor

"mov ax, 0x10	\n" // load the kernel data segment descriptor
"mov ds, ax		\n"
"mov es, ax		\n"
"mov fs, ax		\n"
"mov gs, ax		\n"

"call _cpu_isr_handler \n"	// invoke the actual handler

"pop eax	\n" // reload the original data segment descriptor
"mov ds, ax	\n"
"mov es, ax	\n"
"mov fs, ax	\n"
"mov gs, ax	\n"

"popa		\n" // Pops edi, esi, ebp...
"add esp, 8	\n" // Cleans up the pushed error code and pushed ISR number
"iret		\n" // pops 5 things at once : CS, EIP, EFLAGS, SS, and ESP
);


char int_text1[] = "\ninterrupt 0x";
char int_text2[] = " err 0x";

extern void fancy_delay(uint32_t count);
void cpu_isr_handler(saved_registers_t regs) {
	teletype_print_string(int_text1, sizeof(int_text1), 0x07);
	teletype_print_hex(&regs.int_no, 4, 0x07);
	teletype_print_string(int_text2, sizeof(int_text2), 0x07);
	teletype_print_hex(&regs.err_code, 4, 0x07);
	//fancy_delay(0x8FFFFFF);
	//while (1);
}
