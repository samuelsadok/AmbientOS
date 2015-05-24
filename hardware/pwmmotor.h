/*
*
*
* created: 27.02.15
*
*/
#ifndef __PWM_MOTOR_H__
#define __PWM_MOTOR_H__


extern motor_driver_t pwmMotorDriver; // defined in pwmmotor.c

#define CREATE_PWM_MOTOR(_gpio)		{ .handle = (_gpio), .driver = &pwmMotorDriver }


#endif // __PWM_MOTOR_H__
