/*
*
* Manages power states in a simple setup that involves a tactile power button and a
* MOSFET that acts as a power switch so that power can be removed by software.
*
* Config options:
*	USING_POWER_MGR				enable power management features
*	POWER_SWITCH_PIN			the pin to which the power switch is connected
*	POWER_SWITCH_ACTICE_HIGH	should be 1 if the power switch is active high
*	POWER_CONTROL_PIN			the pin that is connected to the power MOSFET
*	POWER_CONTROL_ACTICE_HIGH	should be 1 if the control output is active high
*	POWER_WAKE_PIN				an output pin that can wake up peripherals on the device
*	POWER_WAKE_ACTICE_HIGH		should be 1 if the wake signal is active high
*
* created: 15.03.15
*
*/

#include <system.h>

#ifdef USING_POWER_MGR


// Returns 1 if the power button is pressed (i.e. immediately after power-on).
bool pwr_switch_pressed(void) {
	return gpio_get(POWER_SWITCH_PIN) ^ (!(POWER_SWITCH_ACTIVE_HIGH));
}


// Asserts the WAKE-output for 1ms
void pwr_wake(void) {
	gpio_set(POWER_WAKE_PIN, POWER_WAKE_ACTIVE_HIGH);
	_delay_ms(1);
	gpio_set(POWER_WAKE_PIN, !POWER_WAKE_ACTIVE_HIGH);
}


// Shuts down the entire system down by opening the main power supply MOSFET.
// This routine does not return.
void __attribute__((__noreturn__)) pwr_shutdown(void) {
	// todo: invoke shutdown callbacks

	interrupts_off();

	// we must not pull down the power control pin while the power switch is pressed (this would be a short circuit)
	while (pwr_switch_pressed());

	// discharge power mosfet gate capacitor
	gpio_set(POWER_CONTROL_PIN, !POWER_CONTROL_ACTIVE_HIGH);
	for (;;);
}


// Invoked when the power switch is pressed
void pwr_switch_callback(uintptr_t context) {
	pwr_shutdown();
}


// Initializes power management. If external interrupt handling is not enabled (USING_EXT_INT),
// the application must invoke pwr_shutdown() manually as soon as the power button is pressed.
void pwr_init(void) {
	// As soon as the user releases the power button, the gate capacitor of
	// the power control MOSFET starts discharging.
	// We can keep it on by setting PWR_CTRL to high.
	gpio_config_output(POWER_WAKE_PIN, !POWER_WAKE_ACTIVE_HIGH);
	gpio_config_output(POWER_CONTROL_PIN, POWER_CONTROL_ACTIVE_HIGH);
	gpio_config_input(POWER_SWITCH_PIN, POWER_SWITCH_ACTIVE_HIGH);

#ifdef USING_EXT_INT
	interrupt_register(POWER_SWITCH_PIN, POWER_SWITCH_ACTIVE_HIGH, pwr_switch_callback, 0);
#endif

	pwr_wake();
}


#endif
