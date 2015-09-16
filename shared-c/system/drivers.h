/*
*
*
* created: 02.02.15
*
*/

#ifndef __DRIVERS_H__
#define __DRIVERS_H__



// The driver type determines what a driver can be attached to.
// E.g. a volume driver can be attached to a volume and will most likely provide a file system.
typedef enum
{
	DRIVER_TYPE_VOLUME		// driver: pointer to fs_t, identifier: pointer to 8 bytes in the VBR, initProc args: volume_t*, the first 512 bytes
} driver_type_t;


// A driver is defined by its initialization procedure, which returns a driver context upon invokation.
// The arguments and return value of this procedure is specific to each driver type but consistent accross one type.
typedef struct driver_t
{
	status_t(*initProc)(void);	// an initialization procedure that returns a driver context of some form
	struct driver_t *next;		// the next driver of the same type
} driver_t;


typedef struct driver_list_t
{
	driver_type_t type;
	driver_t *head;
	struct driver_list_t *next;
} driver_list_t;


status_t driver_register(driver_t *driver, driver_type_t type);
driver_t* driver_getlist(driver_type_t type);



#endif
