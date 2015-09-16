/*
*
*
* created: 13.03.15
*
*/
#ifndef __CSR_NVM_H__
#define __CSR_NVM_H__


// This function is used to perform things necessary to save power on NVM
// once the read/write operations are done.
static inline void nvm_disable(void) {
	NvmDisable();
	PioSetI2CPullMode(pio_i2c_pull_mode_strong_pull_up);
}

// Reads bytes from the NVM. The NVM is enabled automatically.
//	offset:  The byte offset within the NVM to read from (must be a multiple of the machine word size)
//	buffer:  The buffer to read words into (in packed format)
//	length:  The number of bytes to read (must be a multiple of the machine word size)
static inline status_t nvm_raw_read(size_t offset, char *buffer, size_t length) {
	return status_convert(NvmRead((uint16_t *)buffer, length >> 1, offset >> 1));
}


// Writes bytes from the NVM. The NVM is enabled automatically.
//	offset:  The byte offset within the NVM to write to (must be a multiple of the machine word size)
//	buffer:  The buffer to write (in packed format)
//	length:  The number of bytes to write (must be a multiple of the machine word size)
static inline status_t nvm_raw_write(size_t offset, const char *buffer, size_t length) {
	return status_convert(NvmWrite((const uint16_t *)buffer, length >> 1, offset >> 1));
}


// Initializes the on-board NVM and returns the size of the available NVM store.
static inline status_t nvm_raw_init(size_t *size) {
	sys_status status;
	uint16_t nvmSize;
	status = NvmSize(&nvmSize);
	*size = nvmSize;

#ifdef NVM_TYPE_EEPROM
	if (status == sys_status_success) status = NvmConfigureI2cEeprom();
#elif NVM_TYPE_FLASH
	if (status == sys_status_success) status = NvmConfigureSpiFlash());
#else // this should not happen as these macros are defined in the SDK makefile
#	error "NVM type not specified"
#endif

	return status_convert(status);
}


#endif // __CSR_NVM_H__
