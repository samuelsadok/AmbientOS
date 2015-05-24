/*
*
*
* created: 13.02.15
*
*/

#include <system.h>

#ifdef USING_BUILTIN_TEMPERATURE

// Returns the chip's current temperature in °C (updated every 15s)
float builtin_temperature_read(void) {
	return ThermometerReadTemperature();
}

#endif // USING_BUILTIN_TEMPERATURE
