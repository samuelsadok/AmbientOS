/*******************************************************************************
 *  Copyright (C) Cambridge Silicon Radio Limited 2013
 *
 *  FILE
 *      csr_ota.h
 *
 *  DESCRIPTION
 *      Header definitions for CSR OTA-update library
 *
 ******************************************************************************/

#ifndef _CSR_OTA_H
#define _CSR_OTA_H

/*=============================================================================*
 *  SDK Header Files
 *============================================================================*/
#include <bt_event_types.h>
#include <status.h>

/*=============================================================================*
 *  Public Data Types
 *============================================================================*/

/* This error is returned when the Central device tries to obtain the value of a 
 * CS-key that cannot be read.
 */
#define CSR_OTA_KEY_NOT_READ    0x80

typedef enum {
    CSR_OTA_BOOT_LOADER   = 0x0,  /* Identifies the CSR OTA-update application */
    CSR_DEVICE_APP_1      = 0x1,  /* Identifies application 1 on this device */
    CSR_DEVICE_GOLDEN_APP = CSR_DEVICE_APP_1,
    CSR_DEVICE_APP_2      = 0x2,  /* Identifies application 2 on this device */
    
    CSR_APP_UNKNOWN       = 0xff  /* Unknown application - used as a return code only */
} CSR_APPLICATION_ID;

/*=============================================================================*
 *  Public Function Prototypes
 *============================================================================*/

/* Set the current application. 
 *
 * This function is called when the Central device writes to the
 * OTA_CURRENT_APP characteristic handle.
 *
 * Primarily, this function is used to switch the device from the current
 * application into OTA-update mode.
 * It is possible to switch directly from one application to another; in this
 * case, it is not required to supply the Central device information and this
 * can be set to NULL.
 *
 * Parameters:
 * - setCurrentApp: 
 *          This is the value written by the Central device.
 * - isBonded: 
 *          Indicates whether this application is bonded with the Central device.
 * - centralAddress: 
 *          The address of the Central device from which the update will be 
 *          received. If this information is not available, pass NULL.
 * - diversifier: 
 *          The diversifier agreed with the bonded Central device. This will be 
 *          0 (zero) if the Central device does not have a random-resolvable 
 *          address.
 * - localRandomAddress: 
 *          If this device is using a random address, then pass the address here. 
 *          The same address will then be used by the boot-loader.
 *          If this device is not using a random address, then pass NULL.
 * - irk: 
 *          The IRK shared with the Central device from which the update will be
 *          received. If this information is not available, set all fields 
 *          to 0 (zero).
 * - serviceChangedConfig: 
 *          Indicates whether the remote device has requested indications on 
 *          the Service Changed characteristic.
 *
 * Return value:
 *  sys_status: indicates whether the write request was processed.
 *
 * NOTE: this function claims the I2C bus for the duration of the call.
 * Applications using the I2C bus may need to re-initialise the bus after
 * calling this function.
 */
extern sys_status OtaWriteCurrentApp(CSR_APPLICATION_ID setCurrentApp,
                                     bool               isBonded,
                                     TYPED_BD_ADDR_T   *centralAddress,
                                     uint16             diversifier,
                                     BD_ADDR_T         *localRandomAddress,
                                     uint16            *irk,
                                     bool               serviceChangedConfig);

/* Read the current application.
 *
 * This function is called when the Central device reads the OTA_CURRENT_APP 
 * characteristic handle. It allows the Central device to read the index number 
 * of the current application (which will be this application).
 *
 * Return value:
 *  The index number of the current application.
 *
 * NOTE: this function claims the I2C bus for the duration of the call.
 * Applications using the I2C bus may need to re-initialise the bus after
 * calling this function.
 */
extern CSR_APPLICATION_ID OtaReadCurrentApp(void);

/* Reset the device.
 * 
 * Typically, this function is called after a call to OtaWriteCurrentApp(), but
 * allowing the application time to disconnect cleanly from the Central device.
 *
 * Note: this function does not return. 
 */
extern void OtaReset(void);
#define ota_reset()	OtaReset()

#endif /* _CSR_OTA_H */