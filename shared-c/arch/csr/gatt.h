/*
* gatt.h
*
* Created: 28.03.2014
*  Author: samuel
*
*/


#ifndef __BLE_GATT_H__
#define __BLE_GATT_H__


char* ble_get_name_and_length(size_t *length);
bool ble_gatt_is_address_resolvable_random(TYPED_BD_ADDR_T *addr);

void ble_advertise(ble_adv_mode_t mode);
void ble_disconnect(ble_connection_t *connection);

// functions called by AppProcessLmEvent
void ble_gatt_add_db_completed(GATT_ADD_DB_CFM_T *event_data);
void ble_gatt_connect_completed(GATT_CONNECT_CFM_T *event_data);
void ble_gatt_connect_cancelled(void);
void ble_gatt_access_indication(GATT_ACCESS_IND_T *event_data);


#endif /* __BLE_GATT_H__ */
