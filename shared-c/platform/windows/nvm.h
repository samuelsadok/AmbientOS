/*
*
*
* created: 01.04.15
*
*/
#ifndef __WINDOWS_NVM_H__
#define __WINDOWS_NVM_H__

#define NVM_SIZE	(512)

#define nvm_disable()

status_t nvm_raw_read(size_t offset, char *buffer, size_t length);
status_t nvm_raw_write(size_t offset, const char *buffer, size_t length);

// Returns the size of the virtual NVM.
static inline status_t nvm_raw_init(size_t *size) {
	*size = NVM_SIZE;
	return STATUS_SUCCESS;
}

#endif // __WINDOWS_NVM_H__
