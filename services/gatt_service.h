/*
*
*
* created: by CSR
*
*/

#ifndef __GATT_SERVICE_H__
#define __GATT_SERVICE_H__


void gatt_service_on_connection(ble_connection_t *connection);
void gatt_register_service_change(void);

EXPORT_ENDPOINT(gatt);


#endif // __GATT_SERVICE_H__
