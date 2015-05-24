/*
 * csr1010.h
 *
 * Created: 24.07.2013 18:16:57
 *  Author: samuel
 */

#ifndef __CSR1010_H__
#define __CSR1010_H__

#define MATLAB // this is defined to trick some CSR header files to omit some definitions

// include CSR framework
#include <reset.h>
#include <gatt.h>
#include <ls_app_if.h>
#include <debug.h>
#include <nvm.h>
#include <i2c.h>
#include <security.h>
#include <mem.h>
#include <macros.h>
#include <gatt_prim.h>
#include <panic.h>
#include <status.h>
#include <buf_utils.h>
#include <config_store.h>
#include <ble_hci_test.h>





// todo: see how atomic operations should be implemented
//typedef int cpu_int_state_t;
//static inline cpu_int_state_t cpu_irq_save(void) { return 0; }
//static inline void cpu_irq_restore(cpu_int_state_t state) { }
//#define __atomic_enter()		cpu_irq_save()
//#define __atomic_exit(state)	cpu_irq_restore(state)

// not implemented (would need to delay API events)
#define atomic()	for (int i = 0; i < 1; i++)

// interrupts cannot be disabled
#define interrupts_on()
#define interrupts_off()



#if defined(CSR101x)
#	if defined(CSR101x_A05)
		// device specific implementations
#	else
#		error "unsupported CSR chip version"
#	endif
#else
#	error "unsupported CSR chip family"
#endif


// Copies from one packed buffer into another one.
// If the length is odd, the MSB of the destination buffer is undefined.
//	length: number of bytes to copy
static inline void *memcpy(void *dest, const void *src, size_t length) {
	return MemCopy(dest, src, (length >> 1) + (length & 1));
}

// Compares two packed buffers and returns 0 if they are equal.
//	length: length in bytes
static inline int memcmp(const void *buf1, const void *buf2, size_t length) {
	int result = MemCmp(buf1, buf2, length >> 1);
	if (result || !(length & 1))
		return result;
	return (int)(((const uint16_t *)buf1)[length >> 1] & 0xFF) - (int)(((const uint16_t *)buf2)[length >> 1] & 0xFF);
}



// Resets the local processor. Peripherals are not reset.
static inline __attribute__((__noreturn__)) void __reset(void) {
	//if (code)
	//	Panic(code);
	WarmReset(); // does not return
	for (;;);
}


// Converts a platform specific status code to a system status code
static inline status_t status_convert(sys_status status) {
	return ((status == sys_status_success) ? STATUS_SUCCESS : (INT_MIN | status));
}


void csr1010_init(void);


// no extended text section on this device
#define EXTENDED_TEXT


#include "ble.h"
#include "gatt.h"
#ifdef USING_BUILTIN_I2C_MASTER
#  include "i2c.h"
#endif
#ifdef USING_NVM
#  include "nvm_raw.h"
#endif
#ifdef USING_BUILTIN_TEMPERATURE
#  include "temperature.h"
#endif
#ifdef USING_BUILTIN_VOLTAGE
#  include "voltage.h"
#endif
#ifdef USING_BOOTLOADER
#  include "bootloader.h"
#endif
#include "timer.h"
#include <app_gatt_db.h>	// this file is compiler generated



#endif // __CSR1010_H__
