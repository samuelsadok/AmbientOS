/*
*
* Uses a file in the same directory as the executable to imitate NVM functionality.
*
* created: 01.04.15
*
*/

#include <system.h>
#include "nvm.h"


// Opens the file that represents the NVM.
// If the file doesn't exist, it is created and filled with 0xFF.
HANDLE nvm_open() {
	HANDLE hFile = CreateFile("./nvm.bin", GENERIC_READ | GENERIC_WRITE, FILE_SHARE_READ, NULL, OPEN_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
	DWORD status = GetLastError();
	if (hFile == INVALID_HANDLE_VALUE)
		bug_check(STATUS_FILE_READ_ERROR, status);

	if (!status) {

		char b[256];
		memset(b, 0xFF, sizeof(b));
		char *buffer = b;
		size_t length = NVM_SIZE;
		DWORD writtenBytes;

		while (length) {
			if (!WriteFile(hFile, buffer, max(length, sizeof(b)), &writtenBytes, NULL))
				bug_check(STATUS_FILE_WRITE_ERROR, 0);
			length -= writtenBytes;
		}

	} else if (status != ERROR_ALREADY_EXISTS) {
		bug_check(STATUS_FILE_READ_ERROR, status);
	}

	return hFile;
}




status_t nvm_raw_read(size_t offset, char *buffer, size_t length) {
	status_t status = STATUS_SUCCESS;

	atomic() {
		HANDLE hFile = nvm_open();

		if (SetFilePointer(hFile, offset, NULL, FILE_BEGIN) == INVALID_SET_FILE_POINTER) {
			length = 0;
			status = STATUS_FILE_READ_ERROR;
		}

		while (length) {
			DWORD readBytes;
			if (!ReadFile(hFile, buffer, length, &readBytes, NULL)) {
				status = STATUS_FILE_READ_ERROR;
				break;
			}
			buffer += readBytes;
			length -= readBytes;
		}

		CloseHandle(hFile);
	}

	return status;
}


status_t nvm_raw_write(size_t offset, const char *buffer, size_t length) {
	status_t status = STATUS_SUCCESS;

	atomic() {
		HANDLE hFile = nvm_open();

		if (SetFilePointer(hFile, offset, NULL, FILE_BEGIN) == INVALID_SET_FILE_POINTER) {
			length = 0;
			status = STATUS_FILE_READ_ERROR;
		}

		while (length) {
			DWORD writtenBytes;
			if (!WriteFile(hFile, buffer, length, &writtenBytes, NULL)) {
				status = STATUS_FILE_WRITE_ERROR;
				break;
			}
			buffer += writtenBytes;
			length -= writtenBytes;
		}

		CloseHandle(hFile);
	}

	return status;
}

