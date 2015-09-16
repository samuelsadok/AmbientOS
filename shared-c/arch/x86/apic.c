

#include <system.h>
#include "apic.h"

#define IA32_APIC_BASE_MSR			0x1BUL
#define IA32_APIC_BASE_MSR_BSP		0x100UL // Processor is a bootstrap processor
#define IA32_APIC_BASE_MSR_ENABLE	0x800UL
#define IA32_APIC_BASE_MSR_ADDRESS	0xFFFFFFFFFFFFF000UL


#define CPUID_BIT_MCE				7	// machine check exception (CPUID:01h.EDX)
#define CPUID_BIT_MCA				14	// machine check architecture (CPUID:01h.EDX)

#define IA32_MCG_CAP				0x179
#define IA32_MCG_CAP_COUNT			0xFFUL	// number of machine check banks
#define IA32_MCG_CAP_CMCI_AVL		0x400UL	// number of machine check banks
#define IA32_MCG_STATUS				0x17A
#define IA32_MCG_CTL				0x17B

#define IA32_MCi_CTL2				0x280		// one register for each bank, starting here (see MCG_CAP for number of banks)
#define IA32_MCi_CTL2_THRESHOLD		0x7FFF		// maximum threshold is implementation specific
#define IA32_MCi_CTL2_CMCI_EN		(1UL << 30)	// enable CMCI generation

#define IA32_MCi_CTL				0x400		// one register for each bank, starting here (every 4th register)
#define IA32_MCi_STATUS				0x401
#define IA32_MCi_ADDR				0x402		// implementation specific
#define IA32_MCi_MISC				0x403



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

#define APIC_INT_MASKED		0x00010000UL

#define APIC_TIMER_ONE_SHOT	0x00000000
#define APIC_TIMER_PERIODIC	0x00020000
#define APIC_TIMER_DEADLINE	0x00040000 // timer fires after a specified deadline timestamp

#define APIC_REGFILE_LENGTH			(0x1000)

enum {
	PIC_INT					= 0x20,	// 16 legacy PIC interrupts

	APIC_INT_CMCI			= 0xF0, // Too many corrected machine checks
	APIC_INT_TIMER			= 0xF1, // Local APIC timer
	APIC_INT_THERMAL		= 0xF2, // Thermal monitor
	APIC_INT_PERFORMANCE	= 0xF3, // Performance Counter
	APIC_INT_LINT0			= 0xF4, // External Int 0
	APIC_INT_LINT1			= 0xF5, // External Int 1

	APIC_INT_ERROR			= 0xFE, // The local APIC encountered an error
	APIC_INT_SPURIOUS		= 0xFF // Spurious interrupt (returns immediately in asm code). Don't change this value (to keep compatibility with older processors)
};



void *apicBase;	// points to the first address of the local APIC register file in linear address space

timer_callback_t timerCallback = NULL;

volatile system_ticks_t systemTicks = 0;


// Checks for the presence of the local APIC
int apic_available(void) {
	uint32_t eax, ebx, ecx, edx;
	cpuid(1, &eax, &ebx, &ecx, &edx);
	return edx & CPUID_FLAG_APIC;
}


// Set the physical address for local APIC registers. The address must be 4kB aligned
static inline void apic_base_write(uintptr_t physicalBase) {
	assert(!(physicalBase & 0xFFF));
	write_msr(IA32_APIC_BASE_MSR, physicalBase | IA32_APIC_BASE_MSR_ENABLE);
}


// Get the physical address of the local APIC registers page
static inline uintptr_t apic_base_read(void) {
	return (uintptr_t)(read_msr(IA32_APIC_BASE_MSR) & IA32_APIC_BASE_MSR_ADDRESS);
}


// Sets a local APIC register
static inline void apic_reg_write(uint32_t id, uint32_t value) {
	*((volatile int *)((char *)apicBase + id)) = value;
}


// Returns a local APIC register value
static inline uint32_t apic_reg_read(uint32_t id) {
	return *((volatile int *)((char *)apicBase + id));
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


// Sets the callback for the APIC timer
//	callback: a routine that will be called whenever the timer fires.
//	This routine must comply with the constraints of an interrupt handler.
//	If it is null, the next timer interrupt will trigger a panic.
void apic_timer_config(timer_callback_t callback) {
	timerCallback = callback;
}


// Starts the local APIC timer. This must not be called before apic_init.
//	prescaler: 111: 1, 000: 2, 001: 4, 010: 8, 011: 16, 100: 32, 101: 64, 110: 128
void apic_timer_start(int interval, int prescaler) {
	apic_reg_write(APIC_REG_T_DIVIDE, ((prescaler & 0x04) << 1) | (prescaler & 0x03));
	apic_reg_write(APIC_REG_T_INTERVAL, interval);
	apic_reg_write(APIC_REG_LVT_TIMER, apic_reg_read(APIC_REG_LVT_TIMER) & ~APIC_INT_MASKED); // unmask timer interrupt
	//apic_reg_write(APIC_REG_T_COUNT, interval);
}


// Stops the local APIC timer.
// The system ticks counter is tied to the APIC and is not incremented when the timer is stopped.
void apic_timer_stop(void) {
	apic_reg_write(APIC_REG_LVT_TIMER, apic_reg_read(APIC_REG_LVT_TIMER) | APIC_INT_MASKED); // mask timer interrupt
	apic_reg_write(APIC_REG_T_COUNT, 0);
}


// Triggers the timer interrupt
void apic_timer_trigger(void) {
	__asm volatile ("int 0xF1"); // potential issue: a EOI will be sent
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



char apicMsg1[] = "\napic: 0x";
char apicMsg2[] = " - 0x";
char apicMsg3[] = "\nexecution halted\n";
char picMsg[] = "\npic: 0x";

void apic_interrupt_handler(uint64_t intNumber, uint64_t errCode, execution_context_t *context) {
	if (intNumber == APIC_INT_TIMER && timerCallback) {
		systemTicks++;
		timerCallback(context);
	} else {
		errCode = apic_read_error_status();
		teletype_print_string(apicMsg1, sizeof(apicMsg1), 0x07);
		teletype_print_hex(&intNumber, 8, 0x07);
		teletype_print_string(apicMsg2, sizeof(apicMsg2), 0x07);
		teletype_print_hex(&errCode, 8, 0x07);
		teletype_print_string(apicMsg3, sizeof(apicMsg3), 0x07);
		while (1)
			cpu_halt();
	}

	if (intNumber != APIC_INT_SPURIOUS) // don't send EOI for spurious interrupts
		apic_reg_write(APIC_REG_EOI, 0); // a write to this register signals End-Of-Interrupt to the APIC and (if necessary) all I/O APICs
}


void pic_interrupt_handler(uint64_t intNumber, uint64_t errCode, execution_context_t *context) {
	// ignore legacy PIC interrupts
	//teletype_print_string(picMsg, sizeof(picMsg), 0x07);
	//teletype_print_hex(&intNumber, 8, 0x07);
}



// Sends a sequence of initialization commands to the PIC
void pic_init(int intLocation) {
	int dM = in(0x21);
	int dS = in(0xA1);

	// init master and space PIC
	out(0x20, 0x11);
	out(0xA0, 0x11);
	io_wait();

	// set interrupt vectors
	out(0x21, intLocation);
	out(0xA1, intLocation + 8);
	io_wait();

	// set up cascading
	out(0x21, 4);
	out(0xA1, 2);
	io_wait();

	// set to 8086 mode
	out(0x21, 0x01);
	out(0xA1, 0x01);
	io_wait();

	// restore values
	out(0x21, dM);
	out(0xA1, dS);
	io_wait();
}

char picMaskMaster;
char picMaskSlave;

// Enables the legacy 8259 master and slave PICs
void pic_enable() {
	pic_init(8);

	out(0x21, picMaskMaster);
	out(0xA1, picMaskSlave);
	io_wait();
}


// Disables the legacy 8259 master and slave PICs by mapping them to reserved interrupts
void pic_disable() {
	pic_init(PIC_INT);

	out(0x21, 0xFF);
	out(0xA1, 0xFF);
	io_wait();
}


// Enables delivery of local APIC interrupts
void apic_enable(void) {
	// software enable APIC, spurious interrupt at APIC vector 0xFF, enable EOI broadcast, enable focus checking
	apic_reg_write(APIC_REG_SPURIOUS, 0x100 | APIC_INT_SPURIOUS);
}


// Disables delivery of local APIC interrupts
void apic_disable(void) {
	// todo: disable I/O APIC
	apic_reg_write(APIC_REG_SPURIOUS, APIC_INT_SPURIOUS);
}



// initializes the APIC
//	timerCallback: an interrupt handler that is executed whenever the timer fires
void apic_init() {
	// Disable legacy PIC
	picMaskMaster = in(0x21);
	picMaskSlave = in(0xA1);
	pic_disable();

	// Check for APIC availability and map it to virtual address space
	assert(apic_available());
	uintptr_t base = apic_base_read();
	apicBase = page_map((void *)base, APIC_REGFILE_LENGTH, 0, 1, 0);

	// todo: move to separate file
	int claimedBanks = 0;
	if (cpuid_test(1, 0, 0, 0, (1 << CPUID_BIT_MCE) | (1 << CPUID_BIT_MCA))) { // check if MCE and MCA are available
		if (read_msr(IA32_MCG_CAP) & IA32_MCG_CAP_CMCI_AVL) { // check if CMCI is available
			int mcCount = read_msr(IA32_MCG_CAP) & IA32_MCG_CAP_COUNT;
			for (int i = 0; i < mcCount; i++) {
				uint64_t val = read_msr(IA32_MCi_CTL2 + i);

				// check if this bank is already being handled by another processor
				if (val & IA32_MCi_CTL2_CMCI_EN)
					continue;

				// handle this bank
				val |= IA32_MCi_CTL2_CMCI_EN;
				write_msr(IA32_MCi_CTL2 + i, val);

				// check if bank supports CMCI
				val = read_msr(IA32_MCi_CTL2 + i);
				if (!(val & IA32_MCi_CTL2_CMCI_EN))
					continue;

				// todo: remember to handle this particular bank on local processor (see vol 3B section 15.5.2)
				claimedBanks++;

				// set threshold to maximum
				val |= 0x7FFF; // some bits may get discarted
				write_msr(IA32_MCi_CTL2 + i, val);
			}
		}
	}


	// install dummy handlers for legacy PIC
	for (int i = 0; i < 16; i++)
		interrupt_register(PIC_INT + i, pic_interrupt_handler);

	// setup handlers for all local APIC built in interrupts
	if (claimedBanks)
		interrupt_register(APIC_INT_CMCI, apic_interrupt_handler);
	interrupt_register(APIC_INT_TIMER, apic_interrupt_handler);
	interrupt_register(APIC_INT_THERMAL, apic_interrupt_handler); // todo: should be implementation specific
	interrupt_register(APIC_INT_PERFORMANCE, apic_interrupt_handler); // todo: should be implementation specific
	interrupt_register(APIC_INT_LINT0, apic_interrupt_handler);
	interrupt_register(APIC_INT_LINT1, apic_interrupt_handler);
	interrupt_register(APIC_INT_SPURIOUS, apic_interrupt_handler);
	interrupt_register(APIC_INT_ERROR, apic_interrupt_handler);


	// configure all local APIC built in interrupts (start off with all interrupts masked)
	if (claimedBanks)
		apic_reg_write(APIC_REG_LVT_CMCI, APIC_INT_CMCI | APIC_INT_MASKED);
	apic_reg_write(APIC_REG_LVT_TIMER, APIC_INT_TIMER | APIC_TIMER_PERIODIC | APIC_INT_MASKED);
	apic_reg_write(APIC_REG_LVT_THERMAL, APIC_INT_THERMAL | APIC_INT_MASKED);
	apic_reg_write(APIC_REG_LVT_PERFORMANCE, APIC_INT_PERFORMANCE | APIC_INT_MASKED);
	apic_reg_write(APIC_REG_LVT_LINT0, APIC_INT_LINT0 | APIC_INT_MASKED); // edge triggered, active high (might need to be adjusted) (see MPS table / ACPI)
	//apic_reg_write(APIC_REG_LVT_LINT0, APIC_INT_LINT0); // edge triggered, active high (might need to be adjusted) (see MPS table / ACPI)
	apic_reg_write(APIC_REG_LVT_LINT1, APIC_INT_LINT1 | APIC_INT_MASKED);
	apic_reg_write(APIC_REG_LVT_ERROR, APIC_INT_ERROR | APIC_INT_MASKED);


	
	apic_enable();
}
