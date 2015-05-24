/*
*
* Uses the stdio.h functions to provide logging output functionality.
* todo: move out of windows folder
*
* created: 02.04.15
*
*/

#include <system.h>
#include <stdio.h>


status_t console_putc(stream_t *stream, char c) {
	fprintf(stdout, "%c", c);
	return STATUS_SUCCESS;
}

status_t console_putc_err(stream_t *stream, char c) {
	fprintf(stderr, "%c", c);
	return STATUS_SUCCESS;
}


REGISTER_OUTPUT(console_putc, console_putc_err);
