/*
 * io.c
 *
 * Created: 07.03.2014 23:48:18
 *  Author: Samuel
 */ 

#include <system.h>
#ifdef USING_DFU
#include <system/dfu.h>
#endif


// Initializes the built-in peripherals of the processor that are used by the application.
void avr_init(void) {
	#ifdef __AVR_XMEGA__
		sysclk_init();
		ioport_init();
		irq_initialize_vectors();

		// enable all interrupt levels (global interrupts are still disabled)
		pmic_init();

#ifdef USING_BOOTLOADER
		pmic_set_vector_location(PMIC_VEC_BOOT); // interrupt vectors are at the beginning of the bootloader section
#else
		pmic_set_vector_location(PMIC_VEC_APPLICATION); // interrupt vectors are at the beginning of the application section
#endif

		// init built-in eeprom
#ifdef USING_NVM
		nvm_init();
#endif

#ifdef USING_BUILTIN_TWI_MASTER
		builtin_i2c_master_init();
#endif

#ifdef USING_BUILTIN_TWI_SLAVE
		builtin_i2c_slave_init();
#endif

#ifdef USING_BUILTIN_TIMERS
		// start timer 4 with a 1.024ms period (at 32MHz system clock)
		tc45_wex_set_otmx(&WEXC, WEX_OTMX_DEFAULT); // no WEX remapping
		tc45_enable(&TCC4);
		tc45_set_wgm(&TCC4, TC45_WG_SS);
		tc45_write_period(&TCC4, 0x7FFF);
		//tc45_write_period(&TCC4, 0x03FF);
		tc45_write_clock_source(&TCC4, TC45_CLKSEL_DIV1_gc);
#endif
		
#ifdef USING_DFU
		if (!nvmValid)
			dfu_init_nvm();

		dfu_init();
#endif

#if defined(USING_NVM) && !defined(USING_BOOTLOADER)
		if (!nvmValid)
			nvm_data_init();
#endif
		
	#else
		
		#error "unsupported architecture"
		
		// TIMER 0:
		// 8-bit Fast PWM, prescaler 8 (has to be activated for each pin)
		TCCR0A = (1 << WGM01) | (1 << WGM00);
		TCCR0B = (1 << CS01);
		
		// TIMER 1:
		// 10 bit, prescaler 8 (can be used as Fast PWM)
		TCCR1A = (1 << WGM11) | (1 << WGM10);
		TCCR1B = (1 << WGM12) | (1 << CS11);
		TIMSK1 |= (1 << TOIE1);

		// TIMER 2:
		// prescaler 8, (the rest is done in the ISR if PWM5 or 6 are activated)
		TCCR2A = (1 << WGM21) | (1 << WGM20);
		TCCR2B = (1 << CS21);
		
	#endif
	
	
}



volatile uint8_t resetCauses;
extern unsigned char _end;

// special function that is executed before any other code (even before the stack exists)
__attribute__ ((naked)) __attribute__ ((section (".init1")))
void __preinit(void) {
	
	cli();
	
#ifdef AUTO_RESET
#	ifdef __AVR_XMEGA__
	wdt_set_timeout_period(WDT_TIMEOUT_PERIOD_1KCLK);
	wdt_enable();
#	else
	wdt_enable(WDTO_2S);
#	endif
#else
	wdt_disable();
#endif


	// place cookie at the end of the heap
	*(&_end + 1) = STACK_MAGIC_COOKIE;
	
	// store reset cause, panic if it was a software reset
	
	#ifdef __AVR_XMEGA__
		resetCauses = reset_cause_get_causes();
	#else
		resetCauses = MCUSR;
	#endif
	
	// todo: kernel panic when reset cause is 0
	
	#ifdef __AVR_XMEGA__
		reset_cause_clear_causes(resetCauses);
	#else
		MCUSR = 0;
	#endif
	
	//__asm volatile ("in r20, %0\n"					// load MCUSR
	//"cpi r20, 0x00\n"
	//"brne __preinit_legit_reset\n"	// if non-zero this is a legit reset
	//"clr r18\n"
	//"clr r19\n"
	//"clr r21\n"
	//"ldi r22, %1\n"
	//"ldi r23, %2\n"
	//"ldi r24, %3\n"
	//"ldi r25, %4\n"
	//"rjmp __kernel_panic\n"
	//
	//"__preinit_legit_reset:"
	//"sts resetCause, r20\n"			// store reset cause
	//"ldi r20, 0x00\n"				// clear MCUSR
	//"out %0, r20"
	//:
	//: "M" (_SFR_IO_ADDR(MCUSR)), "M" (PANIC_NULL_JUMP & 0xFF), "M" ((PANIC_NULL_JUMP >> 8) & 0xFF), "M" (__LINE__ & 0xFF), "M" ((__LINE__ >> 8) & 0xFF));
}


// helper function after memory validation fail
__attribute__ ((naked))
void __memory_validation_failed(void) {
	__asm volatile (
	".global __memory_validation_failed	\n"
		"__memory_validation_failed:		\n"
		"cli								\n"
		"clr r16							\n"
		"clr r17							\n"
		"clr r18							\n"
		"clr r19							\n"
		"clr r20							\n"
		"clr r21							\n"
		"ldi r22, %0						\n"
		"ldi r23, %1						\n"
		"rjmp bug_check_ex					\n"
		: : "M" (STATUS_STACK_OVERFLOW & 0xFF), "M" ((STATUS_STACK_OVERFLOW >> 8) & 0xFF));
}



// executes approximately every 1ms
#ifndef __AVR_XMEGA__
ISR (TIMER1_OVF_vect) {
	static uint8_t locked = 0;
	if (!(++systemClock)) callback_execute(timer_overflow_handler);
	
	if (locked) return;
	locked = 1;
	interrupts_on();
	
	callback_execute(timer_tick_handler);
	
	interrupts_off();
	locked = 0;
};
#endif



// fired when no ISR is provided for an interrupt
__attribute__ ((naked))
ISR (BADISR_vect) {
	__asm volatile ("clr r1\n");
	bug_check(STATUS_BAD_INTERRUPT, 0);
}




// For the AVR architecture, sync functions are not compiler-built-in.
// Since AVR MCU's are single-core, we can just disable interrupts.
bool __sync_bool_compare_and_swap_1(uint8_t *ptr, uint8_t oldVal, uint8_t newVal) {
	atomic() {
		if (*ptr == oldVal) {
			*ptr = newVal;
			return 1;
		}
	}
	return 0;
}


