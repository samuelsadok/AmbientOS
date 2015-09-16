

#include <system.h>
#include "teletype.h"


uint16_t *teletype_buffer = (uint16_t *)0xFFFFFFFF800B8000;
int teletype_cursor = 0;


// clears the screen
void teletype_clear_screen(void) {
	for (int i = 0; i < (TELETYPE_WIDTH * TELETYPE_HEIGHT); i++)
		teletype_buffer[i] = 0x0720;
	teletype_cursor = 0;
}

// puts the specified char at the specified position
void teletype_place_char(char c, int x, int y, char color) {
	teletype_buffer[x + TELETYPE_WIDTH * y] = ((color << 8) + c);
}

// Returns the specified char and its color
int teletype_read_char(int x, int y) {
	return teletype_buffer[x + TELETYPE_WIDTH * y];
}

// moves the entire screen content one line up
void teletype_scroll_down(void) {
	int counter = TELETYPE_WIDTH * (TELETYPE_HEIGHT - 1);
	uint16_t *destination = teletype_buffer;
	uint16_t *source = &(teletype_buffer[TELETYPE_WIDTH]);

	while (counter--) *(destination++) = *(source++); // copy each char

	counter = TELETYPE_WIDTH;
	destination = &(teletype_buffer[TELETYPE_WIDTH * (TELETYPE_HEIGHT - 1)]);
	while (counter--) *(destination++) = 0x0720; // space

	teletype_cursor -= TELETYPE_WIDTH;
}

// Outputs a string
void teletype_print_string(char *str, int len, char color) {
	while (len--) {
		char c = *(str++);

		if (c == '\x09') c = ' '; // tab

		if (c == '\n') {
			int new_cursor = 0;
			while (new_cursor <= teletype_cursor) new_cursor += TELETYPE_WIDTH;
			teletype_cursor = new_cursor;
		} else if (c) {
			teletype_buffer[teletype_cursor++] = ((color << 8) + c);
		}

		if (teletype_cursor >= (TELETYPE_WIDTH * TELETYPE_HEIGHT))
			teletype_scroll_down();
	}
}


// Advances the cursor to the next line
void teletype_print_newline(void) {
	teletype_print_string("\n", 1, 0);
}


// Advances the cursor to the next line
void teletype_print_space(int count) {
	for (int i = 0; i < count; i++)
		teletype_print_string(" ", 1, 0);
}


char hexDigits[] = "0123456789ABCDEF";
// Outputs a hex representation of an integer
void teletype_print_hex(void *value, int bytes, char color) {
	char *current = (char *)value + bytes;
	for (int i = 0; i < bytes; i++) {
		current--;
		teletype_print_string(&hexDigits[((*current) >> 4) & 0xF], 1, color);
		teletype_print_string(&hexDigits[((*current) >> 0) & 0xF], 1, color);
	}
}

//extern void fancy_delay(uint32_t count);


// Ouputs a block of memory in hexadecimal representation
void teletype_print_hex_mem(char *buffer, int count) {
	for (int i = 0; i < count; i++) {
		if (!(i & 0xF)) {
			//fancy_delay(0x2FFFFFF);
			teletype_print_newline();
			teletype_print_space(2);
			uintptr_t addr = (uintptr_t)buffer + i;
			teletype_print_hex(&addr, sizeof(addr), TELETYPE_TEXT_CYAN);
			teletype_print_space(4);
		}

		teletype_print_hex(&buffer[i], 1, 0x07);
		teletype_print_space(1);
	}
	teletype_print_newline();
}



status_t teletype_putc(stream_t *stream, char c) {
	teletype_print_string(&c, 1, 2);
	return STATUS_SUCCESS;
}

status_t teletype_putc_err(stream_t *stream, char c) {
	teletype_print_string(&c, 1, 4);
	return STATUS_SUCCESS;
}


REGISTER_OUTPUT(teletype_putc, teletype_putc_err);
REGISTER_INIT2(teletype_clear_screen);
