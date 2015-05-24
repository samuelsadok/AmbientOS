/*
* gatt.c
*
* Created: 28.03.2014
*  Author: samuel
*/

#include <system.h>


#if defined(USING_BLE_CENTRAL) || defined(USING_BLE_PERIPHERAL)


#if defined(BLE_RANDOM_ADDRESS) && defined(BLE_PUBLIC_ADDRESS)
#	error "you cannot use both addressing types at once"
#endif



typedef struct
{
	endpoint_t *endpoint;
	uint16_t handle;
} ble_attribute_t;

// Imports a named endpoint for use as a bluetooth low energy attribute (ble_attribute_t).
//	handlerName: the name of the handle as used in the CSR GATT database
// The connection parameter passed to the endpoint functions is a pointer to the according ble_connection_t struct.
#define BLE_IMPORT_ENDPOINT(_name, _handleName) {			\
	.endpoint = &_name ## _endpoint,						\
	.handle = HANDLE_ ## _handleName						\
}


ble_attribute_t attributes[] = BLE_ENDPOINTS; // defined in the platform definition file

// Returns the attribute associated with the specified handle.
// Attributes (characteristics) that are handled this way should be marked with FLAG_IRQ in the database.
//	handle: a reference to an attribute
//	attribute: set to the attribute referenced by the handle
// The output is only valid if the call succeeds.
status_t ble_get_attribute(uint16_t handle, ble_attribute_t **attributePtr) {
	for (size_t i = 0; i < (sizeof(attributes) / sizeof(ble_attribute_t)); i++)
		if (attributes[i].handle == handle)
			return *attributePtr = &(attributes[i]), STATUS_SUCCESS;
	return STATUS_ERROR;
}


char ble_device_name[] = " " BLE_DEVICE_NAME;


// Checks if the address is resolvable random or not
bool ble_gatt_is_address_resolvable_random(TYPED_BD_ADDR_T *addr) {
	if ((addr->type != L2CA_RANDOM_ADDR_TYPE) ||
		(addr->addr.nap & BD_ADDR_NAP_RANDOM_TYPE_MASK) != BD_ADDR_NAP_RANDOM_TYPE_RESOLVABLE)
		return FALSE; // This isn't a resolvable private address
	return TRUE;
}


char* ble_get_name_and_length(size_t *length) {
	*length = sizeof(ble_device_name) - 2; // todo: make dynamic
	return ble_device_name + 1;
}





// This constant is used in the main server app to define array that is large enough to hold the advertisement data.
#define MAX_ADV_DATA_LEN                                  (31)

// Acceptable shortened device name length that can be sent in advertisement data
#define SHORTENED_DEV_NAME_LEN                            (8)

// length of Tx Power prefixed with 'Tx Power' AD Type
#define TX_POWER_VALUE_LENGTH                             (2)


// This function is used to add device name to advertisement or scan
// response data. It follows below steps:
// a. Try to add complete device name to the advertisment packet
// b. Try to add complete device name to the scan response packet
// c. Try to add shortened device name to the advertisement packet
// d. Try to add shortened (max possible) device name to the scan response packet
void ble_gatt_add_device_name_to_ad_data(uint16_t adv_data_len, uint16_t scan_data_len) {
	sys_status status;

	// Read device name along with AD Type and its length
	uint16_t device_name_adtype_len = sizeof(ble_device_name) - 1;
	unsigned char *device_name = (unsigned char *)ble_device_name;

	device_name[0] = AD_TYPE_LOCAL_NAME_COMPLETE; // Add complete device name to Advertisement data

	// Increment device_name_length by one to account for length field which will be added by the GAP layer.

	if ((device_name_adtype_len + 1) <= (MAX_ADV_DATA_LEN - adv_data_len)) {
		// Add Complete Device Name to Advertisement Data
		if ((status = LsStoreAdvScanData(device_name_adtype_len, device_name, ad_src_advertise)) != ls_err_none)
			bug_check(STATUS_BLE_ADV_SETUP, status);

	} else if ((device_name_adtype_len + 1) <= (MAX_ADV_DATA_LEN - scan_data_len)) {
		// Add Complete Device Name to Scan Response Data
		if ((status = LsStoreAdvScanData(device_name_adtype_len, device_name, ad_src_scan_rsp)) != ls_err_none)
			bug_check(STATUS_BLE_ADV_SETUP, status);

	} else if ((MAX_ADV_DATA_LEN - adv_data_len) >= (SHORTENED_DEV_NAME_LEN + 2)) { // Added 2 for Length and AD type added by GAP layer
		// Add shortened device name to Advertisement data
		device_name[0] = AD_TYPE_LOCAL_NAME_SHORT;

		if ((status = LsStoreAdvScanData(SHORTENED_DEV_NAME_LEN, device_name, ad_src_advertise)) != ls_err_none)
			bug_check(STATUS_BLE_ADV_SETUP, status);

	} else { // Add device name to remaining Scan reponse data space
		// Add as much as can be stored in Scan Response data
		device_name[0] = AD_TYPE_LOCAL_NAME_SHORT;

		if ((status = LsStoreAdvScanData(MAX_ADV_DATA_LEN - scan_data_len, device_name, ad_src_scan_rsp)) != ls_err_none)
			bug_check(STATUS_BLE_ADV_SETUP, status);
	}
}



static uint16_t ble_add_services_to_ad_data(uint8_t *service_uuid_ad) {
	uint8_t i = 0;

	// Add 16-bit UUID for supported main service
	service_uuid_ad[i++] = AD_TYPE_SERVICE_UUID_16BIT_LIST;

	service_uuid_ad[i++] = LE8_L(BLE_MAIN_SERVICE);
	service_uuid_ad[i++] = LE8_H(BLE_MAIN_SERVICE);

	return ((uint16_t)i);

}



// This function is used to set advertisement parameters
void ble_gatt_set_ad_params(bool fastConnection) {
	sys_status status;
	uint8_t advert_data[MAX_ADV_DATA_LEN];
	uint16_t length;

	int8_t tx_power_level = 0xff; // Signed value

	// Tx power level value prefixed with 'Tx Power' AD Type
	uint8_t device_tx_power[TX_POWER_VALUE_LENGTH] = { AD_TYPE_TX_POWER };

//	uint8_t device_appearance[ATTR_LEN_DEVICE_APPEARANCE + 1] = { AD_TYPE_APPEARANCE, LE8_L(BLE_APPEARANCE), LE8_H(BLE_APPEARANCE) };
	uint8_t device_appearance[] = { AD_TYPE_APPEARANCE, LE8_L(BLE_APPEARANCE), LE8_H(BLE_APPEARANCE) };

	// A variable to keep track of the data added to AdvData. The limit is MAX_ADV_DATA_LEN. GAP layer will add AD Flags to AdvData which is 3 bytes. Refer BT Spec 4.0, Vol 3, Part C, Sec 11.1.3.
	uint16_t length_added_to_adv = 3;


	// we can block the iOS device if we do this wrong (security: none, bonding: yes)
#if defined(BLE_PAIRING_NONE) // no encryption/authentication
	//gap_mode_security security = gap_mode_security_none;
	gap_mode_security security = gap_mode_security_unauthenticate;
	gap_mode_bond bond = gap_mode_bond_no;
#elif defined(BLE_PAIRING_SIMPLE) // the user presses yes/no
	gap_mode_security security = gap_mode_security_unauthenticate;
	gap_mode_bond bond = gap_mode_bond_yes;
#else
#	error "you must specify a pairing mode"
#endif

	uint32_t intervalMin = (fastConnection ? BLE_FC_ADVERTISING_INTERVAL_MIN : BLE_RP_ADVERTISING_INTERVAL_MIN);
	uint32_t intervalMax = (fastConnection ? BLE_FC_ADVERTISING_INTERVAL_MAX : BLE_RP_ADVERTISING_INTERVAL_MAX);

	if ((status = GapSetMode(gap_role_peripheral, gap_mode_discover_general, gap_mode_connect_undirected, bond, security)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);
	if ((status = GapSetAdvInterval(intervalMin, intervalMax)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);

	// Reset existing advertising data and scan response data
	if ((status = LsStoreAdvScanData(0, NULL, ad_src_advertise)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);

	/* Setup ADVERTISEMENT DATA */

	// Add UUID list of the services supported by the device
	length = ble_add_services_to_ad_data(advert_data);

	// One added for Length field, which will be added to Adv Data by GAP layer
	length_added_to_adv += (length + 1);

	if ((status = LsStoreAdvScanData(length, advert_data, ad_src_advertise)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);

	// One added for Length field, which will be added to Adv Data by GAP layer
	length_added_to_adv += (sizeof(device_appearance)+1);

	// Add device appearance to the advertisements
	if ((status = LsStoreAdvScanData(sizeof(device_appearance), device_appearance, ad_src_advertise)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);

	// Read tx power of the chip
	if ((status = LsReadTransmitPowerLevel(&tx_power_level)) != ls_err_none)
		bug_check(STATUS_BLE_RADIO, status);

	// Add the read tx power level to device_tx_power Tx power level value is of 1 byte
	device_tx_power[TX_POWER_VALUE_LENGTH - 1] = (uint8)tx_power_level;

	// One added for Length field, which will be added to Adv Data by GAP layer
	length_added_to_adv += (TX_POWER_VALUE_LENGTH + 1);

	// Add tx power value of device to the advertising data (can be used for distance estimation)
	if ((status = LsStoreAdvScanData(TX_POWER_VALUE_LENGTH, device_tx_power, ad_src_advertise)) != ls_err_none)
		bug_check(STATUS_BLE_ADV_SETUP, status);

	ble_gatt_add_device_name_to_ad_data(length_added_to_adv, 0);
}



// Handles Advertisement timer expiry.
void ble_gatt_handle_ad_timer(void *context) {
	ble_advertise(bleData.advMode == ble_adv_fast ? ble_adv_slow : ble_adv_none);
}


// This function is used to start undirected advertisements and moves to ADVERTISING state.
void ble_advertise(ble_adv_mode_t mode) {
	// Variable 'connect_flags' needs to be updated to have peer address type
	// if Directed advertisements are supported as peer address type will
	// only be used in that case. We don't support directed advertisements for
	// this application.

	timer_stop(&(bleData.advTimer));

	// if we're still advertising, cancel first
	if (bleData.advMode != ble_adv_none) {
		bleData.advMode = mode; // this is the mode that will be entered when the cancellation completes
		GattCancelConnectReq();
		return;
	}

	if ((bleData.advMode = mode) == ble_adv_none)
		return;


#if defined(BLE_RANDOM_ADDRESS)
	uint16_t connect_flags = L2CAP_OWN_ADDR_TYPE_RANDOM;
#elif defined(BLE_PUBLIC_ADDRESS)
	uint16_t connect_flags = L2CAP_OWN_ADDR_TYPE_PUBLIC;
#else
#	error "you must either use a static random address or a public address"
#endif

	connect_flags |= L2CAP_CONNECTION_SLAVE_UNDIRECTED; // todo: add whitelist support

	// Set advertisement parameters
	ble_gatt_set_ad_params(bleData.advMode == ble_adv_fast);

	// Start GATT connection in Slave role (= advertisments)
	GattConnectReq(NULL, connect_flags);

	// Start advertisement timer
	bleData.advTimer = (timer_t) CREATE_TIMER(bleData.advMode == ble_adv_fast ? BLE_FAST_ADV_TIMEOUT : BLE_SLOW_ADV_TIMEOUT, ble_gatt_handle_ad_timer, NULL);
	timer_start(&bleData.advTimer);
}


void ble_disconnect(ble_connection_t *connection) {
	GattDisconnectReq(connection->id);
}










// ***************** GATT EVENT HANDLERS *******************


// Database is set up, start advertising
void ble_gatt_add_db_completed(GATT_ADD_DB_CFM_T *event_data) {
	if (event_data->result != sys_status_success)
		bug_check(STATUS_BLE_DB, event_data->result);

	ble_advertise(ble_adv_fast);
}



// Connection requested
void ble_gatt_connect_completed(GATT_CONNECT_CFM_T *event_data) {
	bleData.advMode = ble_adv_none;

	if (event_data->result != sys_status_success)
		return; // wait for user activity before we start advertising again

	ble_connection_t *connection = ble_alloc_connection();
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->cid);
	bleData.connectionCount++;
	
	connection->id = event_data->cid;
	connection->bond = ble_recall_bond(&event_data->bd_addr, &connection->bondNum);
	connection->addr = event_data->bd_addr;
	connection->connInterval = 0;
	connection->connLatency = 0;
	connection->connTimeout = 0;
	

	bool acceptConnection = 1;

	// Check for special case: application was bonded to a remote device with resolvable random address and application has failed to
	// resolve the remote device address to which we just connected So disconnect and start advertising again
	if (connection->bond)
		if (ble_gatt_is_address_resolvable_random(&connection->bond->addr) &&
			(SMPrivacyMatchAddress(&connection->addr, connection->bond->irk, 1, BLE_IRK_LENGTH >> 1) < 0))
			acceptConnection = 0;
	

	if (acceptConnection) {

#if defined(USING_BLE_CENTRAL)
		// Initiate slave security request if the remote host
		// supports security feature. This is added for this device
		// to work against legacy hosts that don't support security

		// Security supported by the remote host
		if (bleData.bonded && !ble_gatt_address_is_resolvable_random(&connection->addr)) {
			// Application sends slave security request only if it bonded to some device.
			// If bonded device has resolvable random address, then also we don't send this request
			SMRequestSecurityLevel(&connection->addr);
		}

		// Start service discovery procedure
		StartDiscoveryProcedure();

#elif defined(USING_BLE_PERIPHERAL)

#ifdef USING_GATT_SERVICE
		gatt_service_on_connection(connection); // might trigger a service changed indication
#endif

		// This device does not mandate encryption. Let the host decide.

		ble_negotiate_params(connection);
#endif
	} else {
		ble_disconnect(connection);
	}

	// if we have resources for more connections, continue advertisements
	if (bleData.connectionCount < BLE_MAX_CONNECTIONS)
		ble_advertise(ble_adv_fast);
}



// Connection was cancelled by local or remote host
void ble_gatt_connect_cancelled(void) {
#ifndef USING_BLE_PERIPHERAL
#error "todo: implement"
#endif

	// switch to mode that was set when issuing the cancellation
	ble_adv_mode_t mode = bleData.advMode;
	bleData.advMode = ble_adv_none;
	ble_advertise(mode);

	if (mode == ble_adv_none) {
		// todo: notify application that ads stopped
	}
}


#define exit_access(status) do { ; return; } while(0)

// Executed when the remote host accessed GATT for a characteristic that is manually
// controlled by the application (as opposed to the database).
// Maps the GATT attribute access to an endpoint access and responds accordingly.
void ble_gatt_access_indication(GATT_ACCESS_IND_T *event_data) {
#ifdef USING_BLE_PERIPHERAL // nothing to do on central
	ble_connection_t *connection = ble_recall_connection(event_data->cid);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->cid);

	status_t status;

	// get attribute for this access
	ble_attribute_t *attribute;
	if ((status = (ble_get_attribute(event_data->handle, &attribute) ? gatt_status_attr_not_found : STATUS_SUCCESS)))
		goto exit;
	if ((status = (event_data->size_value > BLE_MAX_TRANSFER_LENGTH ? gatt_status_invalid_length : STATUS_SUCCESS)))
		goto exit;


	// write to attribute
	if (event_data->flags & ATT_ACCESS_WRITE) {
		// pack buffer (LSB first)
		for (size_t i = 0; i < event_data->size_value; i += 2)
			event_data->value[i >> 1] = event_data->value[i] | (event_data->value[i + 1] << 8);
		if ((status = (attribute->endpoint->write ? STATUS_SUCCESS : gatt_status_write_not_permitted)))
			goto exit;
		if ((status = attribute->endpoint->write(connection, event_data->offset, (char *)event_data->value, event_data->size_value) | 0x80) != 0x80)
			goto exit;
	}

	size_t bufLen = BLE_MAX_TRANSFER_LENGTH;
	uint16_t buf[BLE_MAX_TRANSFER_LENGTH];

	// read from attribute
	if (event_data->flags & ATT_ACCESS_READ) {
		if ((status = (attribute->endpoint->read ? STATUS_SUCCESS : gatt_status_read_not_permitted)))
			goto exit;
		if ((status = attribute->endpoint->read(connection, event_data->offset, (char *)buf, &bufLen) | 0x80) != 0x80)
			goto exit;
		assert(bufLen <= BLE_MAX_TRANSFER_LENGTH);

		// unpack buffer (LSB first)
		for (int i = bufLen - 1; i >= 0; i--)
			buf[i] = (buf[i >> 1] >> ((i & 1) ? 8 : 0)) & 0xFF;
	}



exit:

	// send response if neccessary
	if (status != sys_status_success) {
		GattAccessRsp(event_data->cid, event_data->handle, status, 0, NULL);
	} else if (event_data->flags & (ATT_ACCESS_PERMISSION | ATT_ACCESS_WRITE_COMPLETE)) {
		if (event_data->flags & ATT_ACCESS_READ)
			GattAccessRsp(event_data->cid, event_data->handle, sys_status_success, bufLen, (unsigned char *)buf);
		else
			GattAccessRsp(event_data->cid, event_data->handle, sys_status_success, 0, NULL);

		// carry out follow-up tasks
		if (event_data->flags & ATT_ACCESS_WRITE_COMPLETE)
			if (attribute->endpoint->writeComplete)
				attribute->endpoint->writeComplete(connection);
	}


#endif
}




#endif
