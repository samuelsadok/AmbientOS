/*
* ble.c
* Together with gatt.c, enables bluetooth low energy functions.
* 
* Config options:
*	USING_BLE_CENTRAL				enables the central role
*	USING_BLE_PERIPHERAL			enables the peripheral role (both roles may be used in the same application)
*
*	BLE_DEVICE_NAME					default device name
*	BLE_CUSTOM_DEVICE_NAME			a bluetooth central can edit the device name
*	BLE_RANDOM_ADDRESS/BLE_PUBLIC_ADDRESS	the address type to be used by this device
*	BLE_PAIRING_NONE				don't require pairing
*
*	NVM_BLE_BONDS_OFFSET			the location in memory where the bonds are stored
*	NVM_BLE_BONDS_LENGTH			the number of bytes reserved for the ble driver to store bonds
*
*	These 3 values affect memory, stack and nvm requirements:
*	BLE_MAX_TRANSMISSION_LENGTH		maximum packet size this device should be able to handle
*	BLE_MAX_CONNECTIONS				maximum number of concurrent connections
*	BLE_MAX_BONDS					maximum number of bonds that this device should be able to maintain
*
*	... and many more
*
*
* Created: 24.03.2014
*  Author: samuel
*/

#include <system.h>

#ifdef USING_DFU
#  include <system/dfu.h>	// required to notify bonded devices about service changes
#endif

#ifdef USING_NVM
#  include <hardware/nvm.h>
#endif


#if defined(USING_BLE_CENTRAL) || defined(USING_BLE_PERIPHERAL)


STATIC_ASSERT(NVM_BLE_DATA_LENGTH >= (sizeof(ble_bond_t) * BLE_MAX_BONDS + sizeof(uint16_t)) * WORDSIZE, "reserved NVM space for ble bonds too small");

#define NVM_BLE_BONDS_OFFSET	NVM_BLE_DATA_OFFSET

ble_data_t bleData;



// Inits BLE related data in the NVM to default values
// This function is only called if the data in the NVM is not valid.
void ble_init_nvm(void) {
	for (size_t i = 0; i < BLE_MAX_BONDS; i++)
		bleData.bonds[i].valid = 0;
	bleData.oldestBond = 0;
	nvm_write(NVM_BLE_BONDS_OFFSET, (char *)bleData.bonds, (sizeof(ble_bond_t) + sizeof(uint16_t)) * WORDSIZE);
}


// This function is used to initialize application data structure.
void ble_init(void) {
	sys_status status;

	// reset the advertisment timer
	timer_stop(&bleData.advTimer);

	// reset all connections
	for (size_t i = 0; i < BLE_MAX_CONNECTIONS; i++)
		bleData.connections[i].id = GATT_INVALID_UCID;

	// restore bonding information
	nvm_read(NVM_BLE_BONDS_OFFSET, (char *)bleData.bonds, (sizeof(ble_bond_t) * BLE_MAX_BONDS + sizeof(uint16_t)) * WORDSIZE);

	// for each bond for which the device address is not resolvable random, configure White list with the Bonded host address
	if ((status = LsResetWhiteList()) != sys_status_success)
		bug_check(STATUS_BLE_WHITELIST, status);
	for (size_t i = 0; i < BLE_MAX_BONDS; i++)
		if (bleData.bonds[i].valid && (!ble_gatt_is_address_resolvable_random(&bleData.bonds[i].addr)))
			if ((status = LsAddWhiteListDevice(&bleData.bonds[i].addr)) != ls_err_none)
				bug_check(STATUS_BLE_WHITELIST, status); // this seems a little extreme

#ifdef USING_DFU
	if (dfuDidSwitch)
		gatt_register_service_change();
#endif

	/* todo: is this only relevant for central role?
	// Restore the last used diversifier
	nvm_read(NVM_OFFSET_SM_DIV, (char *)&bleData.diversifier, sizeof(bleData.diversifier) * WORDSIZE);
	SMInit(bleData.diversifier);
	*/

	// todo: make customizable via macro
	if ((status = LsSetTransmitPowerLevel(LS_MAX_TRANSMIT_POWER_LEVEL)) != sys_status_success)
		bug_check(STATUS_BLE_RADIO, status);

	GattInit();
#if defined(USING_BLE_CENTRAL)
	GattInstallClientRole();
#elif defined(USING_BLE_PERIPHERAL)
	GattInstallServerWriteLongReliable();
	GattInstallServerExchangeMtu();
#else
#	error "you must either use bluetooth central or peripheral role (why else would you use a CSR chip?)"
#endif

}


// Starts the BLE driver.
// This must only be called after ble_init and after all BLE services have been set up.
void ble_start(void) {
	// Tell GATT about our database. We will get a GATT_ADD_DB_CFM event when this has completed.
	uint16_t gatt_db_length = 0;
	uint16_t *p_gatt_db = GattGetDatabase(&gatt_db_length);
	GattAddDatabaseReq(gatt_db_length, p_gatt_db);
}


// Returns an unused connection structure.
// Returns NULL if all resources ar exhausted.
// The id of the connection must afterwards be set to a value other than GATT_INVALID_UCID.
// The structure may be reclaimed if the id is GATT_INVALID_UCID.
ble_connection_t *ble_alloc_connection(void) {
	for (size_t i = 0; i < BLE_MAX_CONNECTIONS; i++)
		if (bleData.connections[i].id == GATT_INVALID_UCID)
			return &bleData.connections[i];
	return NULL;
}

// Returns a connection structure identified by it's ID.
// Returns NULL if no match could be made.
//	id: the connection ID used by the framework
ble_connection_t *ble_recall_connection(uint16_t id) {
	for (size_t i = 0; i < BLE_MAX_CONNECTIONS; i++)
		if (bleData.connections[i].id == id)
			return &bleData.connections[i];
	return NULL;
}

// Returns a connection structure identified by the peer address.
// Returns NULL if no match could be made.
//	addr: the address of the remote device of this connection
ble_connection_t *ble_recall_connection_by_addr(TYPED_BD_ADDR_T *addr) {
	for (size_t i = 0; i < BLE_MAX_CONNECTIONS; i++)
		if (bleData.connections[i].id != GATT_INVALID_UCID)
			if (!MemCmp(&bleData.connections[i].addr, addr, sizeof(TYPED_BD_ADDR_T)))
				return &bleData.connections[i];
	return NULL;
}

// Returns an unused bond structure. This should be immediately added to a connection.
// The oldest bond may be reclaimed to satisfy the request.
//	*number: set to the index of the bond structure that was returned
ble_bond_t *ble_alloc_bond(size_t *number) {
	for (size_t i = 0; i < BLE_MAX_BONDS; i++)
		if (!bleData.bonds[i].valid)
			return (bleData.bonds[i].valid = 1), &bleData.bonds[*number = i];

	// reclaim oldest bond
	// (ideally we'd reclaim the bond that was unused for the longest time, but this will do)
	ble_bond_t *bond = &bleData.bonds[*number = bleData.oldestBond++];
	if (bleData.oldestBond >= BLE_MAX_BONDS)
		bleData.oldestBond = 0;

	// remove the bond from any open connection it may belong to
	for (size_t i = 0; i < BLE_MAX_CONNECTIONS; i++)
		if (bleData.connections[i].bond == bond)
			bleData.connections[i].bond = NULL;

	return (bond->valid = 1), bond;
}

// Returns a bond structure identified by the bonded address.
// Returns NULL if no match could be made.
//	*number: set to the index of the bond structure that was returned
ble_bond_t *ble_recall_bond(TYPED_BD_ADDR_T *addr, size_t *number) {
	for (size_t i = 0; i < BLE_MAX_BONDS; i++)
		if (bleData.bonds[i].valid)
			if (!MemCmp(&bleData.bonds[i].addr, addr, sizeof(TYPED_BD_ADDR_T)))
				return &bleData.bonds[i];
	return NULL;
}


// Saves data associated with a bond to NVM.
//	bondNum: index of the bond to be saved
void ble_save_bond(size_t bondNum) {
	nvm_write(NVM_BLE_BONDS_OFFSET * BLE_MAX_BONDS + bondNum * sizeof(ble_bond_t) * WORDSIZE, (char *)&(bleData.bonds[bondNum]), sizeof(ble_bond_t) * WORDSIZE);
}





// Handler for the timer 
void ble_request_conn_param_update(void *context) {
	// Application specific preferred paramters
	ble_con_params app_pref_conn_param = { 
		BLE_PREFERRED_MIN_CON_INTERVAL, 
		BLE_PREFERRED_MAX_CON_INTERVAL, 
		BLE_PREFERRED_SLAVE_LATENCY, 
		BLE_PREFERRED_SUPERVISION_TIMEOUT 
	};
	sys_status status;

	ble_connection_t *connection = (ble_connection_t *)context;
	if (!connection)
		return;

	// Send Connection Parameter Update request using application specific preferred connection paramters
	if ((status = LsConnectionParamUpdateReq(&connection->addr, &app_pref_conn_param)) != ls_err_none)
		bug_check(STATUS_BLE_CON_PARAM_UPDATE, status);

	// Increment the count for Connection Parameter Update requests
	connection->connUpdateReqNum++;
}


// Starts to negotiate connection parameters if they are currently not within range.
// The maximum number of negotiation attempts is defined by a macro.
void ble_negotiate_params(ble_connection_t *connection) {
	if (connection->connInterval < BLE_PREFERRED_MIN_CON_INTERVAL || connection->connInterval > BLE_PREFERRED_MAX_CON_INTERVAL
#if BLE_PREFERRED_SLAVE_LATENCY
		|| connection->connLatency < BLE_PREFERRED_SLAVE_LATENCY
#endif
		) {
		// Set the num of conn update attempts to zero and start timer to trigger Connection Paramter Update procedure
		connection->connUpdateReqNum = 0;
		connection->connUpdateReqTimer = (timer_t) CREATE_TIMER(BLE_GAP_CONN_PARAM_TIMEOUT, ble_request_conn_param_update, connection);
		timer_start(&(connection->connUpdateReqTimer));
	}
}







// ***************** LINK MANAGER EVENT HANDLERS *******************


// Connection established, store the connection parameters.
void ble_lm_connection_completed(LM_EV_CONNECTION_COMPLETE_T *event_data) {
	ble_connection_t *connection = ble_recall_connection(event_data->data.connection_handle);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->data.connection_handle);

	connection->connInterval = event_data->data.conn_interval;
	connection->connLatency = event_data->data.conn_latency;
	connection->connTimeout = event_data->data.supervision_timeout;
}


#ifdef USING_BLE_PERIPHERAL
// Connection changed, store the connection parameters.
void ble_lm_connection_updated(LM_EV_CONNECTION_UPDATE_T* event_data) {
	ble_connection_t *connection = ble_recall_connection(event_data->data.connection_handle);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->data.connection_handle);

	// Store the new connection parameters.
	connection->connInterval = event_data->data.conn_interval;
	connection->connLatency = event_data->data.conn_latency;
	connection->connTimeout = event_data->data.supervision_timeout;
}
#endif



// Encryption did change
void ble_lm_encryption_changed(void *event_data) {
#ifdef USING_BLE_CENTRAL // nothing to do on peripheral
#error "todo: implement"
#endif
}



// Connection closed. The app will either enter idle or advertising state depending on the disconnect reason.
void ble_lm_connection_closed(HCI_EV_DATA_DISCONNECT_COMPLETE_T *event_data) {

#ifdef USING_BLE_CENTRAL
#error "todo: implement"
#endif


	if (shuttingDown)
		__reset();


	ble_connection_t *connection = ble_recall_connection(event_data->handle);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->handle);

	// LM_EV_DISCONNECT_COMPLETE event can have following disconnect reasons:
	//
	// HCI_ERROR_CONN_TIMEOUT - Link Loss case
	// HCI_ERROR_CONN_TERM_LOCAL_HOST - Disconnect triggered by device
	// HCI_ERROR_OETC_* - Other end (i.e., remote host) terminated connection

	// Handling signal as per current state
	
	// invalidate connection data
	connection->id = GATT_INVALID_UCID;
	bleData.connectionCount--;

	// we might actually disable ads in some cases but let's be safe
	ble_advertise(ble_state_fast_advertising);
}




// ***************** LINK SUPERVISOR EVENT HANDLERS *******************



// Connection parameters are being updated
void ble_ls_conn_param_update_indication(LS_CONNECTION_PARAM_UPDATE_IND_T *event_data) {
	ble_connection_t *connection = ble_recall_connection_by_addr(&event_data->address);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, (uintptr_t)&event_data->address);

	// Delete timer if running
	timer_stop(&(connection->connUpdateReqTimer));
	
	// The application has already received the new connection
	// parameters while handling event LM_EV_CONNECTION_UPDATE.
	// Check if new parameters comply with application preferred
	// parameters. If not, application shall trigger Connection
	// parameter update procedure
}



void ble_ls_conn_param_update_completed(LS_CONNECTION_PARAM_UPDATE_CFM_T *event_data) {
#ifdef USING_BLE_CENTRAL
#error "todo: implement"
#endif

	ble_connection_t *connection = ble_recall_connection_by_addr(&event_data->address);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, (uintptr_t)&event_data->address);
	
	// Received in response to the L2CAP_CONNECTION_PARAMETER_UPDATE request sent from the slave after encryption is enabled. 
	// If the request has failed, the device should again send the same request only after Tgap(conn_param_timeout).
	// Refer Bluetooth 4.0 spec Vol 3 Part C, Section 9.3.9 and profile spec.

	if ((event_data->status != ls_err_none) && (connection->connUpdateReqNum++ < BLE_MAX_NUM_CONN_PARAM_UPDATE_REQS)) {
		// restart timer
		timer_stop(&connection->connUpdateReqTimer);
		connection->connUpdateReqTimer = (timer_t) CREATE_TIMER(BLE_GAP_CONN_PARAM_TIMEOUT, ble_request_conn_param_update, connection);
		timer_start(&(connection->connUpdateReqTimer));
	}
}




// ***************** SECURITY MANAGER EVENT HANDLERS *******************


void ble_sm_diversifier_approval_indication(SM_DIV_APPROVE_IND_T *event_data) {
	ble_connection_t *connection = ble_recall_connection(event_data->cid);
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, event_data->cid);

	sm_div_verdict approve_div = SM_DIV_REVOKED;

	// Check whether the bond still exists.
	if (connection->bond)
		if (connection->bond->diversifier == event_data->div)
			approve_div = SM_DIV_APPROVED;

	SMDivApproval(event_data->cid, approve_div);
}



void ble_sm_keys_indication(SM_KEYS_IND_T *event_data) {
	ble_connection_t *connection = ble_recall_connection_by_addr(&event_data->remote_addr); // does this require resolving?
	if (!connection)
		bug_check(STATUS_BLE_INVALID_CONNECTION, (uintptr_t)&event_data->remote_addr);

	// create new bond, the bond will be stored to NVM after pairing is complete
	connection->bond = ble_alloc_bond(&connection->bondNum);

	connection->bond->gattClientConfig = 0;
	connection->bond->gattChanged = 0;

	// Store the diversifier which will be used for accepting/rejecting the encryption requests.
	connection->bond->diversifier = event_data->keys->div;

	// Store IRK if the connected host is using random resolvable address.
	// IRK is used afterwards to validate the identity of connected host.
	if (ble_gatt_is_address_resolvable_random(&connection->addr))
		MemCopy(connection->bond->irk, event_data->keys->irk, BLE_IRK_LENGTH >> 1);
}



void ble_sm_simple_pairing_completed(SM_SIMPLE_PAIRING_COMPLETE_IND_T *event_data) {
	ble_connection_t *connection = ble_recall_connection_by_addr(&event_data->bd_addr); // does this require resolving?
	if (!connection)
		return; // Firmware may send this signal after disconnection. So don't panic but ignore this signal.
	if (!connection->bond)
		return; // if this connection was not opened for a bond (in SM_KEYS_IND or on connection), ignore this signal

	sys_status status;

	if (event_data->status == sys_status_success) {
		connection->bond->addr = event_data->bd_addr;

		// Configure white list with the Bonded host address only if the connected host doesn't support random resolvable address
		if (!ble_gatt_is_address_resolvable_random(&connection->addr)) {
			// It is important to note that this application
			// doesn't support reconnection address. In future, if
			// the application is enhanced to support Reconnection
			// Address, make sure that we don't add reconnection
			// address to white list
			if ((status = LsAddWhiteListDevice(&connection->bond->addr)) != ls_err_none)
				bug_check(STATUS_BLE_WHITELIST, status);
		}

		// todo: notify services that we're bonded now

	} else {
	
		// If application is already bonded to this host and pairing fails, remove device from the white list.
		if ((status = LsDeleteWhiteListDevice(&connection->bond->addr) != ls_err_none))
			bug_check(STATUS_BLE_WHITELIST, status);

		// invalidate this bond
		connection->bond->valid = FALSE;
		connection->bond = NULL;
	}


	// Update bond information in NVM
	ble_save_bond(connection->bondNum);
}



#endif // defined(USING_BLE_CENTRAL) || defined(USING_BLE_PERIPHERAL)
