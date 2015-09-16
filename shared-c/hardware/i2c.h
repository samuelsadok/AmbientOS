/*
*
* I2C master and slave definitions
*
* created: 13.02.15
*
*/

#ifndef __GLOBAL_I2C_H__
#define __GLOBAL_I2C_H__


#ifdef USING_I2C_MASTER

typedef void(*i2c_callback_t)(uintptr_t context, status_t status);


typedef struct i2c_device_t
{
	uint16_t chip;		// I2C chip address to communicate with.
	size_t addrBytes;	// number of bytes used for register addressing
	uintptr_t context;	// context used by the I2C driver

	// in case rwn = 0, buffer must be considered to be constant!
	status_t(*transfer)(struct i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);
	void(*transferAsync)(struct i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn);
} i2c_device_t;


// Requests data from the slave and blocks until the transfer is complete.
static inline status_t i2c_master_read(i2c_device_t *device, uint32_t addr, char *buffer, size_t count) {
	return device->transfer(device, addr, buffer, count, 1);
}

// Sends data to the slave and blocks until the transfer is complete.
static inline status_t i2c_master_write(i2c_device_t *device, uint32_t addr, const char *buffer, size_t count) {
	return device->transfer(device, addr, (char *)buffer, count, 0);
}

// Requests data from the slave and invokes a callback upon completion or failure.
// Async mode is not supported on all devices.
//	callback: a callback that handles the completion of the transfer
//	context: a context that will be passed to the callback
static inline void i2c_master_read_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context) {
	device->transferAsync(device, addr, buffer, count, callback, context, 1);
}

// Sends data to the slave and invokes a callback upon completion or failure.
// Async mode is not supported on all devices.
//	callback: a callback that handles the completion of the transfer
//	context: a context that will be passed to the callback
static inline void i2c_master_write_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context) {
	device->transferAsync(device, addr, buffer, count, callback, context, 0);
}

#endif


struct endpoint_t; // defined in services.h

typedef struct
{
	struct endpoint_t *endpoint;
	uint32_t addr;
	bool randomAccess;
} i2c_endpoint_t;

// Imports a named endpoint for use as a register (or range of registers) on an I2C slave (i2c_endpoint_t).
//	addr: the register address used to access the endpoint
//	randomAccess: If set to 1, the master can read from anywhere in the endpoint.
//		In this case, caution must be taken to ensure that address ranges of different
//		endpoints don't overlap. An access is always mapped to the endpoint with the next smaller address.
// The connection parameter passed to the endpoint functions is a pointer to the according I2C slave struct.
#define I2C_IMPORT_ENDPOINT(_name, _addr, _randomAccess) {	\
	.endpoint = &_name ## _endpoint,						\
	.addr = (_addr),										\
	.randomAccess = (_randomAccess)							\
}


#endif // __GLOBAL_I2C_H__
