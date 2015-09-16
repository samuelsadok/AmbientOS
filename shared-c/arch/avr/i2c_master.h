/*
*
*
* created: 27.02.15
*
*/

#ifndef __AVR_I2C_MASTER_H__
#define __AVR_I2C_MASTER_H__

#include <hardware/i2c.h>

#if defined(USING_BUILTIN_TWIC_MASTER) || defined(USING_BUILTIN_TWID_MASTER) || defined(USING_BUILTIN_TWIE_MASTER) || defined(USING_BUILTIN_TWIF_MASTER)
#  define USING_BUILTIN_TWI_MASTER
#  ifndef USING_I2C_MASTER
#    error "you must also enable the I2C_MASTER feature to use builtin TWI master controller"
#  endif
#endif


#define CREATE_TWI_DEVICE(_twi, _chip, _addrBytes) {				\
	.chip = (_chip),												\
	.addrBytes = (_addrBytes),										\
	.transfer = builtin_ ## _twi ## _master_transfer,				\
	.transferAsync = builtin_ ## _twi ## _master_transfer_async,	\
}

#ifdef USING_BUILTIN_TWIC_MASTER
status_t builtin_twic_master_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);
void builtin_twic_master_transfer_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn);
#define CREATE_TWIC_DEVICE(_chip, _addrBytes)	CREATE_TWI_DEVICE(twic, _chip, _addrBytes)
#endif

#ifdef USING_BUILTIN_TWID_MASTER
status_t builtin_twid_master_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);
void builtin_twid_master_transfer_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn);
#define CREATE_TWID_DEVICE(_chip, _addrBytes)	CREATE_TWI_DEVICE(twid, _chip, _addrBytes)
#endif

#ifdef USING_BUILTIN_TWIE_MASTER
status_t builtin_twie_master_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);
void builtin_twie_master_transfer_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn);
#define CREATE_TWIE_DEVICE(_chip, _addrBytes)	CREATE_TWI_DEVICE(twie, _chip, _addrBytes)
#endif

#ifdef USING_BUILTIN_TWIF_MASTER
status_t builtin_twif_master_transfer(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, bool rwn);
void builtin_twif_master_transfer_async(i2c_device_t *device, uint32_t addr, char *buffer, size_t count, i2c_callback_t callback, uintptr_t context, bool rwn);
#define CREATE_TWIF_DEVICE(_chip, _addrBytes)	CREATE_TWI_DEVICE(twif, _chip, _addrBytes)
#endif


#ifdef USING_BUILTIN_TWI_MASTER

void builtin_i2c_master_init(void);

#endif


#endif // __AVR_I2C_MASTER_H__
