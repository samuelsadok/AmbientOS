/*
*
*
* created: 15.03.15
*
*/
#ifndef __POWER_H__
#define __POWER_H__


bool pwr_switch_pressed(void);
void pwr_wake(void);
void __attribute__((__noreturn__)) pwr_shutdown(void);
void pwr_init(void);


#endif // __POWER_H__
