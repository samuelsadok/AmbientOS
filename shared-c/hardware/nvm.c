/*
*
* Provides resilent access to the system's non-volatile memory.
* This depends on the platform specific raw NVM driver.
*
* Config options:
*	USING_NVM			enables the NVM driver (the type is defined in the makefile)
*	NVM_SANITY_OFFSET	offset of the magic number
*	NVM_VERSION			NVM data format version (must not exceed 255)
*						this should be incremented if changes to the firmware make it incompatible with the old data format
*
* creating: 13.05.15
*
*/


#include <system.h>

#ifdef USING_NVM

#define NVM_MAGIC_NUMBER	(0xAA00 | NVM_VERSION)

#if NVM_VERSION > 255
#	error "the NVM format version must be 255 or smaller"
#endif

STATIC_ASSERT(NVM_SANITY_LENGTH >= sizeof(uint16_t) * WORDSIZE, "reserved NVM space for magic number too small");

int nvmValid = 0;




// Reads bytes from the NVM. After the read operation, the NVM is powered down again.
//	offset:  The byte offset within the NVM to read from (must be a multiple of the machine word size)
//	buffer:  The buffer to read words into (in packed format)
//	length:  The number of bytes to read (must be a multiple of the machine word size)
void nvm_read(size_t offset, char* buffer, size_t length) {
	status_t status = nvm_raw_read(offset, buffer, length);
	nvm_disable(); // disable NVM after reading

	// if NvmRead fails, report panic
	if (status)
		bug_check(STATUS_NVM_READ_ERROR, status);
}


// Writes bytes to the NVM. After the write operation, the NVM is powered down again.
// This operation is atomic in that a power loss during the operation leaves the old value intact.
//	offset:  The byte offset within the NVM store to write to (must be a multiple of the machine word size)
//	buffer:  The buffer to write (in packed format)
//	length:  The number of bytes to write (must be a multiple of to the machine word size)
void nvm_write(size_t offset, const char* buffer, size_t length) {
	
	// create backup of old value
	uint16_t backup[length >> 1];
	uint16_t header[] = { offset, length };
	status_t status = nvm_raw_read(offset, (char *)backup, length);
	if (!status) status = nvm_raw_write(NVM_FIXUP_OFFSET + sizeof(header) * WORDSIZE, (char *)backup, length);
	if (!status) status = nvm_raw_write(NVM_FIXUP_OFFSET, (char *)header, sizeof(header) * WORDSIZE);

	// write new value
	if (!status) status = nvm_raw_write(offset, buffer, length);

	// invalidate backup
	header[1] = 0;
	if (!status) status = nvm_raw_write(NVM_FIXUP_OFFSET + 2, (char *)&header[1], 2);

	nvm_disable(); // disable NVM after reading/writing

	// Irrecoverable error. Reset the chip.
	if (status)
		bug_check(STATUS_NVM_WRITE_ERROR, status);
}


// Inits the NVM and validates the data (rolling back incomplete writes if neccessary).
// If nvmValid = 0 after calling this, the data in the NVM must be considered
// uninitialized and should be set to default values followed by a call to nvm_data_init.
void nvm_init(void) {
	status_t status;

	size_t nvmSize;

	// init NVM module
	if ((status = nvm_raw_init(&nvmSize)))
		goto exit;

	// verify settings
	if ((status = (NVM_FIXUP_OFFSET >= nvmSize) ? STATUS_INVALID_CONFIG : STATUS_SUCCESS))
		goto exit;

	// validate data
	uint16_t magicNumber;
	if ((status = nvm_raw_read(NVM_SANITY_OFFSET, (char *)&magicNumber, 2)))
		goto exit;

	// roll back incomplete write
	if ((nvmValid = (magicNumber == NVM_MAGIC_NUMBER))) {
		uint16_t fixup[2];
		if ((status = nvm_raw_read(NVM_FIXUP_OFFSET, (char *)fixup, sizeof(fixup) * WORDSIZE)))
			goto exit;
		if (fixup[1]) {
			// restore backup
			char backup[fixup[1] / WORDSIZE];
			if ((status = nvm_raw_read(NVM_FIXUP_OFFSET + sizeof(fixup) * WORDSIZE, backup, fixup[1])))
				goto exit;
			if ((status = nvm_raw_write(fixup[0], backup, fixup[1])))
				goto exit;

			// invalidate backup
			fixup[1] = 0;
			if ((status = nvm_raw_write(NVM_FIXUP_OFFSET + 2, (char *)&fixup[1], 2)))
				goto exit;
		}
	}

exit:

	nvm_disable();

	if (status)
		__reset(status); // we can't bug check here as this would require writing to the NVM (which might destroy fixup data)
}


// If the nvm data was reported uninitialized after nvm_init (nvmValid = 0),
// the application should call this function after setting all fields to default values.
status_t nvm_data_init(void) {
	uint16_t magicNumber = NVM_MAGIC_NUMBER;
	status_t status = nvm_raw_write(NVM_SANITY_OFFSET, (char *)&magicNumber, 2);
	return (nvmValid = (status == STATUS_SUCCESS)), status;
}


#endif
