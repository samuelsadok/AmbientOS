/*
 * pwm.h
 *
 * Header file for various clock facilities:  (DESCRIPTION DEPREACED)
 * 
 * 1x 32-bit system clock counter		interval: 8196 ticks (@ 8MHz)
 * 2x 8-bit pure hardware PWM (1,2)		0: min, 255: max (driven by timer0)
 * 2x 10-bit pure hardware PWM (3,4)	0: min, 1023: max (driven by timer1)
 * 2x 10-bit semi-hardware PWM (5,6)	0: min, 1023: max (driven by timer2)
 * 1x 7-bit counter						interval: 2048 ticks, max: 128 (driven by timer2)
 *
 * Global interrupts must be enabled for: system counter, PWM5,6, counter1
 * clock_asm.c must be compiled for: PWM5,6, counter1
 * The timer0 overflow vector can be used separately and fires every 2048 CPU ticks.
 *
 * Created: 27.12.2013
 *  Author: cpuheater
 */


#ifndef PWM_H_
#define PWM_H_


#ifdef USING_BUILTIN_PWM
#ifdef __AVR_XMEGA__


static inline uint8_t pwm_get_index(ioport_pin_t pin) {
	uint8_t index = arch_ioport_pin_to_index(pin);
	if (index >= 4) index -= 4;
	return index;
}

// Enables PWM output on the specified pin.
// Specifying a non-PWM pin results in undefined behavior.
// Currently only timer4 on PORTC is supported.
static inline void pwm_enable(ioport_pin_t pin) {
	ioport_set_pin_dir(pin, IOPORT_DIR_INPUT);
	
	// remap pwm output
	uint8_t index = pwm_get_index(pin);
	if (arch_ioport_pin_to_mask(pin) & 0x0F)
		arch_ioport_pin_to_base(pin)->REMAP &= ~(1 << index);
	else
		arch_ioport_pin_to_base(pin)->REMAP |= (1 << index);
	
	uint8_t inverted = 0;
	switch (index) {
		case 0: tc45_enable_cc_channels(&TCC4, TC45_CCACOMP); inverted = 1; break;
		case 1: tc45_enable_cc_channels(&TCC4, TC45_CCBCOMP); inverted = 0; break;
		case 2: tc45_enable_cc_channels(&TCC4, TC45_CCCCOMP); inverted = 0; break;
		case 3: tc45_enable_cc_channels(&TCC4, TC45_CCDCOMP); inverted = 1; break;
	}
	
	if (inverted) ioport_set_pin_mode(pin, IOPORT_MODE_INVERT_PIN);
	ioport_set_pin_level(pin, inverted);
	ioport_set_pin_dir(pin, IOPORT_DIR_OUTPUT);
	//ioport_set_pin_mode(pin, IOPORT_MODE_TOTEM);
	
}


// Sets the PWM output to the specified value. No input checks are performed, the caller must ensure that the value is within range.
static inline void pwm_set(ioport_pin_t pin, uint16_t val) {
	switch (pwm_get_index(pin)) {
		case 0: tc45_write_cc(&TCC4, TC45_CCA, 0x8000 - val); break;
		case 1: tc45_write_cc(&TCC4, TC45_CCB, val); break;
		case 2: tc45_write_cc(&TCC4, TC45_CCC, val); break;
		case 3: tc45_write_cc(&TCC4, TC45_CCD, 0x8000 - val); break;
	}
}

// Disables PWM output on the specified pin and sets the pin to low.
// Specifying a non-PWM pin results in undefined behavior.
static inline void pwm_disable(ioport_pin_t pin) {
	switch (pwm_get_index(pin)) {
		case 0: tc45_disable_cc_channels(&TCC4, TC45_CCACOMP); break;
		case 1: tc45_disable_cc_channels(&TCC4, TC45_CCBCOMP); break;
		case 2: tc45_disable_cc_channels(&TCC4, TC45_CCCCOMP); break;
		case 3: tc45_disable_cc_channels(&TCC4, TC45_CCDCOMP); break;
	}
}



#else // legacy

#include <hardware/gpio.h>

#define PWM1	OCR0A	// output compare 0 A
#define PWM2	OCR0B	// output compare 0 B
#define PWM3	OCR1A	// output compare 1 A
#define PWM4	OCR1B	// output compare 1 B
#define PWM5	ocr2A	// output compare 2 A
#define PWM6	ocr2B	// output compare 2 B

#define GPIO_PWM1	GPIO_PD6
#define GPIO_PWM2	GPIO_PD5
#define GPIO_PWM3	GPIO_PB1
#define GPIO_PWM4	GPIO_PB2
#define GPIO_PWM5	GPIO_PB3
#define GPIO_PWM6	GPIO_PD3


#define COUNTER1_INTERVAL	(1000000UL * 2048 / F_CPU)	// interval at which counter1 is incremented [µs] (2048 ticks)

extern volatile uint16_t ocr2A, ocr2B;
extern volatile uint8_t counter1;


// sets the specified PWM output to a specified value
//	gpio: one of the six pins that can output PWM
//	val: the desired PWM value (will be limited if out of range)
static inline void pwm_set(uint8_t gpio, int16_t val) {	// the compiler can optimize this down to two instructions if both parameters are known
	if (val < 0) val = 0;
	switch (gpio) {
		case GPIO_PWM1: PWM1 =  (uint8_t)((val > 0x00FF) ? 0x00FF : val); break;
		case GPIO_PWM2: PWM2 =  (uint8_t)((val > 0x00FF) ? 0x00FF : val); break;
		case GPIO_PWM3: PWM3 = (uint16_t)((val > 0x03FF) ? 0x03FF : val); break;
		case GPIO_PWM4: PWM4 = (uint16_t)((val > 0x03FF) ? 0x03FF : val); break;
		case GPIO_PWM5: PWM5 = (uint16_t)((val > 0x03FF) ? 0x03FF : val); break;
		case GPIO_PWM6: PWM6 = (uint16_t)((val > 0x03FF) ? 0x03FF : val); break;
		default: break; // todo: panic
	}
}

// activates the specified PWM output
//	gpio: one of the six pins that can output PWM
static inline void pwm_activate(uint8_t gpio) {
	switch (gpio) {
		case GPIO_PWM1: TCCR0A |= (1 << COM0A1); break;
		case GPIO_PWM2: TCCR0A |= (1 << COM0B1); break;
		case GPIO_PWM3: TCCR1A |= (1 << COM1A1); break;
		case GPIO_PWM4: TCCR1A |= (1 << COM1B1); break;
		case GPIO_PWM5: TIMSK2 |= (1 << TOIE2); break;
		case GPIO_PWM6: TIMSK2 |= (1 << TOIE2); break;
		default: break; // todo: panic
	}
	gpio_output(gpio);
}

#endif
#endif // USING_BUILTIN_PWM

#endif /* PWM_H_ */
