
#ifndef __TELETYPE_H__
#define __TELETYPE_H__

#include <stdint.h>


#define TELETYPE_WIDTH	80
#define TELETYPE_HEIGHT	25


#define TELETYPE_TEXT_BRIGHT	0x08

#define TELETYPE_TEXT_BRIGHT	0x08
#define TELETYPE_TEXT_BLINKING	0x80

#define TELETYPE_TEXT_BLACK		0x00
#define TELETYPE_TEXT_BLUE		0x01
#define TELETYPE_TEXT_GREEN		0x02
#define TELETYPE_TEXT_CYAN		0x03
#define TELETYPE_TEXT_RED		0x04
#define TELETYPE_TEXT_MAGENTA	0x05
#define TELETYPE_TEXT_BROWN		0x06
#define TELETYPE_TEXT_GRAY		0x07

#define TELETYPE_BACK_BLACK		0x00
#define TELETYPE_BACK_BLUE		0x10
#define TELETYPE_BACK_GREEN		0x20
#define TELETYPE_BACK_CYAN		0x30
#define TELETYPE_BACK_RED		0x40
#define TELETYPE_BACK_MAGENTA	0x50
#define TELETYPE_BACK_BROWN		0x60
#define TELETYPE_BACK_GRAY		0x70

#define TELETYPE_DEFAULTCOLOR	(TELETYPE_BACK_BLACK + TELETYPE_TEXT_GRAY)




void teletype_clear_screen(void);
void teletype_place_char(char c, int x, int y, char color);
int teletype_read_char(int x, int y);
void teletype_scroll_down(void);
void teletype_print_string(char *str, int len, char color);
void teletype_print_newline(void);
void teletype_print_space(int count);
void teletype_print_hex(void *value, int bytes, char color);
void teletype_print_hex_mem(char *buffer, int count);



static inline void teletype_print_uint8(uint8_t val) {
	teletype_print_hex(&val, 1, 0x07);
}
static inline void teletype_print_uint16(uint16_t val) {
	teletype_print_hex(&val, 2, 0x07);
}
static inline void teletype_print_uint32(uint32_t val) {
	teletype_print_hex(&val, 4, 0x07);
}
static inline void teletype_print_uint64(uint64_t val) {
	teletype_print_hex(&val, 8, 0x07);
}


#endif
