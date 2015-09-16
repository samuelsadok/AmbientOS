/*
*
*
* created: 04.03.15
*
*/


/*

endpoints:
 - info:
	read: reports the DFU version, state (normal/DFU) and application state
	write: puts the device into DFU/normal mode
 - platform
 - version: reports the current application version (only available in normal mode)
 - app: maps to application memory (only available in DFU mode)
*/


#ifdef USING_DFU

#include <system.h>
#include "system/dfu.h"
#include "dfu_service.h"


#ifdef DFU_SLAVES
i2c_device_t *currentSlave = NULL;
#endif



/* DFU info endpoint */

status_t EXTENDED_TEXT dfu_info_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	if (offset >= sizeof(dfu_info_t) * WORDSIZE)
		return STATUS_OUT_OF_RANGE;

	dfu_info_t info = {
		.version = DFU_VERSION,
		.state = DFU_STATE_NONE
	};

#ifdef USING_BOOTLOADER
	if (dfuOnHold)
		info.state = DFU_STATE_HOLD;
	else
		info.state = (dfu_check_for_app() == STATUS_SUCCESS ? DFU_STATE_TEMP : DFU_STATE_PERMA);
#endif

	info.domain = dfuSlaveCount + 1;

	memcpy(buf, ((char *)&info) + offset / WORDSIZE, *count = min(*count, sizeof(dfu_info_t) * WORDSIZE - offset));

	return STATUS_SUCCESS;
}


dfu_info_t newInfo;

status_t EXTENDED_TEXT dfu_info_handler_w(void *connection, size_t offset, char *buf, size_t count) {
	if (offset || count != sizeof(dfu_info_t) * WORDSIZE)
		return STATUS_INVALID_OPERATION;
	newInfo = *(dfu_info_t *)buf;
	return STATUS_SUCCESS;
}

status_t EXTENDED_TEXT dfu_info_handler_c(void *connection) {

	if (newInfo.domain != 0xFFFF) {
		if (newInfo.domain > dfuSlaveCount)
			return STATUS_OUT_OF_RANGE;
#ifdef DFU_SLAVES
		currentSlave = (newInfo.domain ? &dfuSlaves[newInfo.domain - 1] : NULL);
#endif
		return STATUS_SUCCESS;
	}

	switch (newInfo.state) {
		case DFU_STATE_NONE:
#ifdef USING_BOOTLOADER
			if (dfuOnHold)
				return STATUS_INVALID_OPERATION;
			if (dfu_check_for_app() != STATUS_SUCCESS)
				return STATUS_INVALID_OPERATION;
			return dfu_try_launch_app(); // does usually not return
#else
			return STATUS_SUCCESS;
#endif

		case DFU_STATE_PERMA:
			dfu_invalidate_app();
			/* fallthrough */
		case DFU_STATE_TEMP:
#ifdef USING_BOOTLOADER
			return STATUS_SUCCESS;
#else
			dfu_launch_bootloader(); // does not return
#endif

		default:
			return STATUS_INVALID_OPERATION;
	}
}




/* Universal string endpoint */

status_t EXTENDED_TEXT dfu_length_handler_r(const_unicode_t *str, size_t offset, char *buf, size_t *count, uint32_t slaveRegLen) {
	if (offset || *count < 4)
		return STATUS_OUT_OF_RANGE;
	*count = 4;

#ifdef DFU_SLAVES
	// fetch length from slave
	if (currentSlave)
		return i2c_master_read(currentSlave, slaveRegLen, buf, *count);
#endif

	// return local length
	*(uint32_t *)buf = (uint32_t)str->length;
	return STATUS_SUCCESS;
}

status_t EXTENDED_TEXT dfu_string_handler_r(const_unicode_t *str, size_t offset, char *buf, size_t *count, uint32_t slaveRegData, uint32_t slaveRegLen) {
	if (offset)
		return STATUS_OUT_OF_RANGE;

#ifdef DFU_SLAVES
	// fetch string from DFU slave
	if (currentSlave) {
		uint32_t length;
		status_t status;
		if ((status = i2c_master_read(currentSlave, slaveRegLen, (char *)&length, 4)))
			return status;
		return i2c_master_read(currentSlave, slaveRegData, buf, *count = min(*count, length * 2));
	}
#endif

	// copy from local string
	memcpy(buf, str->data, *count = min(*count, str->length * sizeof(wchar_t) * WORDSIZE));
	return STATUS_SUCCESS;
}


/* Platform identifier endpoint */

status_t EXTENDED_TEXT dfu_platform_length_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_length_handler_r(&platformStr, offset, buf, count, DFU_PLATFORM_LENGTH_REG);
}

status_t EXTENDED_TEXT dfu_platform_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_string_handler_r(&platformStr, offset, buf, count, DFU_PLATFORM_REG, DFU_PLATFORM_LENGTH_REG);
}


/* Application name endpoint */

status_t EXTENDED_TEXT dfu_appname_length_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_length_handler_r(&appNameStr, offset, buf, count, DFU_APPNAME_LENGTH_REG);
}

status_t EXTENDED_TEXT dfu_appname_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_string_handler_r(&appNameStr, offset, buf, count, DFU_APPNAME_REG, DFU_PLATFORM_LENGTH_REG);
}


/* Current firmware version (build time) endpoint */

status_t EXTENDED_TEXT dfu_version_length_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_length_handler_r(&versionStr, offset, buf, count, DFU_VERSION_LENGTH_REG);
}

status_t EXTENDED_TEXT dfu_version_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	return dfu_string_handler_r(&versionStr, offset, buf, count, DFU_VERSION_REG, DFU_PLATFORM_LENGTH_REG);
}


#ifdef USING_BOOTLOADER


bool didInvalidate = 0; // indicates if the local app has been invalidated

// variables used to write to local program memory
size_t currentPage = 0;
size_t currentPageOffset = 0;
char pageBuffer[PMEM_PAGESIZE / WORDSIZE];


/* Program memory info endpoint */

typedef struct
{
	uint32_t size;			// read: maximum application size on bytes, write: actual app size
	uint32_t pagesize;		// granularity for write operations
	uint32_t checksum;		// read: the checksum method used by this device, write: actual checksum
} progmem_info_t;

// Returns program memory parameters.
status_t EXTENDED_TEXT dfu_progmem_info_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
	if (offset)
		return STATUS_INVALID_OPERATION;

	*count = min(*count, sizeof(progmem_info_t) * WORDSIZE);

#ifdef DFU_SLAVES
	if (currentSlave)
		return i2c_master_read(currentSlave, DFU_PROGMEM_INFO_REG, buf, *count);
#endif

	progmem_info_t info = {
		.size = APP_MAX_SIZE,
		.pagesize = PMEM_PAGESIZE,
		.checksum = PMEM_CHECKSUM_METHOD
	};

	memcpy(buf, (char *)&info, *count);

	return STATUS_SUCCESS;
}

progmem_info_t stagedProgmemInfo;

// Issues an application validation by providing the size of the application and a checksum.
// If the check succeeds, the application on the DFU target is marked valid, so the device can
// exit DFU mode again.
status_t EXTENDED_TEXT dfu_progmem_info_handler_w(void *connection, size_t offset, char *buf, size_t count) {
	if (offset || count != sizeof(progmem_info_t) * WORDSIZE)
		return STATUS_INVALID_OPERATION;

	stagedProgmemInfo = *(progmem_info_t *)buf;

	return STATUS_SUCCESS;
}

// Executed the application validation
status_t EXTENDED_TEXT dfu_progmem_info_handler_c(void *connection) {
	currentPageOffset = 0;
	status_t status = dfu_validate_app(stagedProgmemInfo.size, stagedProgmemInfo.checksum);
	if (!status) didInvalidate = 0;
	return status;
}



/* Direct progmem access endpoint */

// Reads from an arbitrary position in the program memory.
// This works even if there is no valid application present.
// The read does not have to be page aligned but should be WORDSIZE aligned.
status_t EXTENDED_TEXT dfu_progmem_handler_r(void *connection, size_t offset, char *buf, size_t *count) {
#ifdef DFU_SLAVES
	if (currentSlave)
		return i2c_master_read(currentSlave, offset, buf, *count);
#endif

	if (offset + *count > APP_MAX_SIZE)
		return STATUS_OUT_OF_RANGE;
	return pmem_read(APP_OFFSET + offset, buf, *count);
}


// Writes to program memory.
// Writing is only possible in multiples of the page size granularity. A write can be splitted
// into multiple writes of arbitrary size as long as they are issued consecutively.
// Writing to this endpoint immediately invalidates the application on the DFU target.
status_t EXTENDED_TEXT dfu_progmem_handler_w(void *connection, size_t offset, char *buf, size_t count) {
#ifdef DFU_SLAVES
	if (currentSlave)
		return i2c_master_write(currentSlave, offset, buf, count);
#endif

	if (!didInvalidate) {
		dfu_invalidate_app();
		didInvalidate = 1;
	}

	status_t status;

	while (count) {
		// param checks:
		//  - if the previous page write wasn't completed, offset must continue from where the last write left off.
		//  - else the offset must be page aligned
		if ((currentPageOffset % PMEM_PAGESIZE) != (offset % PMEM_PAGESIZE))
			return STATUS_INVALID_OPERATION;
		else if (!(offset % PMEM_PAGESIZE))
			currentPage = offset / PMEM_PAGESIZE;
		else if (offset / PMEM_PAGESIZE != currentPage)
			return STATUS_INVALID_OPERATION;

		// write at most until the page buffer is full or no more data is left
		size_t writeBytes = min(PMEM_PAGESIZE - currentPageOffset, count);
		memcpy(pageBuffer + currentPageOffset / WORDSIZE, buf, writeBytes);
		offset += writeBytes;
		buf += writeBytes / WORDSIZE;
		count -= writeBytes;

		// if we're at the end of the page buffer, write to program memory
		if (!((currentPageOffset += writeBytes) % PMEM_PAGESIZE))
			if ((status = pmem_write_page(currentPage, pageBuffer)))
				return status;
	}

	return STATUS_SUCCESS;
}

#endif



DEFINE_ENDPOINT(dfu_info, dfu_info_handler_r, dfu_info_handler_w, dfu_info_handler_c);

// the following endpoints always map to the current domain
DEFINE_ENDPOINT(dfu_platform_length, dfu_platform_length_handler_r, NULL, NULL);
DEFINE_ENDPOINT(dfu_platform, dfu_platform_handler_r, NULL, NULL);
DEFINE_ENDPOINT(dfu_appname_length, dfu_appname_length_handler_r, NULL, NULL);
DEFINE_ENDPOINT(dfu_appname, dfu_appname_handler_r, NULL, NULL);
DEFINE_ENDPOINT(dfu_version_length, dfu_version_length_handler_r, NULL, NULL);
DEFINE_ENDPOINT(dfu_version, dfu_version_handler_r, NULL, NULL);
#ifdef USING_BOOTLOADER
DEFINE_ENDPOINT(dfu_progmem_info, dfu_progmem_info_handler_r, dfu_progmem_info_handler_w, dfu_progmem_info_handler_c);
DEFINE_ENDPOINT(dfu_progmem, dfu_progmem_handler_r, dfu_progmem_handler_w, NULL);
#endif

#endif // USING_DFU
