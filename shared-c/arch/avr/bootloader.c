/*
*
*
* created: 05.03.15
*
*/

#include <system.h>

#ifdef USING_BOOTLOADER

void __attribute__((__noreturn__)) EXTENDED_TEXT bootloader_launch_app(uint16_t appOffset) {
	assert(!appOffset);
	((void(*)(void))(0))();
	for (;;); // convince the compiler that we won't return
}


status_t EXTENDED_TEXT pmem_read(size_t offset, void *buffer, size_t length) {
	memcpy_PF(buffer, offset, length);
	return STATUS_SUCCESS;
}


#ifdef __AVR_XMEGA__

status_t EXTENDED_TEXT pmem_write_page(size_t page, void *_buffer) {
	uint16_t *buffer = _buffer;

	atomic() {
		for (size_t i = 0; i < PMEM_PAGESIZE; i += 2)
			nvm_flash_load_word_to_buffer(i, *buffer++);
		nvm_flash_atomic_write_app_page(page * PMEM_PAGESIZE);
	}

	return STATUS_SUCCESS;
}

#else

status_t EXTENDED_TEXT pmem_write_page(size_t page, void *_buffer) {
	uint16_t *buffer = _buffer;

	atomic() {
		boot_page_erase_safe(page * PMEM_PAGESIZE);
		boot_spm_busy_wait(); // Wait until the memory is erased.

		for (size_t i = 0; i < PMEM_PAGESIZE; i += 2)
			boot_page_fill(page * PMEM_PAGESIZE + i, *buffer++); // offset correct?
		boot_page_write(page * PMEM_PAGESIZE); // Store buffer in flash page.
		boot_spm_busy_wait(); // Wait until the memory is written.

		boot_rww_enable(); // reenable RWW section
	}

	return STATUS_SUCCESS;
}

#endif


// Calculates a 32-bit CRC checksum of a portion of the program memory.
status_t EXTENDED_TEXT pmem_checksum(size_t offset, size_t length, uint32_t *checksum) {
	*checksum = crc_flash_checksum(CRC_FLASH_RANGE, offset, length);
	return STATUS_SUCCESS;
}


#endif
