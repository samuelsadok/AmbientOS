/*
*
* Provides a bluetooth central with direct access to the I2C port.
*
* created: 21.02.15
*
*/


#include <system.h>

#ifdef USING_I2C_SERVICE

typedef struct __attribute__((__packed__)) {
	uint32_t chip;
	uint32_t addrBytes;
	uint32_t addr;
} i2c_context_t;
i2c_context_t i2cContext;


/*
 * I2C setup characteristic
 *	access to the setup structure that defines the chip and register address for the I2C transfer
 */

status_t i2c_setup_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	if (offset)
		return STATUS_ERROR;
	memcpy(buf, &i2cContext, (*count = min(sizeof(i2c_context_t) * WORDSIZE, *count)));
	return STATUS_SUCCESS;
}

status_t i2c_setup_handler_w(void *connection, size_t offset, char *buf, size_t count) {
	if (offset)
		return STATUS_ERROR;
	memcpy(&i2cContext, buf, min(sizeof(i2c_context_t) * WORDSIZE, count));
	if (i2cContext.addrBytes > sizeof(int) * WORDSIZE) {
		i2cContext.addrBytes = sizeof(int) * WORDSIZE;
		return STATUS_INVALID_ARGUMENT;
	}
	return STATUS_SUCCESS;
}


/*
 * I2C transfer characteristic
 *	reading and writing to this characteristic maps directly to reads or writes on the I2C port.
 */

status_t i2c_transfer_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	i2c_device_t device = {
		.chip = i2cContext.chip,
		.addrBytes = i2cContext.addrBytes,
		.transfer = builtin_i2c_master_transfer
	};
	return i2c_master_read(&device, i2cContext.addr + offset / WORDSIZE, buf, *count);
}

status_t i2c_transfer_handler_w(void *connection, size_t offset, char *buf, size_t count) {
	i2c_device_t device = {
		.chip = i2cContext.chip,
		.addrBytes = i2cContext.addrBytes,
		.transfer = builtin_i2c_master_transfer
	};
	return i2c_master_write(&device, i2cContext.addr + offset / WORDSIZE, buf, count);
}


DEFINE_ENDPOINT(i2c_control, i2c_setup_handler_r, i2c_setup_handler_w, NULL);
DEFINE_ENDPOINT(i2c_data, i2c_transfer_handler_r, i2c_transfer_handler_w, NULL);


#endif // USING_I2C_SERVICE
