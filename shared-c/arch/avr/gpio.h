/*
*
* Provides a unified interface to access GPIO-pins on AVR MCU's.
*
* created: 15.03.15
*
*/
#ifndef __AVR_GPIO__
#define __AVR_GPIO__


typedef ioport_pin_t gpio_t;


// Configures the specified pin as an output.
//	level: the initial level (0: GND, 1: VCC)
static inline void gpio_config_output(gpio_t pin, bool level) {
	ioport_set_pin_level(pin, (level ? 1 : 0));
	ioport_set_pin_mode(pin, IOPORT_MODE_TOTEM);
	ioport_set_pin_dir(pin, IOPORT_DIR_OUTPUT);
}

// Configures the specified pin as an input.
//	activeHigh: enables the built-in pull-up (0) or pull-down (1) resistor
static inline void gpio_config_input(gpio_t pin, bool activeHigh) {
	ioport_set_pin_mode(pin, (activeHigh ? IOPORT_MODE_PULLDOWN : IOPORT_MODE_PULLUP));
	ioport_set_pin_dir(pin, IOPORT_DIR_INPUT);
}

// Configures the specified pin as a wired-and output.
// This means that a pull-up resistor is used to generate 1.
static inline void gpio_config_wired_and(gpio_t pin, bool level) {
	ioport_set_pin_level(pin, level);
	ioport_set_pin_dir(pin, IOPORT_DIR_OUTPUT);
	ioport_set_pin_mode(pin, IOPORT_MODE_WIREDAND);
}


// Returns the current level on the specified GPIO
static inline bool gpio_get(gpio_t pin) {
	return ioport_get_pin_level(pin);
}

// Sets the output level on the specified GPIO
static inline void gpio_set(gpio_t pin, bool level) {
	ioport_set_pin_level(pin, (level ? 1 : 0));
}


#endif // __AVR_GPIO__
