/*
* ble.h
*
* Created: 24.03.2014
*  Author: samuel
*
*/


#ifndef __BLUETOOTH_LE_H__
#define __BLUETOOTH_LE_H__

#include "timer.h"
#include <system/time.h>


#define GATT_INVALID_UCID					(0xFFFF)
#define INVALID_ATT_HANDLE                  (0x0000) // Invalid Attribute Handle
#define AD_TYPE_APPEARANCE                  (0x19) // AD Type for Appearance

#define BLE_IRK_LENGTH						(16)


#define LE8_L(x)                             ((x) & 0xff) // Extract low order byte of 16-bit UUID
#define LE8_H(x)                             (((x) >> 8) & 0xff) // Extract high order byte of 16-bit UUID



// GAP appearance values
// For values, refer http://developer.bluetooth.org/gatt/characteristics/Pages/CharacteristicViewer.aspx?u=org.bluetooth.characteristic.gap.appearance.xml
#define BLE_APPEARANCE_UNKNOWN                0x0000 // unknown
#define BLE_APPEARANCE_PHONE                  0x0040 // generic phone
#define BLE_APPEARANCE_COMPUTER               0x0080 // generic computer
#define BLE_APPEARANCE_WATCH                  0x00C0 // generic watch
#define BLE_APPEARANCE_HID                    0x03C0 // generic HID
#define BLE_APPEARANCE_KEYBOARD               0x03C1 // HID: keyboard
#define BLE_APPEARANCE_MOUSE                  0x03C2 // HID: mouse
#define BLE_APPEARANCE_TAG                    0x0200 // generic tag
#define BLE_APPEARANCE_HR_SENSOR              0x0340 // generic heart rate sensor
#define BLE_APPEARANCE_THERMOMETER            0x0300 // generic thermometer


#define UUID_FLIGHT_SERVICE						0x1337 // todo: adjust




#ifdef USING_BLE_CENTRAL
// Profiles discovered on the remote (connected) device
typedef enum {
	ans_supported = 0x0001, // Alert notification service
	pas_supported = 0x0002, // phone alert status service
	// todo: handle other profiles (this is a bitmask)
} ble_profile_t;
#endif


typedef enum
{
	ble_state_init = 0,					// Initial state
	ble_state_fast_advertising,			// Fast undirected advertisements configured
	ble_state_slow_advertising,			// Slow undirected advertisements configured
	ble_state_connected,				// Application has established connection to the host
	ble_state_connected_discovering,	// Enters when application starts primary service discovery (central only)
	ble_state_disconnecting,			// Enters when disconnect is initiated by the application
	ble_state_idle,						// Idle state
} ble_state_t;


typedef struct
{
	bool valid;							// if 0, all other fields in this struct are undefined
	TYPED_BD_ADDR_T	addr;				// bluetooth address of the host
	uint16_t irk[BLE_IRK_LENGTH >> 1];	// identity resolving key
	uint16_t diversifier;				// diversifier associated with the long term key
	uint16_t gattClientConfig;			// indicates whether the peer wishes to be notified when the device changed its services
	uint16_t gattChanged;				// set to true if the device boots into a different application
} ble_bond_t; // size: 34 bytes

typedef struct
{
	uint16_t id;						// connection ID (if GATT_INVALID_UCID, all other fields in this struct are undefined)
	TYPED_BD_ADDR_T	addr;				// address of the connected host
	
	uint8_t connUpdateReqNum;			// variable to keep track of number of connection parameter update request made
	timer_t connUpdateReqTimer;		// used to repeat a failed connection update request after some time

	uint16_t connInterval;				// connection interval
	uint16_t connLatency;				// slave latency
	uint16_t connTimeout;				// connection timeout value

	ble_bond_t *bond;					// points to the bond data in case the peer of this connection is a device we're bonded with (NULL otherwise)
	size_t bondNum;						// refers to the index of the bond (only valid if bond is not NULL)

#ifdef USING_BLE_CENTRAL
	ble_profile_t serverProfiles;		// Profiles supported by the remote (connected) device
#endif
} ble_connection_t;


typedef enum
{
	ble_adv_fast,
	ble_adv_slow,
	ble_adv_none
} ble_adv_mode_t;


typedef struct
{
	ble_adv_mode_t advMode;		// current advertising mode
	timer_t advTimer;			// used for advertising timeout

	ble_connection_t connections[BLE_MAX_CONNECTIONS];	// list of connections
	size_t connectionCount;								// number of open connections

	ble_bond_t bonds[BLE_MAX_BONDS];					// current bonds (a copy is stored in NVM)
	size_t oldestBond;									// refers to the bond that will be discarded next (must be directly after the bonds field)

	// Note that the sets of connected and bonded devices are distinct.
	// This device may be bonded to devices that are currently not connected
	// and connected to devices to which it is not bonded.
} ble_data_t;




extern ble_data_t bleData;



void ble_init_nvm(void);
void ble_init(void);
void ble_start(void);

ble_connection_t *ble_alloc_connection(void);
ble_connection_t *ble_recall_connection(uint16_t id);
ble_connection_t *ble_recall_connection_by_addr(TYPED_BD_ADDR_T *addr);
ble_bond_t *ble_alloc_bond(size_t *number);
ble_bond_t *ble_recall_bond(TYPED_BD_ADDR_T *addr, size_t *number);
void ble_save_bond(size_t bondNum);

void ble_negotiate_params(ble_connection_t *connection);


// functions called by AppProcessLmEvent
void ble_lm_connection_completed(LM_EV_CONNECTION_COMPLETE_T *event_data);
void ble_lm_connection_updated(LM_EV_CONNECTION_UPDATE_T* event_data);
void ble_lm_encryption_changed(void *event_data);
void ble_lm_connection_closed(HCI_EV_DATA_DISCONNECT_COMPLETE_T *event_data);
void ble_ls_conn_param_update_indication(LS_CONNECTION_PARAM_UPDATE_IND_T *event_data);
void ble_ls_conn_param_update_completed(LS_CONNECTION_PARAM_UPDATE_CFM_T *event_data);
void ble_sm_diversifier_approval_indication(SM_DIV_APPROVE_IND_T *event_data);
void ble_sm_keys_indication(SM_KEYS_IND_T *event_data);
void ble_sm_simple_pairing_completed(SM_SIMPLE_PAIRING_COMPLETE_IND_T *event_data);


#endif /* __BLUETOOTH_LE_H__ */
