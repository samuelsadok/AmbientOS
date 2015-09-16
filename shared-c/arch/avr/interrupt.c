/*
*
*
* created: 01.03.15
*
*/


#include <system.h>


#ifdef USING_EXT_INT


typedef struct {
	uintptr_t context;
	void(*callback)(uintptr_t context);
} int_callback_t;


static inline void interrupt_handler(PORT_t *port, int_callback_t *callbacks) {
	char flags = port->INTFLAGS;

	for (size_t i = 0; i < 8; i++)
		if ((flags >> i) & 1)
			if (callbacks[i].callback)
				callbacks[i].callback(callbacks[i].context);

	port->INTFLAGS = flags; // clear all flags that were just handled (setting 1 clears them)
}


#ifdef PORTA
int_callback_t callbacksA[8] = { { .callback = NULL } };
ISR(PORTA_INT_vect) {
	interrupt_handler(&PORTA, callbacksA);
}
#else
#define callbacksA NULL
#endif

#ifdef PORTB
int_callback_t callbacksB[8] = { { .callback = NULL } };
ISR(PORTB_INT_vect) {
	interrupt_handler(&PORTB, callbacksB);
}
#else
#define callbacksB NULL
#endif

#ifdef PORTC
int_callback_t callbacksC[8] = { { .callback = NULL } };
ISR(PORTC_INT_vect) {
	interrupt_handler(&PORTC, callbacksC);
}
#else
#define callbacksC NULL
#endif

#ifdef PORTD
int_callback_t callbacksD[8] = { { .callback = NULL } };
ISR(PORTD_INT_vect) {
	interrupt_handler(&PORTD, callbacksD);
}
#else
#define callbacksD NULL
#endif

#ifdef PORTR
int_callback_t callbacksR[8] = { { .callback = NULL } };
ISR(PORTR_INT_vect) {
	interrupt_handler(&PORTR, callbacksR);
}
#else
#define callbacksR NULL
#endif

#ifdef PORTE
#error "not implemented"
#endif


static inline int_callback_t *get_callback_array(char port) {
	switch (port) {
		case 0: return callbacksA;
		case 1: return callbacksB;
		case 2: return callbacksC;
		case 3: return callbacksD;
		case 15: return callbacksR;
	}
	return NULL;
}


// Registers a callback for an interrupt on the specified pin and enables the interrupt on this pin.
// A callback is exclusive on a per-pin basis.
void interrupt_register(ioport_pin_t pin, bool activeHigh, void(*callback)(uintptr_t context), uintptr_t context) {
	
	int_callback_t *c = get_callback_array(pin >> 3) + (pin & 7);
	c->callback = callback;
	c->context = context;

	ioport_set_pin_sense_mode(pin, (activeHigh ? IOPORT_SENSE_RISING : IOPORT_SENSE_FALLING));

	// ports start at address 0x600, every port structure is 0x20 bytes
	PORT_t *port = (PORT_t *)(((((uintptr_t)pin) & 0xF8) << 2) + 0x600);
	port->INTMASK |= (1 << (pin & 7));
	port->INTCTRL = PORT_INTLVL_LO_gc;
}


#endif // USING_EXT_INT
