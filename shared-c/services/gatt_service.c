/*
*
* Provides a BLE service to ensures that peers learn about changes
* in the services offered by this device.
*
* created: by CSR
*
*/


#include <system.h>


#ifdef USING_GATT_SERVICE

// Must be called when a connection was established, so that the host can be
// informed about service changes since the last connection.
void gatt_service_on_connection(ble_connection_t *connection) {
	if (!connection->bond)
		return;

	uint8 serviceChangedData[] = { 0x05, 0x00, 0xff, 0xff };

	// Check if this host has subscribed to service changes
	if (connection->bond->gattClientConfig == 1)
		GattCharValueNotification(connection->id, HANDLE_SERVICE_CHANGED, sizeof(serviceChangedData), serviceChangedData);
	if (connection->bond->gattClientConfig == 2)
		GattCharValueIndication(connection->id, HANDLE_SERVICE_CHANGED, sizeof(serviceChangedData), serviceChangedData);

	// reset service changed flag
	connection->bond->gattChanged = 0;
	ble_save_bond(connection->bondNum);
}


// Must be called when the device is rebooted into another application.
// If this is not called, any bonded device that reconnects will think the device
// still has the same features as before.
void gatt_register_service_change(void) {
	for (size_t i = 0; i < BLE_MAX_BONDS; i++) {
		bleData.bonds[i].gattChanged = 1;
		ble_save_bond(i);
	}
}




status_t gatt_service_changed_client_config_handler_r(void *_connection, size_t offset, char *buf, size_t *count) {
	ble_connection_t *connection = _connection;

	if (offset || *count < 2)
		return STATUS_ERROR;

	if (connection->bond)
		*(uint16_t *)buf = connection->bond->gattClientConfig;
	else
		*(uint16_t *)buf = 0;
	*count = 2;

	return STATUS_SUCCESS;
}

status_t gatt_service_changed_client_config_handler_w(void *_connection, size_t offset, char *buf, size_t count) {
	ble_connection_t *connection = _connection;

	if (offset || count < 2)
		return STATUS_ERROR;

	if (connection->bond)
		connection->bond->gattClientConfig = *(uint16_t *)buf;

	return STATUS_SUCCESS;
}


DEFINE_ENDPOINT(gatt, gatt_service_changed_client_config_handler_r, gatt_service_changed_client_config_handler_w, NULL);

#endif // USING_GATT_SERVICE
