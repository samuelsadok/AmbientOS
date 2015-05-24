/*
*
*
*/

#ifndef __SYSTEM_H__
#define __SYSTEM_H__



#include <stdint.h>
#include <stdbool.h>
#include <stddef.h>
#include <stdarg.h>
#include <stdlib.h>
#include <limits.h>


#ifndef __INT8_T__
typedef signed char int8_t;
#define __INT8_T__
#endif
#ifndef __UINT8_T__
typedef unsigned char uint8_t;
#define __UINT8_T__
#endif


// determine how many bytes one address location holds
#if __SIZEOF_SHORT__ == 2
#	define WORDSIZE		(1)
#elif __SIZEOF_SHORT__ == 1
#	define WORDSIZE		(2)
#else
#	error "could not determine word size"
#endif



#define CONCAT_(a, b) a##b
#define CONCAT(a, b) CONCAT_(a, b)

/* These can't be used after statements in c89. */
#ifdef __COUNTER__
#define STATIC_ASSERT(e,m) \
    ;enum { CONCAT(static_assert_, __COUNTER__) = 1/(!!(e)) }
#else
/* This can't be used twice on the same line so ensure if using in headers
* that the headers are not included twice (by wrapping in #ifndef...#endif)
* Note it doesn't cause an issue when used on same line of separate modules
* compiled with gcc -combine -fwhole-program.  */
#define STATIC_ASSERT(e,m) \
    ;enum { CONCAT(assert_line_, __LINE__) = 1/(!!(e)) }
#endif






//#if UINT64_MAX != 0xFFFFFFFFFFFFFFFFUL
//#	error "uin64_t"
//#endif

// Returns a 64-bit integer where the specified number of bits are set to one (starting at the LSB)
#define BITMASK64(bits)			(UINT64_C(0xFFFFFFFFFFFFFFFF) >> (64 - (bits)))

// Rounds up so that the specified number of bits are zero
static inline uint64_t round_up(uint64_t val, uint64_t bits) {
	if (!(val & BITMASK64(bits))) return val;
	return (((val >> bits) + 1) << bits);
}

// Rounds down so that the specified number of bits are zero
static inline uint64_t round_down(uint64_t val, uint64_t bits) {
	return (val & ~BITMASK64(bits));
}


typedef enum status_t
{
	/* generic status codes */
	STATUS_SUCCESS = 0,
	STATUS_ERROR = 1,
	STATUS_IN_PROGRESS,					// this is in most cases no error
	STATUS_TIMEOUT,
	STATUS_ASSERTION_FAILED,
	STATUS_OUT_OF_MEMORY,
	STATUS_OUT_OF_RANGE,
	STATUS_INVALID_ARGUMENT,
	STATUS_INVALID_OPERATION,
	STATUS_INVALID_CONFIG,				// the system configuration was invalid
	STATUS_END_OF_STREAM,
	STATUS_DEVICE_NOT_FOUND,
	STATUS_DEVICE_ERROR,
	STATUS_DISK_READ_ERROR,
	STATUS_DISK_WRITE_ERROR,
	STATUS_NOT_IMPLEMENTED,				// the function is not implemented
	STATUS_INCOMPATIBLE,
	STATUS_NOT_SUPPORTED,				// the device is not supported
	STATUS_BUFFER_OVERRUN,				// write to a full buffer
	STATUS_BUFFER_UNDERRUN,				// read from an empty buffer
	STATUS_STACK_OVERFLOW,
	STATUS_DATA_CORRUPT,
	STATUS_FILE_NOT_FOUND,
	STATUS_FILE_READ_ERROR,
	STATUS_FILE_WRITE_ERROR,
	STATUS_SYNC_ERROR,					// synchronization error
	STATUS_BAD_INTERRUPT,				// an interrupt fired for which no handler was installed

	/* NVM errors */
	STATUS_NVM_READ_ERROR = 0x100,
	STATUS_NVM_WRITE_ERROR,

	/* I2C errors */
	STATUS_I2C_GROUP = 0x200,
	STATUS_I2C_BUS_JAMMED,				// the I2C bus is jammed or currently controlled by another master
	STATUS_I2C_DEVICE_NOT_RESPONDING,	// the slave device is not responding or sent a NACK
	STATUS_I2C_LINK_BROKEN,				// the slave device stopped sending ACK
	STATUS_I2C_TIMEOUT,					// the operation timed out
	STATUS_I2C_BUSY,					// a transmission is in progress
	STATUS_I2C_PROTOCOL,				// the I2C protocol was violated

	/* bluetooth errors */
	STATUS_BLE_GROUP = 0x300,
	STATUS_BLE_ADV_SETUP,				// Failure while setting up advertisement data
	STATUS_BLE_DB,						// Failure while registering GATT DB with firmware
	STATUS_BLE_RADIO,					// Failure while reading Tx Power Level
	STATUS_BLE_WHITELIST,				// Failure while editing the whitelist
	STATUS_BLE_CON_PARAM_UPDATE,		// Failure while triggering connection parameter update procedure
	STATUS_BLE_INVALID_CONNECTION,		// Event received for an inexistent connection

	// negative values are reserved for platform specific status codes
} status_t;




// bug_check_ex is provided by the platform abstraction layer or kernel.h and must not return.
void __attribute__((__noreturn__)) bug_check_ex(status_t reason, uintptr_t info, const char *file, int line);

static inline void assert_ex(uintptr_t expression, const char *file, int line) {
	if (!expression)
		bug_check_ex(STATUS_ASSERTION_FAILED, expression, file, line);
}

#define bug_check(reason, info) bug_check_ex((reason), (info), __FILE__, __LINE__)
#define assert(expression) assert_ex((uintptr_t)(expression), __FILE__, __LINE__)




// Returns 1 on a little endian machine and 0 on a big endian machine.
static bool is_little_endian(void) {
	uint16_t i = 1;
	return *(char *)&i;
}



/* swap_bytes_[...] swaps the byte order of the value */

static inline uint16_t swap_bytes_16(uint16_t val) {
	return ((val >> 8) & 0xFF) | ((val << 8) & 0xFF00);
}
static inline uint32_t swap_bytes_32(uint32_t val) {
	return ((val >> 24) & 0xFF) | ((val >> 8) & 0xFF00) | ((val << 8) & 0xFF0000) | ((val << 24) & 0xFF000000);
}

/* big_endian_[...] returns the value interpreted as a big-endian value (i.e. on a little-endian system, the byte order is swapped) */

static inline uint16_t big_endian_16(uint16_t val) {
	return (is_little_endian() ? swap_bytes_16(val) : val);
}
static inline uint32_t big_endian_32(uint32_t val) {
	return (is_little_endian() ? swap_bytes_32(val) : val);
}

/* little_endian_[...] returns the value interpreted as a little-endian value (i.e. on a big-endian system, the byte order is swapped) */

static inline uint16_t little_endian_16(uint16_t val) {
	return (is_little_endian() ? val : swap_bytes_16(val));
}
static inline uint32_t little_endian_32(uint32_t val) {
	return (is_little_endian() ? val : swap_bytes_32(val));
}


/*
* Include application header. 
* This shall contain any application specific definitions that are required
* to configure framework features.
*/
#include "application.h"


/*
* Include headers for the target platform. This includes all definitions
* that describe how the device is wired and all functions that are required to
* interact with the hardware on the device.
* If the target platform is an OS, there is no need to include any hardware headers.
*/
#if defined(__IBM_PC__)
#  include <platform/IBM-PC/device.h>
#elif defined(__S1_QUADROCOPTER__)
#  include <platform/S1/device.h>
#elif defined(WINNT) || defined(WIN32)
#  include <platform/windows/windows.h>
#else
#  error "unknown platform"
#endif


/*
* When compiling third party framwork code, don't include framework headers, as
* this could mess up the include order of the third party code file.
*/
#ifndef __THIRD_PARTY_FRAMEWORK__


#include "arch/io.h"



/*
* If we're running on bare metal, we need to provide our own kernel.
*
* todo: rename kernel files, kernel has mostly been split into features (kernel.c only provides bugcheck functionality)
*/
#include <kernel/kernel.h>



/*
* Include system headers.
* In most applications, only a subset of these features are actually used.
* To control this, customize the application makefile.
*/

#include <system/unicode.h>
#include <system/build.h>
#include <system/filesystem.h>
#include <system/bitmap.h>
#include <system/debug.h>
#include <system/drivers.h>
#include <system/log.h>
#include <system/math.h>
#include <system/time.h>

#include <hardware/i2c.h>
#include <hardware/motor.h>
#ifdef USING_MPU
#  include <hardware/mpu/mpu.h>
#endif
//#ifdef USING_NVM // why disabled?
//#  include <hardware/nvm.h>
//#endif
#ifdef USING_POWER_MGR
#  include <hardware/power.h>
#endif
#ifdef USING_PWM_MOTOR
#  include <hardware/pwmmotor.h>
#endif

#ifdef USING_SIMULATION
#  include <simulation/simulation.h>
#endif

#include <services/services.h>


#endif // __THIRD_PARTY_FRAMEWORK__

#endif // __SYSTEM_H__
