/*
*
*
* created: 05.03.15
*
*/

#ifndef __AVR_BOOTLOADER_H__
#define __AVR_BOOTLOADER_H__

#define PMEM_PAGESIZE		(SPM_PAGESIZE) // defined in avr/pgmspace.h
//#	define PMEM_PAGESIZE		(FLASH_PAGE_SIZE) // defined in avr/pgmspace.h

#ifdef USING_BOOTLOADER
#define APP_OFFSET			(0)
#define APP_MAX_SIZE		(APP_SECTION_SIZE - EXTENDED_BOOTSECTION_SIZE) // defined in MCU header
#define EXTENDED_TEXT		__attribute__((__section__(".extended_text"))) // offload some code from bootloader section to application section
#endif


static inline void bootloader_init(void) {} // provided for compatibility
void __attribute__((__noreturn__)) bootloader_launch_app(uint16_t appOffset);
status_t pmem_read(size_t offset, void *buffer, size_t length);
status_t pmem_write_page(size_t page, void *buffer);
status_t pmem_checksum(size_t offset, size_t length, uint32_t *checksum);

#define PMEM_CHECKSUM_METHOD CHECKSUM_METHOD_CRC32

#endif // __AVR_BOOTLOADER_H__

