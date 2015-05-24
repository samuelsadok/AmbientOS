/*******************************************************************************
 *  Copyright (C) Cambridge Silicon Radio Limited 2013
 *
 *  FILE
 *      service_csr_ota.h
 *
 *  DESCRIPTION
 *      Header definitions for CSR OTA-update library
 *
 ******************************************************************************/

#ifndef _CSR_OTA_UUIDS_H
#define _CSR_OTA_UUIDS_H

/*=============================================================================*
 *  Public Definitions
 *============================================================================*/


// *****************************************************************************
// This is the UUID for the CSR Over-the-Air Application Update Service 

#define CSR_OTA_UPDATE_SERV_UUID_WORD1      0x0000
#define CSR_OTA_UPDATE_SERV_UUID_WORD2      0x1016
#define CSR_OTA_UPDATE_SERV_UUID_WORD3      0xd102
#define CSR_OTA_UPDATE_SERV_UUID_WORD4      0x11e1
#define CSR_OTA_UPDATE_SERV_UUID_WORD5      0x9b23
#define CSR_OTA_UPDATE_SERV_UUID_WORD6      0x0002
#define CSR_OTA_UPDATE_SERV_UUID_WORD7      0x5b00
#define CSR_OTA_UPDATE_SERV_UUID_WORD8      0xa5a5

#define CSR_OTA_UPDATE_SERVICE_UUID    0x00001016d10211e19b2300025b00a5a5


// *****************************************************************************
// This is the UUID for the Current Application Characteristic 

#define CSR_OTA_CURRENT_APP_UUID_WORD1      0x0000
#define CSR_OTA_CURRENT_APP_UUID_WORD2      0x1013
#define CSR_OTA_CURRENT_APP_UUID_WORD3      0xd102
#define CSR_OTA_CURRENT_APP_UUID_WORD4      0x11e1
#define CSR_OTA_CURRENT_APP_UUID_WORD5      0x9b23
#define CSR_OTA_CURRENT_APP_UUID_WORD6      0x0002
#define CSR_OTA_CURRENT_APP_UUID_WORD7      0x5b00
#define CSR_OTA_CURRENT_APP_UUID_WORD8      0xa5a5

#define CSR_OTA_CURRENT_APP_UUID        0x00001013d10211e19b2300025b00a5a5


// *****************************************************************************
// This is the UUID for the Read CS-key Characteristic 

#define CSR_OTA_READ_CS_KEY_UUID_WORD1      0x0000
#define CSR_OTA_READ_CS_KEY_UUID_WORD2      0x1017
#define CSR_OTA_READ_CS_KEY_UUID_WORD3      0xd102
#define CSR_OTA_READ_CS_KEY_UUID_WORD4      0x11e1
#define CSR_OTA_READ_CS_KEY_UUID_WORD5      0x9b23
#define CSR_OTA_READ_CS_KEY_UUID_WORD6      0x0002
#define CSR_OTA_READ_CS_KEY_UUID_WORD7      0x5b00
#define CSR_OTA_READ_CS_KEY_UUID_WORD8      0xa5a5

#define CSR_OTA_READ_CS_KEY_UUID        0x00001017d10211e19b2300025b00a5a5


// *****************************************************************************
// This is the UUID for the Data Transfer Characteristic 
#define CSR_OTA_DATA_TRANSF_UUID_WORD1      0x0000
#define CSR_OTA_DATA_TRANSF_UUID_WORD2      0x1014
#define CSR_OTA_DATA_TRANSF_UUID_WORD3      0xd102
#define CSR_OTA_DATA_TRANSF_UUID_WORD4      0x11e1
#define CSR_OTA_DATA_TRANSF_UUID_WORD5      0x9b23
#define CSR_OTA_DATA_TRANSF_UUID_WORD6      0x0002
#define CSR_OTA_DATA_TRANSF_UUID_WORD7      0x5b00
#define CSR_OTA_DATA_TRANSF_UUID_WORD8      0xa5a5

#define CSR_OTA_DATA_TRANSF_UUID        0x00001014d10211e19b2300025b00a5a5

#endif /* _CSR_OTA_UUIDS_H */
