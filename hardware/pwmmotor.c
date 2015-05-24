/*
*
* Provides a motor driver that uses PWM to control the motor.
*
* created: 27.02.15
*
*/

#include <system.h>
#include "motor.h"

#ifdef USING_PWM_MOTOR


// Enables PWM for the specified motor at 0% duty cycle
void pwm_motor_start(uintptr_t motor, bool direction) {
	pwm_set(motor, 0);
	pwm_enable(motor);
}

// Sets PWM to the specified motor speed
void pwm_motor_set_speed(uintptr_t motor, float speed) {
	pwm_set(motor, speed);
}

// Disables PWM for the specified motor and sets the pin to ground
void pwm_motor_stop(uintptr_t motor, bool hardStop) {
	ioport_set_pin_level(motor, 0);
	pwm_disable(motor);
}

// Expose driver functions
motor_driver_t pwmMotorDriver = {
	.start = pwm_motor_start,
	.setSpeed = pwm_motor_set_speed,
	.stop = pwm_motor_stop
};


#endif
