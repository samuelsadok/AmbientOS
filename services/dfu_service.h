/*
*
*
* created: 04.03.15
*
*/

#ifndef __DFU_SERVICE_H__
#define __DFU_SERVICE_H__


typedef struct
{
	uint16_t version;	// writing to this field has no effect
	uint16_t state;		// see state descriptions below, write to this field is ignored if domain != 0xFFFF
	uint16_t domain;	// firmware domain (read: number of domains (at least 1), write: 0 selects the local firmware, other values may select slave devices)
} dfu_info_t;


/* checksum method field */

#define CHECKSUM_METHOD_SUM16	(1)	// sum(data) mod 2^16
#define CHECKSUM_METHOD_SUM32	(2) // sum(data) mod 2^32
#define CHECKSUM_METHOD_CRC16	(3) // 16-bit CRC (undefined)
#define CHECKSUM_METHOD_CRC32	(4) // 32-bit CRC, as used in the ATXmega MCU


/* DFU state field */

// read: a normal application is running on the device
// write: reboot the device and launch the application (the device reboots into DFU mode if no valid application is present)
#define DFU_STATE_NONE	(0)

// This state is used when there is an option for the user to force DFU mode (e.g. press the power button for 5s)
// read: the bootloader is temporarily waiting for some condition to start the application or DFU mode
// write: selecting this mode has no effect
#define DFU_STATE_HOLD	(1)

// read: the device is in DFU mode and there is a valid application
// write: if this mode is selected and there is no valid application, the device enters DFU_STATE_PERMA instead
#define DFU_STATE_TEMP	(2)

// read: the device is in DFU mode and there is no valid application
// write: selecting this mode invalidates the application and should be used with caution.
// Once the application is invalidated (either by the DFU master or by starting to write an application),
// it can only be re-enabled after a full application was successfully written and validated.
#define DFU_STATE_PERMA	(3)



EXPORT_ENDPOINT(dfu_info);
EXPORT_ENDPOINT(dfu_platform_length);
EXPORT_ENDPOINT(dfu_platform);
EXPORT_ENDPOINT(dfu_appname_length);
EXPORT_ENDPOINT(dfu_appname);
EXPORT_ENDPOINT(dfu_version_length);
EXPORT_ENDPOINT(dfu_version);
#ifdef USING_BOOTLOADER
EXPORT_ENDPOINT(dfu_progmem_info);
EXPORT_ENDPOINT(dfu_progmem);
#endif




// The progmem endpoint is only available in the bootloader application.
#ifdef USING_BOOTLOADER

#define BLE_DFU_BOOTLOADER_SERVICE ,											\
	BLE_IMPORT_ENDPOINT(dfu_progmem_info, DFU_PROGMEM_INFO),					\
	BLE_IMPORT_ENDPOINT(dfu_progmem, DFU_PROGMEM)

#define I2C_DFU_BOOTLOADER_SERVICE ,											\
	I2C_IMPORT_ENDPOINT(dfu_progmem_info, DFU_PROGMEM_INFO_REG, 0),				\
	I2C_IMPORT_ENDPOINT(dfu_progmem, 0, 1)

#else

#define BLE_DFU_BOOTLOADER_SERVICE
#define I2C_DFU_BOOTLOADER_SERVICE

#endif // USING_BOOTLOADER


#define BLE_DFU_SERVICE															\
	BLE_IMPORT_ENDPOINT(dfu_info, DFU_INFO),									\
	BLE_IMPORT_ENDPOINT(dfu_platform, DFU_PLATFORM),							\
	BLE_IMPORT_ENDPOINT(dfu_appname, DFU_APPNAME),								\
	BLE_IMPORT_ENDPOINT(dfu_version, DFU_VERSION)								\
	BLE_DFU_BOOTLOADER_SERVICE


#define I2C_DFU_SERVICE															\
	I2C_IMPORT_ENDPOINT(dfu_info, DFU_INFO_REG, 0),								\
	I2C_IMPORT_ENDPOINT(dfu_platform_length, DFU_PLATFORM_LENGTH_REG, 0),		\
	I2C_IMPORT_ENDPOINT(dfu_platform, DFU_PLATFORM_REG, 0),						\
	I2C_IMPORT_ENDPOINT(dfu_appname_length, DFU_APPNAME_LENGTH_REG, 0),			\
	I2C_IMPORT_ENDPOINT(dfu_appname, DFU_APPNAME_REG, 0),						\
	I2C_IMPORT_ENDPOINT(dfu_version_length, DFU_VERSION_LENGTH_REG, 0),			\
	I2C_IMPORT_ENDPOINT(dfu_version, DFU_VERSION_REG, 0)						\
	I2C_DFU_BOOTLOADER_SERVICE


#endif // __DFU_SERVICE_H__
