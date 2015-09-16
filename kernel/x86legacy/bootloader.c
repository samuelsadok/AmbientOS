

#include <stdint.h>
#include <asciiart.h>
#include <hardware\x86\io.h>
#include <hardware\x86\interrupt.h>
#include <hardware\x86\apic.h>
#include <hardware\teletype.h>
#include <system\threading.h>




#define cpu_halt()			__asm("hlt" : : : "memory")
#define system_int()		__asm("int 0x3" : : : "memory")





uint8_t *vga_buffer;



//void memcopy(void *destination, void *source, int count) {
//	while (count--) *(destination++) = *(source++);
//}






void vga_clear_screen(uint8_t color) {
	uint8_t *buffer = &(vga_buffer[640 * 480]);
	while (--buffer) *buffer = color;
}





extern void fancy_delay(uint32_t count);


char exit_common[] = "\nI'm too old for this shit: ";
char exit_apic[] = "APIC missing";

void exit_main(char *str, int len) {
	interrupts_off();
	teletype_print_string(exit_common, sizeof(exit_common), TELETYPE_TEXT_RED | TELETYPE_TEXT_BRIGHT);
	teletype_print_string(str, len, TELETYPE_TEXT_RED | TELETYPE_TEXT_BRIGHT);
	while (1);
}




char the_text[] = "\n                    WOW. SUCH OPERATING SYSTEM. MUCH AMAZING.\n";
char dog[] = ASCII_ART_DOG;


char thread1_text[] = "system thread still here\n";
char thread2_text[] = "hi, this is thread 2\n";
char thread3_text[] = "and yet another thread\n";

typedef struct {
	int currentX, currentY;
	int dirX, dirY;
	int val;
	int delay;
	int coveredChar;
} ball_context_t;



#define TEMP_CHAR

thread_t thread1;	ball_context_t context1 = { .currentX = 0, .currentY = 7, .dirX = 1, .dirY = 0, .val = 0x096F, .delay = 3, .coveredChar = -1 };
thread_t thread2;	ball_context_t context2 = { .currentX = 79, .currentY = 18, .dirX = -1, .dirY = 0, .val = 0x0A6F, .delay = 4, .coveredChar = -1 };
thread_t thread3;	ball_context_t context3 = { .currentX = 60, .currentY = 0, .dirX = 0, .dirY = 1, .val = 0x0B6F, .delay = 5, .coveredChar = -1 };
thread_t thread4;	ball_context_t context4 = { .currentX = 0, .currentY = 15, .dirX = 1, .dirY = 1, .val = 0x0C6F, .delay = 2, .coveredChar = -1 };
thread_t thread5;	ball_context_t context5 = { .currentX = 79, .currentY = 20, .dirX = -2, .dirY = 1, .val = 0x0D6F, .delay = 3, .coveredChar = -1 };
thread_t thread6;	ball_context_t context6 = { .currentX = 59, .currentY = 12, .dirX = -3, .dirY = 1, .val = 0x096F, .delay = 3, .coveredChar = -1 };
thread_t thread7;	ball_context_t context7 = { .currentX = 22, .currentY = 15, .dirX = -2, .dirY = -3, .val = 0x0A6F, .delay = 4, .coveredChar = -1 };
thread_t thread8;	ball_context_t context8 = { .currentX = 13, .currentY = 7, .dirX = -1, .dirY = 3, .val = 0x0B6F, .delay = 5, .coveredChar = -1 };
thread_t thread9;	ball_context_t context9 = { .currentX = 35, .currentY = 23, .dirX = 0, .dirY = 1, .val = 0x0C6F, .delay = 2, .coveredChar = -1 };
thread_t thread10;	ball_context_t context10 = { .currentX = 70, .currentY = 1, .dirX = 1, .dirY = -3, .val = 0x0D6F, .delay = 3, .coveredChar = -1 };
thread_t thread11;	ball_context_t context11 = { .currentX = 41, .currentY = 19, .dirX = 2, .dirY = 2, .val = 0x096F, .delay = 3, .coveredChar = -1 };
thread_t thread12;	ball_context_t context12 = { .currentX = 32, .currentY = 5, .dirX = 3, .dirY = 2, .val = 0x0A6F, .delay = 4, .coveredChar = -1 };
thread_t thread13;	ball_context_t context13 = { .currentX = 45, .currentY = 13, .dirX = -3, .dirY = 0, .val = 0x0B6F, .delay = 5, .coveredChar = -1 };
thread_t thread14;	ball_context_t context14 = { .currentX = 18, .currentY = 24, .dirX = -2, .dirY = -3, .val = 0x0C6F, .delay = 2, .coveredChar = -1 };
thread_t thread15;	ball_context_t context15 = { .currentX = 64, .currentY = 14, .dirX = -1, .dirY = 1, .val = 0x0D6F, .delay = 3, .coveredChar = -1 };


thread_t kawoom;


// Configures one of the three channels on the programmable interval timer. This operation is globally atomic.
//	channel: 0-2
//	interval: 0-65535 (0 maps to 65536)
void timer_set(int channel, int divisor) {
	out(0x43, 0b00110110 | (channel << 6)); // lo-hi-byte access, mode 3, 16-bit binary
	out(0x40 | channel, divisor & 0xFF); // set low byte
	out(0x40 | channel, (divisor >> 8) & 0xFF); // set high byte
}

// Plays a specified frequency on the built-in speaker
void sound_on(int divisor) {
	// enable timer
	timer_set(2, divisor);

	// connect timer channel 2 to speaker
	int val = in(0x61);
	if (val != (val | 3)) out(0x61, val | 3);
}

// Stops any sound that is playing on the built-in speaker
void sound_off(void) {
	out(0x61, in(0x61) & ~(3));
}


void make_kawoom(void *context) {


	int key;

	while (1) {
		while (in(0x64) & (1 << 0)) {
			key = in(0x60);
			teletype_print_hex(&key, 4, 0x7);

			if (key == 0x01) {
				out(0x64, 0xD1);
				out(0x64, 0xFE);
			}
		}

		sound_on(1000);
		threading_sleep(50);
		sound_off();
		threading_sleep(50);
	}


}


void ball_handler(ball_context_t *context) {
	while (1) {
		interrupts_off();

		if (context->coveredChar > -1)
			teletype_place_char(context->coveredChar & 0xFF, context->currentX, context->currentY, (context->coveredChar >> 8) & 0xFF);

		if (((context->currentX + context->dirX) < 0) || ((context->currentX + context->dirX) >= TELETYPE_WIDTH)) context->dirX = -context->dirX;
		if (((context->currentY + context->dirY) < 0) || ((context->currentY + context->dirY) >= TELETYPE_HEIGHT)) context->dirY = -context->dirY;
		context->currentX += context->dirX;
		context->currentY += context->dirY;

		context->coveredChar = teletype_read_char(context->currentX, context->currentY);
		if (context->coveredChar & 0xFF == 'o')
			context->coveredChar = -1;
		else
			teletype_place_char(context->val & 0xFF, context->currentX, context->currentY, (context->val >> 8) & 0xFF);
		
		interrupts_on();
		threading_sleep(context->delay);
	}
}





int systemTicks;


__attribute__((noreturn))
void main(void) {

	//teletype_print_hex_mem((char *)0xC000, 384);
	//for (int i = 0; i < 255; i++)
		//interrupt_set_isr(i, default_isr, INTERRUPT_TYPE_INT);
	
	
	if (apic_init() != APIC_SUCCESS) exit_main(exit_apic, sizeof(exit_apic));
	interrupt_init();


	teletype_clear_screen();

	teletype_print_string(dog, sizeof(dog), 0x07);
	teletype_print_string(the_text, sizeof(the_text), TELETYPE_TEXT_CYAN | TELETYPE_TEXT_BRIGHT);


	
	threading_init();


	interrupts_on();
	threading_thread_init(&thread1, ball_handler, &context1, 0x10000000);
	threading_thread_init(&thread2, ball_handler, &context2, 0x22000000);
	threading_thread_init(&thread3, ball_handler, &context3, 0x23000000);
	threading_thread_init(&thread4, ball_handler, &context4, 0x24000000);
	threading_thread_init(&thread5, ball_handler, &context5, 0x25000000);
	threading_thread_init(&thread6, ball_handler, &context6, 0x26000000);
	threading_thread_init(&thread7, ball_handler, &context7, 0x27000000);
	threading_thread_init(&thread8, ball_handler, &context8, 0x28000000);
	threading_thread_init(&thread9, ball_handler, &context9, 0x29000000);
	threading_thread_init(&thread10, ball_handler, &context10, 0x30000000);
	threading_thread_init(&thread11, ball_handler, &context11, 0x31000000);
	threading_thread_init(&thread12, ball_handler, &context12, 0x32000000);
	threading_thread_init(&thread13, ball_handler, &context13, 0x33000000);
	threading_thread_init(&thread14, ball_handler, &context14, 0x34000000);
	threading_thread_init(&thread15, ball_handler, &context15, 0x35000000);
	threading_thread_init(&kawoom, make_kawoom, 0, 0x60000000);
	interrupts_off();

	threading_thread_resume(&thread1);
	threading_thread_resume(&thread2);
	threading_thread_resume(&thread3);
	threading_thread_resume(&thread4);
	threading_thread_resume(&thread5);
	threading_thread_resume(&thread6);
	threading_thread_resume(&thread7);
	threading_thread_resume(&thread8);
	threading_thread_resume(&thread9);
	threading_thread_resume(&thread10);
	threading_thread_resume(&thread11);
	threading_thread_resume(&thread12);
	threading_thread_resume(&thread13);
	threading_thread_resume(&thread14);
	threading_thread_resume(&thread15);
	threading_thread_resume(&kawoom);



	apic_timer_start(20000, 0b110);
	interrupts_on();
	while (1) {
		systemTicks++;
		cpu_halt();
	}
}

