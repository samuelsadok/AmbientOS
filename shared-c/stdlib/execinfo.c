/*
*
*
* created: 10.02.15
*
*/


#include <system.h>

// todo
size_t backtrace(void **buffer, size_t size) {
	//unsigned int i = 0;
	//do {
	//	buffer[i] = __builtin_extract_return_addr(__builtin_return_address(i));
	//	i++;
	//} while (__builtin_frame_address(i) && i < size);
	//return i;

	int i = 0;
	//buffer[i++] = __builtin_extract_return_addr(__builtin_return_address(1));
	//buffer[i++] = __builtin_extract_return_addr(__builtin_return_address(2));
	return i;
}


