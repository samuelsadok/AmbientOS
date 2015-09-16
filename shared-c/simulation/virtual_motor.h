/*
*
*
* created: 01.04.15
*
*/
#ifndef __VIRTUAL_MOTOR_H__
#define __VIRTUAL_MOTOR_H__


extern motor_driver_t virtualMotorDriver; // defined in virtual_motor.c

#define CREATE_VIRTUAL_MOTOR(_name)		{ .handle = (uintptr_t)(_name), .driver = &virtualMotorDriver }


#endif // __VIRTUAL_MOTOR_H__