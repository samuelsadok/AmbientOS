/*
*
*
* created: 13.02.15
*
*/

#ifndef __CSR_BUILTIN_I2C_H__
#define __CSR_BUILTIN_I2C_H__

#include <hardware/i2c.h>

status_t builtin_i2c_master_transfer(i2c_device_t * device, uint32_t addr, char *buffer, size_t count, bool rwn);
void builtin_i2c_init(void);

#define CREATE_BUILTIN_I2C_DEVICE(_chip, _addrBytes) {				\
	.chip = (_chip),												\
	.addrBytes = (_addrBytes),										\
	.transfer = builtin_i2c_master_transfer,						\
	.transferAsync = NULL											\
}


#endif // __CSR_BUILTIN_I2C_H__
