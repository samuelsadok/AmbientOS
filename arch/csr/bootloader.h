/*
*
*
* created: 18.02.15
*
*/

#ifndef __CSR_BOOTLOADER_H__
#define __CSR_BOOTLOADER_H__



#define PMEM_PAGE_SIZE_BITS	(10)		// no real page size limitations (write limits are handled by the firmware)
#define PMEM_PAGESIZE		(1 << PMEM_PAGE_SIZE_BITS)

#ifdef USING_BOOTLOADER
#define APP_OFFSET			(0x3D00) // this must be larger than the compiled bootloader image size
#define APP_MAX_SIZE		(PMEM_SIZE - APP_OFFSET - 0x0800) // the NVM region size must match the value in the .keyr file
#endif


void bootloader_init(void);
void __attribute__((__noreturn__)) bootloader_launch_app(uint16_t appOffset);
status_t pmem_read(size_t offset, void *buffer, size_t length);
status_t pmem_write_page(size_t page, void *buffer);
status_t pmem_checksum(size_t offset, size_t length, uint32_t *checksum);

#define PMEM_CHECKSUM_METHOD CHECKSUM_METHOD_SUM32


#endif // __CSR_BOOTLOADER_H__
