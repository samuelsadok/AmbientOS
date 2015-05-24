/*
*
*
* created: 14.03.15
*
*/
#ifndef __AVR_NVM_H__
#define __AVR_NVM_H__


// This function has no effects (only provided for compatibility reasons)
static inline void nvm_disable(void) {}

// Reads bytes from the NVM.
//	offset:  The byte offset within the NVM to read from
//	buffer:  The buffer to read into
//	length:  The number of bytes to read
static inline status_t nvm_raw_read(size_t offset, char *buffer, size_t length) {
	atomic() {
		nvm_eeprom_read_buffer(offset, buffer, length);
	}
	return STATUS_SUCCESS;
}


// Writes bytes from the NVM.
//	offset:  The byte offset within the NVM to write to
//	buffer:  The buffer to write
//	length:  The number of bytes to write
static inline status_t nvm_raw_write(size_t offset, const char *buffer, size_t length) {
	atomic() {
#ifdef USING_LARGE_EEPROM_TRANSFERS
		nvm_eeprom_erase_and_write_buffer(offset, buffer, length);
#else
		// this version generates smaller codes but is slower for large transfers on some devices
		while (length--)
			nvm_eeprom_write_byte(offset++, *(buffer++));
#endif
	}
	return STATUS_SUCCESS;
}


// Returns the size of the built-in EEPROM.
static inline status_t nvm_raw_init(size_t *size) {
	*size = EEPROM_SIZE;
	return STATUS_SUCCESS;
}


#endif // __AVR_NVM_H__
