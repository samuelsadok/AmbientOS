

#include <stdint.h>
#include <hardware\teletype.h>
#include <hardware\x86\io.h>
#include <hardware\x86\interrupt.h>
#include "apic.h"

#define IA32_APIC_BASE_MSR 0x1B
#define IA32_APIC_BASE_MSR_BSP 0x100 // Processor is a bootstrap processor
#define IA32_APIC_BASE_MSR_ENABLE 0x800
#define IA32_APIC_BASE_MSR_ADDRESS 0x0000000FFFFFF000

#define APIC_REG_VERSION	0x30
#define APIC_REG_EOI		0xB0
#define APIC_REG_SPURIOUS	0xF0
#define APIC_REG_ERR_STATUS	0x280
#define APIC_REG_T_INTERVAL	0x380
#define APIC_REG_T_COUNT	0x390
#define APIC_REG_T_DIVIDE	0x3E0
#define APIC_REG_LVT_CMCI			0x2F0
#define APIC_REG_LVT_TIMER			0x320
#define APIC_REG_LVT_THERMAL		0x330 // not always to be implemented
#define APIC_REG_LVT_PERFORMANCE	0x340 // not guaranteed to stay be this address
#define APIC_REG_LVT_LINT0			0x350
#define APIC_REG_LVT_LINT1			0x360
#define APIC_REG_LVT_ERROR			0x370

#define APIC_INT_MASKED		0x00010000

#define APIC_TIMER_ONE_SHOT	0x00000000
#define APIC_TIMER_PERIODIC	0x00020000
#define APIC_TIMER_DEADLINE	0x00040000 // timer fires after a specified deadline timestamp


enum {
	APIC_INT_CMCI			= 0xF0, // Too many corrected machine checks
	APIC_INT_TIMER			= 0xF1, // Local APIC timer
	APIC_INT_THERMAL		= 0xF2, // Thermal monitor
	APIC_INT_PERFORMANCE	= 0xF3, // Performance Counter
	APIC_INT_LINT0			= 0xF4, // External Int 0
	APIC_INT_LINT1			= 0xF5, // External Int 1

	APIC_INT_ERROR			= 0xFE, // The local APIC encountered an error
	APIC_INT_SPURIOUS		= 0xFF // Spurious interrupt (returns immediately in asm code). Don't change this value (to keep compatibility with older processors)
};



void *apic_base;	// points to the first address of the local APIC register file


// Checks for the presence of the local APIC
int apic_available(void) {
	uint32_t eax, ebx, ecx, edx;
	cpuid(1, &eax, &ebx, &ecx, &edx);
	return edx & CPUID_FLAG_APIC;
}


// Set the physical address for local APIC registers. The address must be 4kB aligned
static inline void apic_base_write(void * apic_base) {
	write_msr(IA32_APIC_BASE_MSR, ((uintptr_t)apic_base & 0x0000000FFFFFF000) | IA32_APIC_BASE_MSR_ENABLE);
}


// Get the physical address of the local APIC registers page
static inline void * apic_base_read(void) {
	return (void *)(uintptr_t)(read_msr(IA32_APIC_BASE_MSR) & IA32_APIC_BASE_MSR_ADDRESS);
}


// Sets a local APIC register
static inline void apic_reg_write(uint32_t id, uint32_t value) {
	*((volatile int *)(apic_base + id)) = value;
}


// Returns a local APIC register value
static inline uint32_t apic_reg_read(uint32_t id) {
	return *((volatile int *)(apic_base + id));
}


// Returns version of the local APIC (0x10 - 0x15 means integrated, other values reserved)
int apic_read_version(void) {
	return apic_reg_read(APIC_REG_VERSION) & 0xFF;
}


// Returns the number of LVT entries
int apic_read_lvt_num(void) {
	return (apic_reg_read(APIC_REG_VERSION) >> 16) & 0xFF;
}


// Returns the last error code of the APIC
int apic_read_error_status(void) {
	apic_reg_write(APIC_REG_ERR_STATUS, 0); // we should write to this register first
	return apic_reg_read(APIC_REG_ERR_STATUS);
}


// Starts the local APIC timer. It is assumed that the timer entry in the LVT is already set up.
//	prescaler: 111: 1, 000: 2, 001: 4, 010: 8, 011: 16, 100: 32, 101: 64, 110: 128
void apic_timer_start(int interval, int prescaler) {
	apic_reg_write(APIC_REG_T_DIVIDE, ((prescaler & 0x04) << 1) | (prescaler & 0x03));
	apic_reg_write(APIC_REG_T_INTERVAL, interval);
	//apic_reg_write(APIC_REG_T_COUNT, interval);
}


// Stops the local APIC timer.
void apic_timer_stop(void) {
	apic_reg_write(APIC_REG_T_COUNT, 0);
}


// Sets an I/O APIC register
void io_apic_write(void *ioapicaddr, uint32_t reg, uint32_t value) {
	uint32_t volatile *ioapic = (uint32_t volatile *)ioapicaddr;
	ioapic[0] = (reg & 0xff);
	ioapic[4] = value;
}


// Returns an I/O APIC register value
uint32_t io_apic_read(void *ioapicaddr, uint32_t reg) {
	uint32_t volatile *ioapic = (uint32_t volatile *)ioapicaddr;
	ioapic[0] = (reg & 0xff);
	return ioapic[4];
}


// initializes the APIC
apic_status_t apic_init(void) {
	// Check for APIC
	if (!apic_available()) return APIC_NOT_AVAILABLE;

	// Hardware enable the Local APIC if it wasn't enabled
	apic_base = apic_base_read();
	apic_base_write(apic_base);


	// Configure local APIC interrupts
	apic_reg_write(APIC_REG_LVT_CMCI, APIC_INT_CMCI);
	apic_reg_write(APIC_REG_LVT_TIMER, APIC_INT_TIMER | APIC_TIMER_PERIODIC);
	apic_reg_write(APIC_REG_LVT_THERMAL, APIC_INT_THERMAL);
	apic_reg_write(APIC_REG_LVT_PERFORMANCE, APIC_INT_PERFORMANCE);
	apic_reg_write(APIC_REG_LVT_LINT0, APIC_INT_LINT0 | APIC_INT_MASKED); // edge triggered, active high (might need to be adjusted) (see MPS table / ACPI)
	//apic_reg_write(APIC_REG_LVT_LINT0, APIC_INT_LINT0); // edge triggered, active high (might need to be adjusted) (see MPS table / ACPI)
	apic_reg_write(APIC_REG_LVT_LINT1, APIC_INT_LINT1);
	apic_reg_write(APIC_REG_LVT_ERROR, APIC_INT_ERROR);


	// software enable APIC, spurious interrupt at APIC vector 0xFF, enable EOI broadcast, enable focus checking
	apic_reg_write(APIC_REG_SPURIOUS, 0x100 | APIC_INT_SPURIOUS);

	return APIC_SUCCESS;
}


char bla[] = "\napic: 0x";
char bla2[] = " - 0x";
void apic_isr_handler(saved_registers_t regs) {
	int errno = apic_read_error_status();
	teletype_print_string(bla, sizeof(bla), 0x07);
	teletype_print_hex(&regs.int_no, 4, 0x07);
	teletype_print_string(bla2, sizeof(bla2), 0x07);
	teletype_print_hex(&errno, 4, 0x07);
}


__asm(
".globl _apic_isr_common_stub \n"
"_apic_isr_common_stub:		 \n"

"pusha			\n" // Pushes edi, esi, ebp, esp, ebx, edx, ecx, eax

"mov ax, ds		\n" // Lower 16 - bits of eax = ds.
"push eax		\n" // save the data segment descriptor

"mov ax, 0x10	\n" // load the kernel data segment descriptor
"mov ds, ax		\n"
"mov es, ax		\n"
"mov fs, ax		\n"
"mov gs, ax		\n"

"call _apic_isr_handler \n"	// invoke the actual handler

"pop eax	\n" // reload the original data segment descriptor
"mov ds, ax	\n"
"mov es, ax	\n"
"mov fs, ax	\n"
"mov gs, ax	\n"

"popa		\n" // Pops edi, esi, ebp...
"add esp, 8	\n" // Cleans up the pushed error code and pushed ISR number
"mov eax, [_apic_base]\n"
"mov [eax + 0xB0], eax\n"	// a write to this register signals End-Of-Interrupt to the APIC
"iret		\n" // pops 5 things at once : CS, EIP, EFLAGS, SS, and ESP
);
