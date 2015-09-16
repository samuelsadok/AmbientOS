/*
 * soft_i2c_master.h
 *
 * Created: 11.03.2014 00:24:54
 *  Author: Samuel
 */ 


#ifndef SOFT_I2C_MASTER_H_
#define SOFT_I2C_MASTER_H_


#define CREATE_SOFT_I2C_PORT(_name, _scl, _sda) \
status_t soft_i2c_transfer_ ## _name(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);

#ifdef SOFT_I2C_PORTS

SOFT_I2C_PORTS;

#endif


// Creates a device that is connected to a software I2C master port.
#define CREATE_SOFT_I2C_DEVICE(_port, _chip, _addrBytes) {			\
	.chip = (_chip),												\
	.addrBytes = (_addrBytes),										\
	.transfer = soft_i2c_transfer_ ## _port,						\
	.transferAsync = NULL											\
}

void soft_i2c_init(void);


#endif /* SOFT_I2C_MASTER_H_ */