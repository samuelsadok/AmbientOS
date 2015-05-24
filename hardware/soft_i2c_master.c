/*
 * i2c_master.c
 *
 * Created: 08.03.2014 08:04:53
 *  Author: Samuel
 */ 

#include <system.h>
#include "soft_i2c_master.h"

#ifdef USING_SOFT_I2C_MASTER


typedef struct
{
	gpio_t SCL;
	gpio_t SDA;
} soft_i2c_master_t;


// Waits for T/2
void soft_i2c_delay(void) {
	__asm volatile ("nop\n nop\n nop\n nop\n"
					"nop\n nop\n nop\n nop\n"
					"nop\n nop\n nop\n nop\n"
					"nop\n nop\n nop\n nop\n" : : : "memory");
}



// Writes a byte of data to the I2C port.
// Returns 0 if the slave acknowleged the transmission.
bool soft_i2c_write(soft_i2c_master_t *port, const char data) {
	uint8_t b = data;
	for (int i = 8; i; i--) {
		gpio_set(port->SCL, 0);
		gpio_set(port->SDA, b & 0x80);
		soft_i2c_delay();
		gpio_set(port->SCL, 1);
		soft_i2c_delay();
		b <<= 1;
	}
	
	gpio_set(port->SCL, 0);
	gpio_set(port->SDA, 1);
	soft_i2c_delay();
	gpio_set(port->SCL, 1);
	
	while (!gpio_get(port->SCL)); // allow slave to stretch clock
	
	bool nack = gpio_get(port->SDA);
	soft_i2c_delay();
	return nack;
}


// Reads a byte of data from the I2C slave.
//	nack: should be 1 if this is the last byte to be read
char soft_i2c_read(soft_i2c_master_t *port, bool nack) {
	unsigned char inByte = 0;
	
	for (int i = 8; i; i--) {
		gpio_set(port->SCL, 0);
		gpio_set(port->SDA, 1);
		soft_i2c_delay();
		gpio_set(port->SCL, 1);
		soft_i2c_delay();
		
		while (!gpio_get(port->SCL)); // allow slave to stretch clock
		
		inByte = (inByte << 1) | gpio_get(port->SDA);
	}
	
	gpio_set(port->SCL, 0);
	gpio_set(port->SDA, nack);
	soft_i2c_delay();
	gpio_set(port->SCL, 1);
	while (!gpio_get(port->SCL));
	soft_i2c_delay();

	return inByte;
}


// Generates a STOP condition
void soft_i2c_stop(soft_i2c_master_t *port) {
	gpio_set(port->SCL, 0);
	gpio_set(port->SDA, 0);
	soft_i2c_delay();
	gpio_set(port->SCL, 1);
	soft_i2c_delay();
	gpio_set(port->SDA, 1);
	soft_i2c_delay();
}


// Generates a START condition and sends the slave address (including the read/write bit).
// Returns 0 if the slave acknowledges.
bool soft_i2c_start(soft_i2c_master_t *port, const char addr) {
	gpio_set(port->SDA, 0);
	soft_i2c_delay();
	return soft_i2c_write(port, addr);
}


// Generates a repeated START condition and sends the slave address (including the read/write bit).
// Returns 0 if the slave acknowledges.
bool soft_i2c_restart(soft_i2c_master_t *port, const char addr) {
	gpio_set(port->SCL, 0);
	soft_i2c_delay();
	gpio_set(port->SDA, 1);
	soft_i2c_delay();
	gpio_set(port->SCL, 1);
	soft_i2c_delay();
	return soft_i2c_start(port, addr);
}


status_t soft_i2c_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn, soft_i2c_master_t *port) {
	status_t status = STATUS_I2C_DEVICE_NOT_RESPONDING;

	if (soft_i2c_start(port, ((uint8_t)device->chip) << 1))
		goto exit;
	
	status = STATUS_I2C_LINK_BROKEN;

	// send register address
	for (size_t i = device->addrBytes - 1; i >= 0; i--)
		if (soft_i2c_write(port, (addr >> (i * 8)) & 0xFF))
			goto exit;

	if (rwn) {
		// restart transmission and read into buffer
		if (soft_i2c_restart(port, (((uint8_t)device->chip) << 1) | 1))
			goto exit;
		while (count--)
			*(buffer++) = soft_i2c_read(port, !count);

	} else {
		// write buffer
		while (count--)
			if (soft_i2c_write(port, *(buffer++)))
				goto exit;
	}

	status = STATUS_SUCCESS;

exit:
	soft_i2c_stop(port);
	return status;
}



#undef CREATE_SOFT_I2C_PORT
#define CREATE_SOFT_I2C_PORT(_name, _scl, _sda)		\
soft_i2c_master_t soft_i2c_master_ ## _name = {		\
	.SCL = (_scl),									\
	.SDA = (_sda)									\
};													\
status_t soft_i2c_transfer_ ## _name(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn) {	\
	return soft_i2c_transfer(device, addr, buffer, count, rwn, &soft_i2c_master_ ## _name);							\
}


SOFT_I2C_PORTS;


#undef CREATE_SOFT_I2C_PORT
#define CREATE_SOFT_I2C_PORT(_name, _scl, _sda)		\
	gpio_config_wired_and(_scl, 1);					\
	gpio_config_wired_and(_sda, 1);


// Initializes all software I2C master ports
void soft_i2c_init(void) {
	SOFT_I2C_PORTS;
}


#endif
