/*
*
*
* created: 13.02.15
*
*/

#include <system.h>

#ifdef USING_BUILTIN_VOLTAGE

// Returns the chip's current voltage in mV
float builtin_voltage_read(void) {
	return BatteryReadVoltage();
}

#endif // USING_BUILTIN_VOLTAGE
