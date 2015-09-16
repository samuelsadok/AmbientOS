/*
*
* Enables invoking 16-bit real mode BIOS functions from 64-bit long mode
*
* created: 17.01.15
*
*/


#include <system.h>
#include "realmode.h"




// Using this macro allows for importing symbols from bootloader code.
// As the address of the bootloader is not known at compile time, initialization
// code will take care of adding the appropriate offset to all bootloader references.
#define IMPORT_BOOTLOADER_SYMBOL(symbolPtrDecl, symbolPtrType, bootloaderSymbol, offset)	\
extern char bootloaderSymbol;																\
__attribute__((__section__(".bootref")))													\
symbolPtrDecl = (symbolPtrType)((uintptr_t)&(bootloaderSymbol) + (offset))


IMPORT_BOOTLOADER_SYMBOL(uint32_t(*realmodeEntry)(uint64_t, void *), uint32_t(*)(uint64_t, void *), RealModeCall, 0);
IMPORT_BOOTLOADER_SYMBOL(char *realmodeBuffer, char *, RealModeBuffer, KERNEL_OFFSET);
IMPORT_BOOTLOADER_SYMBOL(char *realmodeBuffer2, char *, disk_read_command, KERNEL_OFFSET);
IMPORT_BOOTLOADER_SYMBOL(void **memoryMapPtr, void **, memory_map_ptr, KERNEL_OFFSET);
IMPORT_BOOTLOADER_SYMBOL(void *tssDescriptor, void *, tss_descriptor, KERNEL_OFFSET);
IMPORT_BOOTLOADER_SYMBOL(uint8_t *bootdisk, uint8_t *, boot_drive, KERNEL_OFFSET);
IMPORT_BOOTLOADER_SYMBOL(uint32_t *volumeStart, uint32_t *, volume_offset, KERNEL_OFFSET);



// Executes 8 bytes of instructions in real mode.
// Returns the eflags register returned by the bios function.
// Any real mode calls must be used with caution. The system is blocked when in real mode.
// BIOS interrupts may not work properly, so the system may freeze if a BIOS call depends on interrupts (e.g. wait for keyboard)
// Actions taken:
//	1. call booloader code
//	2. save registers that should be preserved
//	3. binary patch the interrupt call
//	3. switch to real mode stack
//	4. push kernel stack pointer
//	6. push real mode context
//	7. switch to 32-bit compatibility mode
//	7. switch to 32-bit protected mode
//	8. switch to 16-bit real mode
//	9. pop real mode context
//	10. invoke interrupt
//	11. push real mode context
//	12. switch to 32-bit protected mode
//	14. switch to 32-bit compatibility
//	13. switch to 64-bit long mode
//	15. pop kernel stack pointer
//	16. switch to kernel stack
//	17. pop real mode context
//	18. return to kernel mode code
uint32_t realmode_execute(uint64_t code, realmode_context_t *context) {
	uint32_t flags;
	//debug(0x63, 0);
	atomic() {
		apic_disable();		// the BIOS can't handle APIC interrupts
		page_map_realmode();
		pic_enable();		// ... instead it expects legacy PIC interrupts
		idtr_t idtr = get_idtr(); // idtr is clobbered by BIOS code
		flags = realmodeEntry(code, context);
		set_idtr(idtr);
		pic_disable();
		page_unmap_realmode();
		apic_enable();
		//debug(0x63, 1);
	}
	return flags;
}







