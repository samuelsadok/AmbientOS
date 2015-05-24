/*
* log.c
*
* Created: 24.07.2013 15:54:27
*  Author: cpuheater (innovation-labs@appinstall.ch)
*/

#include <system.h>


#if LOG_VERBOSITY > 0

const char log_str_info[] = ": ";
const char log_str_warning[] = " warning: ";
const char log_str_error[] = " (%d) error: ";



// Prints a single char
static inline void __putc(stream_t *stream, char c) {
	stream->writeByte(stream, c);
}


// Prints a null-terminated string
static inline void __puts(stream_t *stream, const char *s) {
	char c;
	while ((c = *(s++)))
		__putc(stream, c);
}


char digitTable[] = "0123456789ABCDEF";


void __itoa(int i, char *buffer, int base) {
	if (i < 0) {
		*(buffer++) = '-';
		i = -i;
	}

	char *buffer2 = buffer;
	do {
		*(buffer2++) = digitTable[i % base];	
	} while (i /= base);
	*buffer2 = 0;

	// reverse output
	while (buffer < buffer2) {
		char temp = *buffer;
		*(buffer++) = *(--buffer2);
		*buffer2 = temp;
	}
}





// prints a null-terminated formatted string that resides in program memory
//	%d: prints a decimal integer
//	%x8, %x16, %x32, %x64: prints a hexadecimal integer of 8 to 64 bits width
//	%s: prints a null-terminated string
//	%p: prints a null-terminated string that resides in program memory
//	%%: prints the % char
void __fprintf(stream_t *stream, const char *__fmt, ...) {
	if (!stream) return;

	char c;

	int i;
	uint64_t u64 = 0;
	char str[12];
	const char *s;
	char *iPtr = (char *)&i;

	va_list ap;
	va_start(ap, __fmt);

	do {
		c = *(__fmt++);
		
		if (!c) {
			break;
		} else if (c == '%') {
			c = *(__fmt++);

			switch (c) {
				case 0:
					break;
				case '%':
					__putc(stream, '%');
					break;
				case 'd':
					i = va_arg(ap, int);
					__itoa(i, str, 10);
					__puts(stream, str);
					break;
				case 'x':
					i = 0;
					if ((iPtr[0] = c = *(__fmt++))) {
						if ((iPtr[0] != '8') && (iPtr[0] != 'p'))
							iPtr[1] = c = *(__fmt++);
						else
							iPtr[1] = 0;
					}
					assert(is_little_endian());
					switch (i) {
						case 0x0070: // p
							u64 = va_arg(ap, uintptr_t);
							i = sizeof(uintptr_t);
							break;
						case 0x0038: // 8
							u64 = va_arg(ap, unsigned int);
							i = 1;
							break;
						case 0x3631: // 16
							u64 = va_arg(ap, unsigned int);
							i = 2;
							break;
						case 0x3233: // 32
							u64 = va_arg(ap, uint32_t);
							i = 4;
							break;
						case 0x3436: // 64
							u64 = va_arg(ap, uint64_t);
							i = 8;
							break;
						default:
							i = 0;
							__putc(stream, '?');
					}
					while (i--) {
						__putc(stream, digitTable[(u64 >> (8 * i + 4)) & 0xF]);
						__putc(stream, digitTable[(u64 >> (8 * i)) & 0xF]);
					}
					break;
				case 's':
					s = va_arg(ap, char*);
					__puts(stream, s);
					break;
				case 'c':
					str[0] = va_arg(ap, int);
					__putc(stream, str[0]);
					break;
				default:
					__putc(stream, '?');
			}

		} else {
			__putc(stream, c);
		}
	} while (c);

	va_end(ap);
}


// Prints a filename without extension
void print_file_name(stream_t *stream, const char *fileName, int fileNameLength) {
	int start = 0;
	int end = fileNameLength;

	for (int i = fileNameLength - 1; i >= 0; i--) {
		if (fileName[i] == '.') {
			end = i;
		} else if ((fileName[i] == '/') || (fileName[i] == '\\')) {
			start = i + 1;
			break;
		}
	}

	for (int i = start; i < end; i++)
		__putc(stream, fileName[i]);
}


#else /* LOG_VERBOSITY > 0 */

void print_file_name(stream_t *stream, const char *fileName, int fileNameLength) {}
void __fprintf(stream_t *stream, const char *__fmt, ...) {}

#endif /* LOG_VERBOSITY > 0 */
