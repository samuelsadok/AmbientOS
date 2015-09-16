/*
*
*
* created: 01.03.15
*
*/

#ifndef __AVR_INTERRUPT_H__
#define __AVR_INTERRUPT_H__

void interrupt_register(ioport_pin_t pin, bool activeHigh, void(*callback)(uintptr_t context), uintptr_t context);

#endif // __AVR_INTERRUPT_H__
