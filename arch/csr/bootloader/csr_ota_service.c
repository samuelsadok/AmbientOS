/*******************************************************************************
 *  Copyright (C) Cambridge Silicon Radio Limited 2013
 *
 *  FILE
 *      csr_ota_service.c
 *
 *  DESCRIPTION
 *      Example implementation of the CSR OTA-update service
 *
 * ** Items labelled CUSTOMISATION may require adjustment for some applications **
 *
 ******************************************************************************/
 
/*=============================================================================*
 *  SDK Header Files
 *============================================================================*/
// #include <gatt.h>
// #include <buf_utils.h>
// #include <config_store.h>
// #include <nvm.h>
// #include <ble_hci_test.h>

/*=============================================================================*
 *  Local Header Files
 *============================================================================*/
#include "customisation.h"

#if defined(USE_STATIC_RANDOM_ADDRESS) || defined(USE_RESOLVABLE_RANDOM_ADDRESS)
#include <gap_app_if.h>
#endif /* USE_STATIC_RANDOM_ADDRESS || USE_RESOLVABLE_RANDOM_ADDRESS */

// #include "gatt_service.h"
// #include "csr_ota_service.h"
// #include "csr_ota_uuids.h"
// #include "csr_ota.h"

/*============================================================================
 *  Public Data Declarations
 *============================================================================*/
/* Indicates whether the OTA module requires the device to reset on client
 * disconnection.
 */
bool otaResetRequired = FALSE;

/*=============================================================================*
 *  Local Data Declarations
 *============================================================================*/

/* The current value of the DATA TRANSFER characteristic */
static uint8 dataTransferMemory[MAX_DATA_LENGTH] = {0};

/* The number of bytes of valid data in dataTransferMemory */
static uint8 dataTransferDataLength = 0;

/* The current configuration of the DATA TRANSFER characteristic */
static uint8 dataTransferConfiguration[2] = {gatt_client_config_none, 0};

/*=============================================================================*
 *  Private Function Implementations
 *============================================================================*/

/*-----------------------------------------------------------------------------*
 * Read a CS-key value.
 *
 * This function is called when the Central device requests the value of a 
 * CS-key, by writing to the OTA_READ_CS_KEY handle. This function is supported
 * only if the application supports the OTA_READ_CS_KEY characteristic.
 *
 * Parameters:
 * - keyIndex: the index of the CS-key to be read. This is the value written by
 *             the Central device to the Characteristic.
 * - data: if the read request is successful, then this is a pointer to the data
 *             to be sent to the Central device.
 * - dataLenInBytes: if the read request is successful, then this is the length
 *             of the data to be sent to the Central device.
 *
 * Return value:
 *  sys_status_success: the read was successful and the data parameter contains
 *                      valid information.
 *  CSR_OTA_KEY_NOT_READ: the read was unsuccessful and the data parameter does
 *                      not contain valid information.
 *----------------------------------------------------------------------------*/
static sys_status OtaReadCsKey(uint8 keyIndex, uint8 *data, uint8 *dataLenInBytes)
{
    sys_status rc;
    uint16 byte;
    BD_ADDR_T *packedAddr;
    
    switch(keyIndex)
    {
        case 1: /* bdaddr */
            /* Rather than allocating yet more memory, we use here the knowledge
             * that "data" is actually "dataTransferMemory" and so is much bigger
             * than we need.
             * Read the packed address into the far end of the array, then unpack
             * it into the array start.
             */
            packedAddr = (BD_ADDR_T*)&data[MAX_DATA_LENGTH - sizeof(BD_ADDR_T) - 1];
            
            if(CSReadBdaddr(packedAddr))
            {
                byte = 0;
                data[byte++] = HIGH_BYTE(packedAddr->nap);
                data[byte++] = LOW_BYTE(packedAddr->nap);
                
                data[byte++] = LOW_BYTE(packedAddr->uap);
                
                data[byte++] = THIRD_BYTE(packedAddr->lap);
                data[byte++] = HIGH_BYTE(packedAddr->lap);
                data[byte++] = LOW_BYTE(packedAddr->lap);
                
                *dataLenInBytes = byte;
                
                rc = sys_status_success;
            }
            else
            {
                rc = CSR_OTA_KEY_NOT_READ;
            }
            break;
            
        case 2:  /* crystal_ftrim */
            *data = TestGetXtalTrim();
            *dataLenInBytes = 1;
            rc = sys_status_success;
            break;
            
        case 3:  /* radio_rx_adc_config */
        case 4:  /* user_keys */
        case 5:  /* uart_rate */
        case 6:  /* uart_config */
            rc = CSR_OTA_KEY_NOT_READ;
            break;
            
        case 7: /* tx_power_level */
            *data = (uint8)CSReadTxPower();
            *dataLenInBytes = 1;
            rc = sys_status_success;
            break;
            
        case 8:  /* err_report_fault */
        case 9:  /* err_panic */
        case 10: /* wd_timeout */
        case 11: /* wd_period */
        case 12: /* sleep_mode */
        case 13: /* slave_clock_accuracy */
        case 14: /* debug_sleep_clock */
        case 15: /* debug_radio_rx */
        case 16: /* debug_radio_tx */
        case 17: /* identity_root */
        case 18: /* encryption_root */
        case 19: /* xtal32k_level */
        case 20: /* xtal16m_level */
        case 21: /* xtal16m_wakeup */
        case 22: /* battery_threshold */
        case 23: /* nvm_start_address */
            rc = CSR_OTA_KEY_NOT_READ;
            break;
            
        case 24: /* nvm_size */
            if(NvmSize((uint16*)data) == sys_status_success)
            {
                /* Unpack the value */
                data[1] = HIGH_BYTE(data[0]);
                data[0] = LOW_BYTE(data[0]);
                
                *dataLenInBytes = 2;
                rc = sys_status_success;
            }
            else
            {
                rc = CSR_OTA_KEY_NOT_READ;
            }
            break;            
            
        case 25: /* spi_flash_block_size */
        case 26: /* nvm_num_spi_blocks */
        case 27: /* i2c_eeprom_init_time */
        case 28: /* spi_flash_init_time */
        case 29: /* temperature_adjust */
        case 30: /* radio_data_win_min */
        case 31: /* radio_data_win_max */
        case 32: /* radio_sleep_win_min */
        case 33: /* radio_sleep_win_max */
        case 34: /* i2c_disabled_pull_mode */
        default:
            rc = CSR_OTA_KEY_NOT_READ;
            break;
    }
    
    return rc;
}

/*=============================================================================*
 *  Public Function Implementations
 *============================================================================*/

/*-----------------------------------------------------------------------------*
 *  NAME
 *      OtaHandleAccessRead
 *
 *  DESCRIPTION
 *      Handle a read-request from the Central device where the characteristic
 *      handle falls within the range of the OTA-update service.
 *----------------------------------------------------------------------------*/
extern void ota_read_handler(GATT_ACCESS_IND_T *pInd)
{
    sys_status rc;
    uint8 *pValue = NULL;
    uint8 dataLenInBytes = 0;
    
    CSR_APPLICATION_ID currentApp;
    
    switch(pInd->handle)
    {
        case HANDLE_OTA_CURRENT_APP:
            /* Read the index of the current application */
            currentApp = OtaReadCurrentApp();
            
            pValue = (uint8*)&currentApp;
            dataLenInBytes = 1;
            rc = sys_status_success;
            break;
            
        case HANDLE_OTA_DATA_TRANSFER:
            /* Read the value of the data transfer characteristic */
            pValue = (uint8*)dataTransferMemory;
            dataLenInBytes = dataTransferDataLength;
            rc = sys_status_success;
            break;
            
        case HANDLE_OTA_DATA_TRANSFER_CLIENT_CONFIG:
            pValue = (uint8*)dataTransferConfiguration;
            dataLenInBytes = 2;
            rc = sys_status_success;
            break;
            
        default:
            /* Reading is not supported on this handle */
            rc = gatt_status_read_not_permitted;
            break;
    }
    
    GattAccessRsp(pInd->cid, pInd->handle, rc, dataLenInBytes, pValue);
}

/*-----------------------------------------------------------------------------*
 *  NAME
 *      OtaHandleAccessWrite
 *
 *  DESCRIPTION
 *      Handle a read-request from the Central device where the characteristic
 *      handle falls within the range of the OTA-update service.
 *----------------------------------------------------------------------------*/
extern void ota_write_handler(GATT_ACCESS_IND_T *pInd)
{
    sys_status rc = gatt_status_write_not_permitted;
    uint8 resetVal = 0x0;
    uint16 client_config;
    
#if defined(USE_STATIC_RANDOM_ADDRESS) || defined(USE_RESOLVABLE_RANDOM_ADDRESS)
    BD_ADDR_T bd_addr;
#endif /* USE_STATIC_RANDOM_ADDRESS || USE_RESOLVABLE_RANDOM_ADDRESS */
    
    switch(pInd->handle)
    {
        case HANDLE_OTA_CURRENT_APP:
            resetVal = pInd->value[0];
            
#if defined(USE_STATIC_RANDOM_ADDRESS) || defined(USE_RESOLVABLE_RANDOM_ADDRESS)
            (void)GapGetRandomAddress(&bd_addr);
#endif /* USE_STATIC_RANDOM_ADDRESS || USE_RESOLVABLE_RANDOM_ADDRESS */
            
            if(OtaWriteCurrentApp((CSR_APPLICATION_ID) resetVal,
                                  IS_BONDED,
                                  &(CONN_CENTRAL_ADDR),
                                  LINK_DIVERSIFIER,
#if defined(USE_STATIC_RANDOM_ADDRESS) || defined(USE_RESOLVABLE_RANDOM_ADDRESS)
                                  &bd_addr,
#else
                                  NULL,
#endif /* USE_STATIC_RANDOM_ADDRESS || USE_RESOLVABLE_RANDOM_ADDRESS */
                                  CONNECTION_IRK,
                                  GattServiceChangedIndActive()) == sys_status_success)
            {
                rc = sys_status_success;
            }
            else
            {
                rc = gatt_status_invalid_param_value;
            }
            break;
            
        case HANDLE_OTA_READ_CS_KEY:
            rc = OtaReadCsKey(pInd->value[0], /* keyIndex */
                              dataTransferMemory, 
                              &dataTransferDataLength);
            break;
            
        case HANDLE_OTA_DATA_TRANSFER_CLIENT_CONFIG:
            client_config = BufReadUint16(&(pInd->value));

            if((client_config == gatt_client_config_notification) ||
               (client_config == gatt_client_config_none))
            {
                dataTransferConfiguration[0] = client_config;
                rc = sys_status_success;
            }
            else
            {
                /* INDICATION or RESERVED */

                /* Return error as only notifications are supported */
                rc = GATT_CCCD_ERROR;
            }
            break;
            
        default:
            /* Writing to this characteristic is not permitted */
            break;
    }
 
    GattAccessRsp(pInd->cid, pInd->handle, rc, 0, NULL);
    
    /* Perform now any follow-up actions */
    switch(pInd->handle)
    {
        case HANDLE_OTA_READ_CS_KEY:
            /* If this write action was to trigger a CS-key read, and the read was
             * successful, send the result now.
             */
            if((rc == sys_status_success) &&
               (dataTransferConfiguration[0] == gatt_client_config_notification))
            {
                GattCharValueNotification(CONNECTION_CID, 
                                          HANDLE_OTA_DATA_TRANSFER, 
                                          dataTransferDataLength,
                                          dataTransferMemory);
            }
            break;
            
        case HANDLE_OTA_CURRENT_APP:
            if(rc == sys_status_success)
            {
                /* Record that the GATT database may well be different
                 * after the chip has reset.
                 */
                GattOnOtaSwitch(); // todo: where is this defined?
                
                /* When the disconnect confirmation comes in, call
                 * OtaReset().
                 */
                otaResetRequired = TRUE;
                
                /* Disconnect from the remote device */
                GattDisconnectReq(CONNECTION_CID);
            }
            break;
            
        default:
            break;
    }
}
