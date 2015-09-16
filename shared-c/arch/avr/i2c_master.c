/*
*
* Enables access to the built in I2C controller(s).
*
* Some adjustments to the ASF are neccessary to enable async operation.
* The ASF already uses interrupt driven transfers, so these changes are minimal.
*	twim.c:		twi_release must (without waiting) invoke the callback,
*				other functions should call twi_release instead of setting transfer.status,
*				twi_master_transfer should set the callback and return immediately
*	twim.h:		change function prototypes to take a callback as an argument
*	twi_common.h: define twi_callback_t
*
*
* Config options:
*	USING_BUILTIN_TWI[...]_MASTER	enables one of the I2C master controllers.
*	BUILTIN_TWI[...]_SPEED			specifies the bus speed when in master mode
*
* created: 27.02.15
*
*/


#include <system.h>
#include "i2c_master.h"


#ifdef __AVR_XMEGA__

// Converts an AVR TWI status code to a system status code
status_t i2c_status_convert(status_code_t status) {
	switch (status) {
		case STATUS_OK:				return STATUS_SUCCESS;
		case ERR_INVALID_ARG:		return STATUS_INVALID_ARGUMENT;
		case ERR_NO_MEMORY:			return STATUS_BUFFER_OVERRUN;
		case ERR_BUSY:				return STATUS_I2C_BUSY;
		case ERR_IO_ERROR:			return STATUS_I2C_PROTOCOL;
		case ERR_PROTOCOL:			return STATUS_I2C_PROTOCOL;
		default:					return STATUS_ERROR;
	}
}

#else

// Converts an AVR TWI status code to a system status code
status_t i2c_status_convert(int status) {
	switch (status) {
		case TWI_SUCCESS:			return STATUS_SUCCESS;
		case TWI_ARBITRATION_LOST:	return STATUS_I2C_BUS_JAMMED;
		case TWI_NO_CHIP_FOUND:		return STATUS_I2C_DEVICE_NOT_RESPONDING;
		case TWI_RECEIVE_OVERRUN:	return STATUS_BUFFER_OVERRUN;
		case TWI_RECEIVE_NACK:		return STATUS_I2C_LINK_BROKEN;
		case TWI_SEND_OVERRUN:		return STATUS_BUFFER_OVERRUN;
		case TWI_SEND_NACK:			return STATUS_I2C_LINK_BROKEN;
		default:					return STATUS_ERROR;
	}
}

#endif



#ifdef USING_BUILTIN_TWI_MASTER



void twi_callback(uintptr_t context, status_t status) {
	*(status_t *)context = status;
}


// Asynchronously transfers data on any of the built in I2C ports.
// This function is inlined to avoid another level of indirection.
static inline void builtin_i2c_master_transfer_async(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn, TWI_t *module, i2c_callback_t callback, uintptr_t context) {
	twi_package_t packet = {
		.addr_length = slave->addrBytes,
		.buffer = buffer,
		.chip = slave->chip,
		.length = count,
		.no_wait = 1
	};

	for (size_t i = 0; i < slave->addrBytes; i++)
		packet.addr[slave->addrBytes - i - 1] = (addr >> (8 * i)) & 0xFF;

	twi_master_transfer(module, &packet, rwn, callback, context);
}


// Synchronously transfers data on any of the built in I2C ports.
static inline status_t builtin_i2c_master_transfer(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn, TWI_t *module) {
	volatile status_t status = STATUS_IN_PROGRESS;
	builtin_i2c_master_transfer_async(slave, addr, buffer, count, rwn, module, twi_callback, (uintptr_t)&status);
	while (status == STATUS_IN_PROGRESS);
	return status;
}



/* TWIC Master */

#ifdef USING_BUILTIN_TWIC_MASTER

status_t builtin_twic_master_transfer(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn) {
	return builtin_i2c_master_transfer(slave, addr, buffer, count, rwn, &TWIC);
}

void builtin_twic_master_transfer_async(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn) {
	builtin_i2c_master_transfer_async(slave, addr, buffer, count, rwn, &TWIC, callback, context);
}

#endif


/* TWID Master */

#ifdef USING_BUILTIN_TWID_MASTER

status_t builtin_twid_master_transfer(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn) {
	return builtin_i2c_master_transfer(slave, addr, buffer, count, rwn, &TWID);
}

void builtin_twid_master_transfer_async(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn) {
	builtin_i2c_master_transfer_async(slave, addr, buffer, count, rwn, &TWID, callback, context);
}

#endif


/* TWIE Master */

#ifdef USING_BUILTIN_TWIE_MASTER

status_t builtin_twie_master_transfer(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn) {
	return builtin_i2c_master_transfer(slave, addr, buffer, count, rwn, &TWIE);
}

void builtin_twie_master_transfer_async(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn) {
	builtin_i2c_master_transfer_async(slave, addr, buffer, count, rwn, &TWIE, callback, context);
}

#endif


/* TWIF Master */

#ifdef USING_BUILTIN_TWIF_MASTER

status_t builtin_twif_master_transfer(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, bool rwn) {
	return builtin_i2c_master_transfer(slave, addr, buffer, count, rwn, &TWIF);
}

void builtin_twif_master_transfer_async(i2c_device_t *slave, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn) {
	builtin_i2c_master_transfer_async(slave, addr, buffer, count, rwn, &TWIF, callback, context);
}

#endif



// Inits a built in I2C controller in master mode
//	speed: the bus clock speed (in Hz)
//			speed range @ 32MHz clock: 62kHz - 400kHz or 1MHz (fast mode)
void builtin_i2c_master_init_ex(TWI_t *module, unsigned long speed) {
	sysclk_enable_peripheral_clock(module);

	twi_options_t options = {
		.speed = speed, // unused
		.chip = 0x00, // unused
		.speed_reg = TWI_BAUD(sysclk_get_cpu_hz(), speed)
	};

	if (speed > 400000UL)
		twi_fast_mode_enable(module);
	twi_master_init(module, &options);
	twi_master_enable(module);
}


// Inits all built in I2C master controllers that are used by the application.
void builtin_i2c_master_init(void) {
#ifdef USING_BUILTIN_TWIC_MASTER
	builtin_i2c_master_init_ex(&TWIC, BUILTIN_TWIC_SPEED);
#endif
#ifdef USING_BUILTIN_TWID_MASTER
	builtin_i2c_master_init_ex(&TWID, BUILTIN_TWID_SPEED);
#endif
#ifdef USING_BUILTIN_TWIE_MASTER
	builtin_i2c_master_init_ex(&TWIE, BUILTIN_TWIE_SPEED);
#endif
#ifdef USING_BUILTIN_TWIF_MASTER
	builtin_i2c_master_init_ex(&TWIF, BUILTIN_TWIF_SPEED);
#endif
}


#endif // USING_BUILTIN_TWI_MASTER

