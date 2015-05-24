/*******************************************************************************
 *  Copyright (C) Cambridge Silicon Radio Limited 2013
 *
 *  FILE
 *      service_csr_ota.h
 *
 *  DESCRIPTION
 *      Header definitions for CSR OTA-update service
 *
 ******************************************************************************/

#ifndef _CSR_OTA_SERVICE_H
#define _CSR_OTA_SERVICE_H

/*=============================================================================*
 *  SDK Header Files
 *============================================================================*/
#include <types.h>
#include <bt_event_types.h>

/*============================================================================
 *  Public Data Declarations
 *============================================================================*/
/* Indicates whether the OTA module requires the device to reset on client
 * disconnection.
 */
extern bool otaResetRequired;

/*=============================================================================*
 *  Public Function Prototypes
 *============================================================================*/

/* Handler for a READ action from the Central */
extern void ota_read_handler(GATT_ACCESS_IND_T *pInd);

/* Handler for a WRITE action from the Central */
extern void ota_write_handler(GATT_ACCESS_IND_T *pInd);

#endif /* _CSR_OTA_SERVICE_H */