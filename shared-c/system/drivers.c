/*
*
*
* created: 02.02.15
*
*/

#include <system.h>
#include "drivers.h"


// A list of driver lists, grouped by their type
driver_list_t *drivers;


// Registers a driver for the specified type.
// The structure must be initialized (except for the next field).
status_t driver_register(driver_t *driver, driver_type_t type) {

	// find the right list or create new list
	driver_list_t *currentList;
	for (currentList = drivers; currentList; currentList = currentList->next)
		if (currentList->type == type)
			break;

	if (!currentList) {
		currentList = (driver_list_t *)malloc(sizeof(driver_list_t));
		if (!currentList) return STATUS_OUT_OF_MEMORY;
		currentList->type = type;
		currentList->head = NULL;
		currentList->next = drivers;
		drivers = currentList;
	}

	// prepend driver to new list
	driver->next = currentList->head;
	currentList->head = driver;
	return STATUS_SUCCESS;
}

// Returns the head of a linked list that holds all drivers of the specified type.
// The caller can traverse the list and try to init each driver until it succeeds.
// An initialization procedure may return STATUS_INCOMPATIBLE if it is attached in the wrong context,
// e.g. if an NTFS driver is attached to a FAT volume.
// Returns NULL if the list is empty.
driver_t* driver_getlist(driver_type_t type) {
	for (driver_list_t *currentList = drivers; currentList; currentList = currentList->next)
		if (currentList->type == type)
			return currentList->head;
	return NULL;
}
