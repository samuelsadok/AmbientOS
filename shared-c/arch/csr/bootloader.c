/*
*
* Provides a bootloader application with access to the program memory and
* with the ability to load another application from program memory.
* Usually, this is done using the DFU service (dfu.c).
* Application loading works by placing a small loader code in a reserved bootloader section.
* This is the same technique used by the original OTA bootloader by CSR.
* The bootloader section must not be used in a normal application because that would
* lead to the bootloader overwriting its own bootloader section.
*
* To compile this, the bootloader section needs to be incorporated into the build process.
*
* Config options:
*	USING_BOOTLOADER				enable bootloader features
*	I2C_EEPROM_WRITE_CYCLE_TIME		write cycle of the EEPROM (in microseconds)
*	I2C_EEPROM_WRITE_PAGE_SIZE		write page size of the EEPROM (in bytes)
*
* created: 18.02.15
*
*/

#include <system.h>
#include "bootloader.h"


#ifdef USING_BOOTLOADER

#ifndef NVM_TYPE_EEPROM
#	error "bootloading from SPI flash is currently not supported"
#endif

#ifndef USING_BUILTIN_I2C_MASTER
#	error "the bootloader requires the builtin I2C master to be initialized"
#endif


typedef struct __attribute__((__packed__)) {
	uint16_t eepromAddr;
	uintptr_t destAddr;
	uint16_t length;
	uint16_t checksum;
} img_block_header_t;


#define appBlockCount		((uint16_t *)0x8F52)
#define appBlockHeaders		((img_block_header_t *)0x8F53)

#define EEPROM_ADDR	0x50


// Loads a single memory block from an application image in the EEPROM.
__attribute__((__section__(".bootloader")))
void load_img_block(uint16_t appOffset, uint16_t blockNum) {
	img_block_header_t *block = &(appBlockHeaders[blockNum]);

	uint16_t eepromAddr = appOffset += block->eepromAddr;
	uint16_t bytesToRead = block->length;
	uintptr_t destAddr = block->destAddr;

	// read in chunks of max 1kB
	while (bytesToRead) {
		size_t readLength = min(0x400, bytesToRead);
		if (I2cEepromRead(EEPROM_ADDR, eepromAddr, 1, readLength, (uint16_t *)destAddr) != sys_status_success)
			WarmReset(); // we can no longer bug check at this stage
		eepromAddr += readLength;
		destAddr += (readLength & 1) + (readLength >> 1);
		bytesToRead -= readLength;
	}
}


// Loads and launches the specified application from EEPROM.
// The block headers must already have been loaded.
__attribute__((__section__(".bootloader")))
void __attribute__((__noreturn__)) load_app(uint16_t appOffset) {
	for (uint16_t i = 0; i < *appBlockCount; i++)
		load_img_block(appOffset, i);

	// jump to application (how does this work?)
	__asm volatile("bra 0, x" : : "rx"(5) : "memory");
	for (;;); // convince the compiler that we really don't return
}


// Loads and launches the specified application from EEPROM.
// The block headers must already have been loaded.
//	appOffset: the byte offset in the EEPROM where the application image starts
void __attribute__((__noreturn__)) bootloader_launch_app(uint16_t appOffset) {
	sys_status status;
	if ((status = I2cEepromRead(EEPROM_ADDR, appOffset + 3, 1, 1, appBlockCount)) != sys_status_success)
		bug_check(STATUS_DISK_READ_ERROR, status);
	*appBlockCount &= 0xFF;
	if ((status = I2cEepromRead(EEPROM_ADDR, appOffset + 6, 1, *appBlockCount << 3, (uint16_t *)appBlockHeaders)) != sys_status_success)
		bug_check(STATUS_DISK_READ_ERROR, status);
	load_app(appOffset);
}


// Reads (packed) data from program memory
//	offset: byte-offset in the program memory
//	buffer: a buffer of sufficient size
//	length: the desired read length in bytes (set to the number of bytes actually read)
status_t pmem_read(size_t offset, void *buffer, size_t length) {
	return status_convert(I2cEepromRead(EEPROM_ADDR, offset, 1, length, (uint16_t *)buffer));
}

// Writes (packed) data to the program memory
//	offset: byte-offset in the program memory
//	buffer: data to be written
//	length: number of bytes to write (must be a multiple of 2)
status_t pmem_write_page(size_t page, void *buffer) {
	// The framework takes care of actual page boundaries and write cycle times.
	// For compatibility reasons, we still act as if there were pages.
	return status_convert(I2cEepromWrite(EEPROM_ADDR, page * PMEM_PAGESIZE, 1, PMEM_PAGESIZE, (uint16_t *)buffer));
}


// Returns the checksum of some portion of the program memory.
// The checksum is calculated by calculating the 32-bit sum over the selected range.
//	offset: start address of the range (in bytes)
//	length: length of the range (in bytes)
//	checksum: set to the calculated checksum
status_t pmem_checksum(size_t offset, size_t length, uint32_t *checksum) {
	status_t status;
	*checksum = 0;
	char buffer[PMEM_PAGESIZE / WORDSIZE];

	while (length) {
		size_t readBytes = min(PMEM_PAGESIZE, length);
		if ((status = pmem_read(offset, buffer, readBytes)))
			return status;
		for (size_t i = 0; i < (readBytes >> 2); i++)
			*checksum += ((uint32_t *)buffer)[i];
		offset += readBytes;
		length -= readBytes;
	}

	return STATUS_SUCCESS;
}


// Prepares reading from and writing to program memory
void bootloader_init(void) {
	I2cEepromSetWriteCycleTime(I2C_EEPROM_WRITE_CYCLE_TIME);
	I2cEepromSetWritePageSize(I2C_EEPROM_WRITE_PAGE_SIZE);
}


#endif
