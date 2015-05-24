/*
*
*
* created: 02.03.15
*
*/

#ifndef __AVR_I2C_SLAVE_H__
#define __AVR_I2C_SLAVE_H__


#if defined(USING_BUILTIN_TWIC_SLAVE) || defined(USING_BUILTIN_TWID_SLAVE) || defined(USING_BUILTIN_TWIE_SLAVE) || defined(USING_BUILTIN_TWIF_SLAVE)
#	define USING_BUILTIN_TWI_SLAVE
#endif


#ifdef USING_BUILTIN_TWI_SLAVE

void(*i2cSlaveErrorCallback)(status_t status);

void builtin_i2c_slave_init(void);

#endif


#endif // __AVR_I2C_SLAVE_H__
