/*
*
*
* created: 03.03.15
*
*/

#ifndef __SERVICES_H__
#define __SERVICES_H__

// Represents an endpoint by the functions that are used to interface with the endpoint.
// For each function, the connection parameter identifies the connection
// (the type depends the physical layer that is used).
// Any of the functions can be NULL.
typedef struct endpoint_t
{
	// Read callback - called when a client reads from the attribute
	//	offset: the byte offset where the client wishes to read from
	//	buf: shall be filled with (packed) data from the attribute
	//	count:	input: the size of the buffer (in bytes)
	//			output: the number of bytes actually written
	// If NULL, the attribute is read protected.
	status_t(*read)(void *connection, size_t offset, char *buf, size_t *count);

	// Write callback - invoked when the client writes to the attribute
	//	offset: the byte offset where the client wishes to write to
	//	buf: the (packed) data buffer
	//	count: length in bytes of the buffer (does not exceed BLE_MAX_PACKET_SIZE)
	// If NULL, the attribute is write protected.
	status_t(*write)(void *connection, size_t offset, char *buf, size_t count);

	// Write complete callback - invoked after the last write of a transaction was completed
	// This callback can be used to carry out follow up tasks after an
	// attribute was edited. In most cases, the returned status code is not checked.
	status_t(*writeComplete)(void *connection);
} endpoint_t;


// Defines a combination of functions as a named endpoint.
// This macro is intended to be used in the file where the functions are defined.
// An endpoint defined in this way should be exported in a header file using EXPORT_ENDPOINT.
#define DEFINE_ENDPOINT(_name, _readCallback, _writeCallback, _writeCompleteCallback)	\
endpoint_t _name ## _endpoint = {														\
	.read = (_readCallback),															\
	.write = (_writeCallback),															\
	.writeComplete = (_writeCompleteCallback)											\
}


// Exports an endpoint that was defined using DEFINE_ENDPOINT.
// This macro is intended to be used in a header file.
// An endpoint exported in this way can be accessed by using the according [...]_IMPORT_ENDPOINT macro
// for the physical layer that the endpoint should be published on.
#define EXPORT_ENDPOINT(_name) extern endpoint_t _name ## _endpoint



#ifdef USING_BLE_PERIPHERAL
#	include "gatt_service.h"	// bluetooth GATT
#	include "gap_service.h"	// bluetooth GAP (device name)
#endif

#ifdef USING_DFU
#	include "dfu_service.h"	// device firmware update
#endif

#ifdef USING_I2C_SERVICE
#	include "i2c_service.h"	// direct I2C access
#endif

#ifdef USING_MPU_SERVICE
#	include "mpu_service.h"	// motion data
#endif

#ifdef USING_UAV_SERVICE
#include "uav_service.h"	// flight control
#endif



#endif // __SERVICES_H__
