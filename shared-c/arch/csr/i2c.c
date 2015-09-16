/*
*
* Driver for the built in hardware I2C master controller.
*
* created: 13.02.15
*
*/

#include <system.h>
#include "i2c.h"

#ifdef USING_BUILTIN_I2C_MASTER

#define I2C_HIGH_PERIOD_VAL(baud)		(((F_CPU / 2) / (baud)) + 1)
#define I2C_LOW_PERIOD_VAL(baud)		(((F_CPU / 2) / (baud)) - 5)




// Transfers an I2C packet on the built in hardware I2C port.
//	buffer: a packed buffer (if reading an odd number of bytes, the MSB of the last word is undefined)
//	rwn: 1: read from I2C slave, 0: write to I2C slave
status_t builtin_i2c_master_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn) {
	sys_status status = sys_status_success;

	// initiate transmission
	I2cEnable(TRUE);
	if ((status = I2cRawStart(TRUE)) != sys_status_success)
		goto exit;
	if ((status = I2cRawWriteByte(((uint8_t)device->chip) << 1)) != sys_status_success)
		goto exit;
	if ((status = I2cRawWaitAck(TRUE)) != sys_status_success)
		goto exit;

	// write address
	assert(device->addrBytes <= 4);
	for (int i = device->addrBytes - 1; i >= 0; i--) {
		if ((status = I2cRawWriteByte((addr >> (8 * i)) & 0xFF)) != sys_status_success)
			goto exit;
		if ((status = I2cRawWaitAck(TRUE)) != sys_status_success)
			goto exit;
	}

	// read/write data
	// Note that since the buffer is packed into 16-bit words, we must unpack it while transmitting.
	uint8 buf[2];
	if (rwn) {
		// restart transmission to read
		if ((status = I2cRawRestart(TRUE)) != sys_status_success)
			goto exit;
		if ((status = I2cRawWriteByte((((uint8_t)device->chip) << 1) | 1)) != sys_status_success)
			goto exit;
		if ((status = I2cRawWaitAck(TRUE)) != sys_status_success)
			goto exit;

		// read bytes (and pack into words)
		for (size_t i = 0; i < count; i++) {
			if ((status = I2cRawReadByte(buf + (i & 1))) != sys_status_success)
				goto exit;
			if ((i & 1) || (i == count - 1)) {
				uint8 *ptr = buf;
				*(buffer++) = BufReadUint16(&ptr);
			}
			if ((status = ((i == count - 1) ? I2cRawSendNack(TRUE) : I2cRawSendAck(TRUE))) != sys_status_success)
				goto exit;
		}
	} else {

		// write bytes (unpack from words)
		for (size_t i = 0; i < count; i++) {
			if (!(i & 1)) {
				uint8 *ptr = buf;
				BufWriteUint16(&ptr, *(buffer++));
			}
			if ((status = I2cRawWriteByte(buf[i & 1])) != sys_status_success)
				goto exit;
			if ((status = I2cRawWaitAck(TRUE)) != sys_status_success)
				goto exit;
		}
	}


exit:

	// terminate transmission
	I2cRawStop(TRUE);
	I2cRawTerminate();
	I2cEnable(FALSE);

	return status_convert(status);
}


// Inits and enables the built in hardware I2C module.
void builtin_i2c_init(void) {
	I2cReset();
	//I2cInit(GPIO_I2C_HARD_SDA, GPIO_I2C_HARD_SCL, I2C_POWER_PIO_UNDEFINED, pio_mode_strong_pull_up);
	I2cInit(I2C_RESERVED_PIO, I2C_RESERVED_PIO, I2C_POWER_PIO_UNDEFINED, pio_mode_strong_pull_up);
	I2cConfigClock(I2C_HIGH_PERIOD_VAL(BUILTIN_I2C_MASTER_SPEED), I2C_LOW_PERIOD_VAL(BUILTIN_I2C_MASTER_SPEED));
	I2cEnable(TRUE);
}

#endif




