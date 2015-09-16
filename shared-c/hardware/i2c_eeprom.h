/*
 *
 *
 * created: 08.03.14
 *
 */ 


#ifndef __I2C_EEPROM_H__
#define __I2C_EEPROM_H__


status_t i2c_eeprom_write(i2c_device_t *eeprom, uint32_t address, const char *buffer, uint32_t count);
#define i2c_eeprom_read i2c_master_read


#endif // __I2C_EEPROM_H__