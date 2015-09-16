/*
 * i2c_eeprom.c
 *
 * Created: 07.03.2014
 *  Author: Samuel
 */ 


#include <system.h>
#include "i2c_eeprom.h"


#ifdef USING_I2C_EEPROM

#ifndef USING_I2C_MASTER
#  error "I2C master features must be enabled to use I2C EEPROM"
#endif


// Writes a buffer to the EEPROM.
// The transfer is partitioned according to the device's write page size
status_t i2c_eeprom_write(i2c_device_t *eeprom, uint32_t address, const char *buffer, uint32_t count) {
	status_t status;

	while (count) {
		// make sure we restart the transmission at every page boundary
		size_t bytes = min(EEPROM_PAGESIZE - (address % EEPROM_PAGESIZE), count);

		if ((status = i2c_master_write(eeprom, address, buffer, bytes)))
			return status;

		address += bytes;
		buffer += bytes;
		count -= bytes;

		if (count)
			_delay_ms(EEPROM_WRITE_TIME);
	}

	return STATUS_SUCCESS;
}

/*
// Reads data from the EEPROM into a buffer.
status_t i2c_eeprom_read(eeprom, uint32_t address, char *buffer, uint32_t count) {
	return i2c_master_read(eeprom, address, buffer, count);;
}
*/

#endif



