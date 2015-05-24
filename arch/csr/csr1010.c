/*
 * csr1010.c
 *
 * Created: 24.07.2013 18:16:57
 *  Author: samuel
 */ 

#include <system.h>

#ifdef USING_DFU
#  include <system/dfu.h>
#endif

#ifdef USING_NVM
#  include <hardware/nvm.h>
#endif

/*
#if defined(USING_BLE_CENTRAL) || defined(USING_BLE_PERIPHERAL)
#	define BLE_TIMERS	(BLE_MAX_CONNECTIONS + 1)
#else
#	define BLE_TIMERS	(0)
#endif
*/


/*
// This function is used to initialize and read NVM data
static void read_persistent_store(void) {
uint16 nvm_sanity = 0xffff;

// Read persistent storage to know if the device was last bonded.
// If the device was bonded, trigger fast undirected advertisements by
// setting the white list for bonded host. If the device was not bonded,
// trigger undirected advertisements for any host to connect.

nvm_read(&nvm_sanity, sizeof(nvm_sanity), NVM_OFFSET_SANITY_WORD);

if (nvm_sanity == NVM_SANITY_MAGIC) {
// Read bonded flag from NVM
nvm_read((uint16_t*)&ble_data.bonded, sizeof(ble_data.bonded), NVM_OFFSET_BONDED_FLAG);

if (ble_data.bonded) { // Bonded host BD address will only be stored if bonded flag is set to TRUE.
nvm_read((uint16_t*)&ble_data.bonded_bd_addr, sizeof(TYPED_BD_ADDR_T), NVM_OFFSET_BONDED_ADDR);

// If device is bonded and bonded address is resolvable then read the bonded device's IRK
if (ble_gatt_is_address_resolvable_random(&ble_data.bonded_bd_addr))
nvm_read(ble_data.central_device_irk, MAX_WORDS_IRK, NVM_OFFSET_SM_IRK);
}

// Read the diversifier associated with the presently bonded/last bonded device.
nvm_read(&ble_data.diversifier, sizeof(ble_data.diversifier), NVM_OFFSET_SM_DIV);

// todo: If NVM in use, read device name and length from NVM
// uint16_t nvm_offset = NVM_MAX_APP_MEMORY_WORDS;
// GapReadDataFromNVM(&nvm_offset);

} else {
// NVM sanity check failed means either the device is being brought up
// for the first time or memory has got corrupted in which case
// discard the data and start fresh.

// The device will not be bonded as it is coming up for the first time
ble_data.bonded = FALSE;
nvm_write((uint16_t*)&ble_data.bonded, sizeof(ble_data.bonded), NVM_OFFSET_BONDED_FLAG);

// When the application is coming up for the first time after flashing
// the image to it, it will not have bonded to any device. So, no LTK
// will be associated with it. Hence, set the diversifier to 0.
ble_data.diversifier = 0;
nvm_write(&ble_data.diversifier, sizeof(ble_data.diversifier), NVM_OFFSET_SM_DIV);

// todo: If fresh NVM, write device name and length to NVM for the first time.
//GapInitWriteDataToNVM(&nvm_offset);

// Write NVM sanity word to the NVM
nvm_sanity = NVM_SANITY_MAGIC;
nvm_write(&nvm_sanity, sizeof(nvm_sanity), NVM_OFFSET_SANITY_WORD);
}

// If device is bonded and bonded address is resolvable then read the bonded device's IRK
if (ble_data.bonded && ble_gatt_is_address_resolvable_random(&ble_data.bonded_bd_addr))
nvm_read((uint16_t*)ble_data.central_device_irk, MAX_WORDS_IRK, NVM_OFFSET_SM_IRK);

uint16_t offset = NVM_MAX_APP_MEMORY_WORDS;
GattReadDataFromNVM(&offset);
}*/








#ifdef USING_BLE_CENTRAL // ****** LEGACY - needs verification and completition ******

// Info about server received
// CLIENT ONLY
static void handleGattServInfoInd(GATT_SERV_INFO_IND_T *ind) {
	// Check the UUID to see whether it is one of interest.

	// We are not interested in 128-bit UUIDs
	if (ind->uuid_type == GATT_UUID16) {
		// Alert notification service
		if ((ind->uuid[0]) == UUID_ALERT_NOTIFICATION_SERVICE) {
			g_app_ans_data.ans_start_handle = ind->strt_handle;
			g_app_ans_data.ans_end_handle = ind->end_handle;
			// Alert notification service supported
			ble_data.connected_device_profiles |= ans_supported;
		} else if ((ind->uuid[0]) == UUID_GATT) {  // Gatt service
			g_app_gatt_data.gatt_start_hndl = ind->strt_handle;
			g_app_gatt_data.gatt_end_hndl = ind->end_handle;
		} else if ((ind->uuid[0]) == UUID_PHONE_ALERT_STATUS) {
			// Phone alert status service
			g_app_pas_data.pas_start_hndl = ind->strt_handle;
			g_app_pas_data.pas_end_hndl = ind->end_handle;
			// Phone alert status service supported
			ble_data.connected_device_profiles |= pas_supported;

			// todo: add additional relevant profile
		}
		// We are not interested in any other service
	}
}


// CLIENT ONLY
static void handleGattDiscAllPrimServCfm(GATT_DISC_ALL_PRIM_SERV_CFM_T *cfm) {

	// Find the range which will include all the three services
	uint16_t	start_hndl = g_app_gatt_data.gatt_start_hndl;
	uint16_t	end_hndl = g_app_gatt_data.gatt_end_hndl;

	if (ble_data.connected_device_profiles & ans_supported) {
		start_hndl = MIN_SET(start_hndl, g_app_ans_data.ans_start_handle);
		end_hndl = MAX(end_hndl, g_app_ans_data.ans_end_hndl);
	}
	if (ble_data.connected_device_profiles & pas_supported) {
		start_hndl = MIN_SET(start_hndl, g_app_pas_data.pas_start_handle);
		end_hndl = MAX(end_hndl, g_app_pas_data.pas_end_hndl);
	}
	// todo: add all supported profiles


	if (cfm->result == sys_status_success) {
		// Check that the remote device supports something interesting
		if ((ble_data.connected_device_profiles & ans_supported) || (ble_data.connected_device_profiles & pas_supported)) {
			// todo: discover for all supported profiles
			// Discover all the characteristics of all the services
			GattDiscoverServiceChar(ble_data.st_ucid, start_hndl, end_hndl, GATT_UUID_NONE, NULL);
		} else {
			// The remote device does not support anything interesting.
			ble_change_state(ble_state_disconnecting);
		}
	} else {
		// Something went wrong. We can't recover, so disconnect.
		ble_change_state(ble_state_disconnecting);
	}
}


// Characteristic discovered
// CLIENT ONLY
static void handleGattCharDeclInfoInd(GATT_CHAR_DECL_INFO_IND_T *ind) {
	uint16_t uuid = ind->uuid[0];

	// Check the UUID to see whether it is one of interest.

	// We are not interested in 128-bit UUIDs
	if (ind->uuid_type == GATT_UUID16) {

		void *conf_handle_ptr = NULL;

		// todo: capture each interesting characteristic
		if (uuid == UUID_BLA) {
			g_app_ans_data.bla_hndl = ind->val_handle;
		} else if (uuid == UUID_BLA2) {
			g_app_ans_data.bla2_hndl = ind->val_handle
				conf_handle_ptr = &g_app_ans_data.bla2_char_end_hndl;

		} else {
			// Not interested in this characteristic
			ble_data.conf_handle_ptr = NULL;
			if (uuid == UUID_SERVICE_CHANGED)
				g_app_gatt_data.service_change_hndl = ind->val_handle;
		}

		if (ble_data.conf_handle_ptr) {
			// 2 has been subtracted because end handle of last characteristic received will be 2 less than the value handle
			// received just now (one for characteristic declaration handle and one for value attribute)
			*ble_data.conf_handle_ptr = (ind->val_handle) - 2;
			ble_data.conf_handle_ptr = conf_handle_ptr;
		}

	}
}


// All characteristics discovered
// CLIENT ONLY
static void handleGattDiscServCharCfm(GATT_DISC_SERVICE_CHAR_CFM_T *ind) {
	if (ind->result == sys_status_success) {
		// todo: discover descriptors in supported characteristics

		//	// If ANS is supported then start with the ANS otherwise start with PAS
		//	if (ble_data.connected_device_profiles & ans_supported) {
		//		// Discover characteristic descriptors of ANS new alert characteristic
		//		GattDiscoverAllCharDescriptors(ble_data.st_ucid,
		//									   g_app_ans_data.new_alert_hndl,
		//									   g_app_ans_data.new_alert_char_end_hndl);
		//		ble_data.conf_handle_ptr = &g_app_ans_data.new_alert_ccd_hndl;
		//	} else if (ble_data.connected_device_profiles & pas_supported) {
		//		// Discover characteristic descriptors of ANS new alert characteristic
		//		GattDiscoverAllCharDescriptors(ble_data.st_ucid,
		//									   g_app_pas_data.phone_alert_hndl,
		//									   g_app_pas_data.phone_alert_char_end_hndl);
		//		ble_data.conf_handle_ptr = &g_app_pas_data.phone_alert_ccd_hndl;
		//	}

	} else {
		// Something went wrong. We can't recover, so disconnect.
		ble_change_state(ble_state_disconnecting);
	}
}


// The interesting characteristics are explored one-by one and the conf_desc handle of each characteristic
// is saved in the reserved field for that characteristic.

// One descriptor discovered
// CLIENT ONLY
static void handleGattCharDescriptorInfoInd(GATT_CHAR_DESC_INFO_IND_T *ind) {
	// Check the UUID to see whether it is one of interest.
	// We are not interested in 128-bit UUIDs
	if (ind->uuid_type == GATT_UUID16 &&
		ind->uuid[0] == UUID_CLIENT_CHARACTERISTIC_CONFIGURATION_DESC) {
		// todo: save ind->desc_handle in a field associated with the currently explored characteristic
	}
}


// One descriptor discovered
// CLIENT ONLY
static void handleGattDiscAllCharDescCfm(GATT_DISC_ALL_CHAR_DESC_CFM_T *cfm) {
	if (((GATT_DISC_ALL_CHAR_DESC_CFM_T *)event_data)->result == sys_status_success) {
		// todo: discover descriptors of next characteristic
		// if done, do something with the now known config data
		// e.g. enable notifications: MainEnableNotifications(cid, g_app_pas_data.phone_alert_ccd_hndl);
	} else {// Something went wrong. We can't recover, so disconnect
		ble_change_state(ble_state_disconnecting);
	}
}

#endif









// This user application function is called whenever a system event, such
// as a battery low notification, is received by the system.
void AppProcessSystemEvent(sys_event_id id, void *data) {
	switch (id) {
		case sys_event_battery_low: // battery low
			// todo: handle

			//if (ble_data.state == ble_state_connected)
			//	BatteryUpdateLevel(ble_data.st_ucid);
			break;

		case sys_event_pio_changed:
			// todo: handle
			break;

		default:
			break;
	}
}





// This user application function is called whenever a LM-specific event is
// received by the system.
//
// Returns:
//	Application should always return TRUE. Refer API Documentation under the module
//	named "Application" for more information.
bool AppProcessLmEvent(lm_event_code event_code, LM_EVENT_T *event_data) {
	if (didBugcheck)
		return TRUE;

	switch (event_code) {

		// ****************** GATT EVENTS ****************** //

		case GATT_ADD_DB_CFM: // client, host
			ble_gatt_add_db_completed((GATT_ADD_DB_CFM_T*)event_data);
			break;


		case GATT_CONNECT_CFM: // client, host
			ble_gatt_connect_completed((GATT_CONNECT_CFM_T *)event_data);
			break;

		case GATT_CANCEL_CONNECT_CFM: // client, host // Confirmation for the completion of GattCancelConnectReq() procedure
			ble_gatt_connect_cancelled();
			break;


		case GATT_ACCESS_IND: // client, host
			// Indicates that an attribute controlled directly by the application (ATT_ATTR_IRQ attribute flag is set) is being read from or written to.
			ble_gatt_access_indication((GATT_ACCESS_IND_T*)event_data);
			break;


		case GATT_DISCONNECT_IND: // client, host
		case GATT_DISCONNECT_CFM: // client, host
			// Disconnect procedure triggered by remote host (or local GattDisconnectReq()) or due to link loss is considered complete on reception of LM_EV_DISCONNECT_COMPLETE event.
			// So, it gets handled on reception of LM_EV_DISCONNECT_COMPLETE event.
			break;



			// ****************** GATT EVENTS (client only) ****************** //

#ifdef USING_BLE_CENTRAL

		case GATT_NOT_CHAR_VAL_IND: // client
			// A notification has been received. Depending on the handle, it will get handled in corresponding function.
			handleANSGattCharValInd((GATT_CHAR_VAL_IND_T *)event_data);
			HandlePASGattCharValInd((GATT_CHAR_VAL_IND_T *)event_data);
			handleGattServiceCharValInd((GATT_CHAR_VAL_IND_T *)event_data);
			break;

		case GATT_CHAR_VAL_NOT_CFM: // client
			break;


		case GATT_SERV_INFO_IND: // client // This service info indication comes for every service present on the server.
			handleGattServInfoInd((GATT_SERV_INFO_IND_T *)event_data);
			break;

		case GATT_DISC_ALL_PRIM_SERV_CFM: // client // This signal comes on completion of primary service discovery 
			handleGattDiscAllPrimServCfm((GATT_DISC_ALL_PRIM_SERV_CFM_T *)event_data);
			break;

		case GATT_CHAR_DECL_INFO_IND: // client // This signal comes when gatt client starts procedure for discovering all the characterstics
			handleGattCharDeclInfoInd((GATT_CHAR_DECL_INFO_IND_T *)event_data);
			break;

		case GATT_DISC_SERVICE_CHAR_CFM: // client // This signal comes on completion of characteristic discovery
			handleGattDiscServCharCfm((GATT_DISC_SERVICE_CHAR_CFM_T *)event_data);
			break;

		case GATT_CHAR_DESC_INFO_IND: // client // This indication signal comes on characteristic descriptor discovery
			handleGattCharDescriptorInfoInd((GATT_CHAR_DESC_INFO_IND_T *)event_data);
			break;

		case GATT_DISC_ALL_CHAR_DESC_CFM: // client // This signal comes on completion of characteristic descriptor discovery
			handleGattDiscAllCharDescCfm((GATT_DISC_ALL_CHAR_DESC_CFM_T *)event_data);
			break;

		case GATT_READ_CHAR_VAL_CFM: // client
			if (((GATT_READ_CHAR_VAL_CFM_T *)event_data)->result == sys_status_success) {
				// If this siganl is for ANS characteristic read, it will get handled in handleANSGattReadCharValCFM,
				// If this signal is for PAS, it will get handled in HandlePASGattReadCharValCFM
				handleANSGattReadCharValCFM((GATT_READ_CHAR_VAL_CFM_T *)event_data);
				HandlePASGattReadCharValCFM((GATT_READ_CHAR_VAL_CFM_T *)event_data);
			} else if ((((GATT_READ_CHAR_VAL_CFM_T *)event_data)->result == GATT_RESULT_INSUFFICIENT_ENCRYPTION) ||
					   (((GATT_READ_CHAR_VAL_CFM_T *)event_data)->result == GATT_RESULT_INSUFFICIENT_AUTHENTICATION)) {
				// If we have received an error with error code insufficient encryption, we will start a slave security request
				SMRequestSecurityLevel(&ble_data.con_bd_addr);
			} else if (((GATT_READ_CHAR_VAL_CFM_T *)event_data)->result != GATT_RESULT_TIMEOUT) { // ATT Timerout case gets handled automatically
				// Something went wrong. We can't recover, so disconnect
				ble_change_state(ble_state_disconnecting);
			}
			break;

		case GATT_WRITE_CHAR_VAL_CFM: // client
			if (((GATT_WRITE_CHAR_VAL_CFM_T *)event_data)->result == sys_status_success) {
				handleGattWriteCharValCFM((GATT_WRITE_CHAR_VAL_CFM_T *)event_data);

				if (g_app_ans_data.config_in_progress == FALSE && ble_data.state == ble_state_connected_discovering) {
					// Configuration process is complete, change application state to connected
					ble_change_state(ble_state_connected);

					// Phone alert status profile mandates the read of phone alert status characteristic on connection.
					// Our application will read it after all the configuration is done and application state switches back to ble_state_connected
					if (ble_data.connected_device_profiles & pas_supported) {
						// Read the phone alert status characteristic
						ReadPASPhoneAlertChar(((GATT_WRITE_CHAR_VAL_CFM_T *)event_data)->cid);
					}
				}
			} else if ((((GATT_WRITE_CHAR_VAL_CFM_T *)event_data)->result == GATT_RESULT_INSUFFICIENT_ENCRYPTION) ||
					   (((GATT_WRITE_CHAR_VAL_CFM_T *)event_data)->result == GATT_RESULT_INSUFFICIENT_AUTHENTICATION)) {
				// If we have received an error with error code insufficient encryption, we will start a slave security request
				SMRequestSecurityLevel(&ble_data.con_bd_addr);
			} else if (((GATT_READ_CHAR_VAL_CFM_T *)event_data)->result != GATT_RESULT_TIMEOUT) { // Time out case gets handled when we receive disconnect indication.
				// Something went wrong. We can't recover, so disconnect
				ble_change_state(ble_state_disconnecting);
			}
			break;

#endif


			// ****************** LM EVENTS ****************** //

		case LM_EV_CONNECTION_COMPLETE: // client, host // Handle the LM connection complete event.
			ble_lm_connection_completed((LM_EV_CONNECTION_COMPLETE_T*)event_data);
			break;

#ifdef USING_BLE_PERIPHERAL
		case LM_EV_CONNECTION_UPDATE: // client // This event is sent by the controller on connection parameter update.
			ble_lm_connection_updated((LM_EV_CONNECTION_UPDATE_T*)event_data);
			break;
#endif

		case LM_EV_ENCRYPTION_CHANGE: // client, host
			ble_lm_encryption_changed(event_data);
			break;

		case LM_EV_DISCONNECT_COMPLETE: // host, client
			// Disconnect procedures either triggered by application or remote
			// host or link loss case are considered completed on reception
			// of LM_EV_DISCONNECT_COMPLETE event
			ble_lm_connection_closed(&((LM_EV_DISCONNECT_COMPLETE_T *)event_data)->data);
			break;

#ifdef USING_BLE_CENTRAL
		case LM_EV_NUMBER_COMPLETED_PACKETS: // client
			break;
#endif


			// ****************** LS EVENTS ****************** //

#ifdef USIN_BLE_CENTRAL
		case LS_CONNECTION_UPDATE_SIGNALLING_IND: // Received when the slave wishes to initiate a connection param update procedure
			ble_ls_conn_param_update_signal((LS_CONNECTION_UPDATE_SIGNALLING_IND_T *)event_data);
			break;
#endif

		case LS_CONNECTION_PARAM_UPDATE_CFM: // client, host
			// Received in response to the LsConnectionParamUpdateReq() request sent from the slave after encryption is enabled.
			// If the request has failed, the device should again send the same request only after Tgap(conn_param_timeout).
			// Refer Bluetooth 4.0 spec Vol 3 Part C, Section 9.3.9 and HID over GATT profile spec section 5.1.2.
			ble_ls_conn_param_update_completed((LS_CONNECTION_PARAM_UPDATE_CFM_T *)event_data);
			break;

		case LS_CONNECTION_PARAM_UPDATE_IND: // client, host // Indicates completion of remotely triggered Connection parameter update procedure
			ble_ls_conn_param_update_indication((LS_CONNECTION_PARAM_UPDATE_IND_T *)event_data);
			break;







#ifdef USING_BLE_CENTRAL
		case SM_PAIRING_AUTH_IND: // client // Authorize or Reject the pairing request
			handleSignalSmPairingAuthInd((SM_PAIRING_AUTH_IND_T*)event_data);
			break;
#endif

		case SM_DIV_APPROVE_IND: // client, host
			// Indication for SM Diversifier approval requested by F/W when the last bonded host exchange keys.
			// Application may or may not approve the diversifier depending upon whether the application is still bonded to the same host
			ble_sm_diversifier_approval_indication((SM_DIV_APPROVE_IND_T *)event_data);
			break;

		case SM_KEYS_IND: // client, host // Indication for the keys and associated security information on a connection that has completed Short Term Key Generation or Transport Specific Key Distribution
			ble_sm_keys_indication((SM_KEYS_IND_T *)event_data);
			break;

		case SM_SIMPLE_PAIRING_COMPLETE_IND: // client, host // Indication for completion of Pairing procedure
			ble_sm_simple_pairing_completed((SM_SIMPLE_PAIRING_COMPLETE_IND_T *)event_data);
			break;




		default: // Control should never come here
			break;
	}
	return TRUE;
}







// This user application function is called just after a power-on reset
// (including after a firmware panic), or after a wakeup from Hibernate or
// Dormant sleep states.
// 
// At the time this function is called, the last sleep state is not yet
// known.
// 
// NOTE: this function should only contain code to be executed after a
// power-on reset or panic. Code that should also be executed after an
// HCI_RESET should instead be placed in the AppInit() function.
void AppPowerOnReset(void) {
	// nothing to do here
}




void main(void);

// This user application function is called after a power-on reset
// (including after a firmware panic), after a wakeup from Hibernate or
// Dormant sleep states, or after an HCI Reset has been requested.
// 
// The last sleep state is provided to the application in the parameter.
// 
// NOTE: In the case of a power-on reset, this function is called
// after app_power_on_reset().
void AppInit(sleep_state last_sleep_state) {
	// Initialize the application timers
	timer_init();

	// init non-volatile memory (either I2C or SPI)
#ifdef USING_NVM
	nvm_init();
#endif

	// init built in I2C controller
#ifdef USING_BUILTIN_I2C_MASTER
	builtin_i2c_init();
#endif

	// when in bootloader, launch application under certain conditions
#ifdef USING_DFU
	if (!nvmValid)
		dfu_init_nvm();

	dfu_init();
#endif

	// init bluetooth
#if defined(USING_BLE_CENTRAL) || defined(USING_BLE_PERIPHERAL)
	if (!nvmValid)
		ble_init_nvm();
	ble_init();
#endif

	// If not in bootloader, mark NVM data as initialized.
	// We don't do this in the bootloader as the actual application might have some
	// NVM fields that the bootloader doesn't know about.
#if defined(USING_NVM) && !defined(USING_BOOTLOADER)
	if (!nvmValid)
		nvm_data_init();
#endif

	// launch application
	main();
}


// For the XAP architecture, sync functions are not compiler-built-in
// The CSR1010 handles one interrupt at a time (does it?), so we don't care too much about synchronization.
bool __sync_bool_compare_and_swap_1(uint8_t *ptr, uint8_t oldVal, uint8_t newVal) {
	if (*ptr == oldVal) {
		*ptr = newVal;
		return 1;
	}
	return 0;
}

