/*
*
* Provides reading and editing the device name.
*
* created: 21.02.15
*
*/

#include <system.h>


#if defined(USING_GAP_SERVICE) && defined(BLE_CUSTOM_DEVICE_NAME)
#	error "not implemented"

status_t gap_name_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	size_t length;
	char *name = ble_get_name_and_length(&length);
	memcpy(buf, name + offset, (*count = min(length - offset, *count)));
	return STATUS_SUCCESS;
}


status_t gap_name_handler_w(void *connection, size_t offset, char *buf, size_t count) {
	// todo: update device name in NVM and make this characteristic writable
	//updateDeviceName(data->size_value, data->value);
	return STATUS_SUCCESS;
}


DEFINE_ENDPOINT(gap_name, gap_name_handler_r, gap_name_handler_w, NULL); // todo: use completition handler to write to NVM


#elif defined(BLE_CUSTOM_DEVICE_NAME)
#	error "the gap service must be enabled to enable an editable device name"
#endif // USING_GAP_SERVICE
