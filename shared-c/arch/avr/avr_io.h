/*
 * xmega_io.h
 *
 * Created: 07.03.2014 23:24:28
 *  Author: Samuel
 */ 
#if !defined(__AVR_IO_H__)
#define __AVR_IO_H__

#ifndef __SYSTEM_H__
#error "include system.h instead of this file"
#endif

#include <string.h>
#include <avr/io.h>
#include <avr/pgmspace.h>
#include <util/delay.h>

#define STACK_MAGIC_COOKIE 0x42

#ifdef __AVR_XMEGA__

// ATXmega specific headers
#include "asf.h"

#else

// ATtiny/ATmega specific headers
#include <avr/interrupt.h>
#include <avr/eeprom.h>
#include <avr/wdt.h>
#include <avr/boot.h>

#endif

#include <avr/pgmspace.h>


#ifdef USING_BOOTLOADER
#  include "bootloader.h"
#else
#  define EXTENDED_TEXT
#endif
#include "gpio.h"
#include "i2c_master.h"
#include "i2c_slave.h"
#include "interrupt.h"
#ifdef USING_BUILTIN_PWM
#include "pwm.h"
#endif
#include "timer.h"




#ifdef __AVR_XMEGA__
	
// not available on all XMegas
#define xmega_map_vport0(realPort)	(PORTCFG.VPCTRLA = (PORTCFG.VPCTRLA & ~PORTCFG_VP0MAP_gm) | (realPort))
#define xmega_map_vport1(realPort)	(PORTCFG.VPCTRLA = (PORTCFG.VPCTRLA & ~PORTCFG_VP1MAP_gm) | (realPort))
#define xmega_map_vport2(realPort)	(PORTCFG.VPCTRLB = (PORTCFG.VPCTRLB & ~PORTCFG_VP2MAP_gm) | (realPort))
#define xmega_map_vport3(realPort)	(PORTCFG.VPCTRLB = (PORTCFG.VPCTRLB & ~PORTCFG_VP3MAP_gm) | (realPort))
	
typedef irqflags_t cpu_int_state_t;
	
#else
	
typedef uint8_t cpu_int_state_t;

static cpu_int_state_t cpu_irq_save() {
	cpu_int_state_t state = SREG;
	cli();
	return state;
}
static void cpu_irq_restore(cpu_int_state_t state) {
	SREG = state;
}
	
#endif



#define interrupts_on()			sei()
#define interrupts_off()		cli()
/*
#define __atomic_enter()		cpu_irq_save()
#define __atomic_exit(state)	cpu_irq_restore(state)
*/

typedef struct { bool running; cpu_int_state_t state; } atomic_helper_t;

// Switches interrupts off. Returns 1 if interrupts were on.
static inline atomic_helper_t atomic_enter(void) {
	return (atomic_helper_t) { .running = 1, .state = cpu_irq_save() };
}

// Switches interrupts off.
static inline atomic_helper_t atomic_exit(atomic_helper_t var) {
	cpu_irq_restore(var.state);
	return (atomic_helper_t) { .running = 0 };
}



// atomic() { statements; } executes a code block with interrupts temporarily disabled
#define		atomic()	for (atomic_helper_t CONCAT(__atomic_, __LINE__) = atomic_enter(); CONCAT(__atomic_, __LINE__).running; CONCAT(__atomic_, __LINE__) = atomic_exit(CONCAT(__atomic_, __LINE__)))


void avr_init(void);


static inline void __validate_memory(uint16_t code_line) {
	__asm volatile ("lds r20, (_end+1)\n" // check if the magic cookie is still there
	"ldi r18, %0\n"
		"cp r18, r20\n"
		"breq validate_done\n"
		"cli\n"
		"jmp __memory_validation_failed\n"
		"validate_done:\n"
		:
	: "M" (STACK_MAGIC_COOKIE));
}


// Performs a software reset.
static inline __attribute__((__noreturn__)) void __reset(void) {
	reset_do_soft_reset();
	for (;;);
}



#ifdef USING_NVM
#  include "eeprom.h"
#endif


#endif // __AVR_IO_H__
