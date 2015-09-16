/*
*
* Provides a universal interface for motor control.
*
* created: 27.02.15
*
*/

#ifndef __MOTOR_H__
#define __MOTOR_H__


struct motor_driver_t;

typedef struct
{
	uintptr_t handle;				// a driver specific value to identify this motor instance
	struct motor_driver_t *driver;	// a set of function pointers that can be used to control the motor
} motor_t;

typedef struct motor_driver_t {

	// Spins the motor up to its minimum speed.
	//	reverse: spin into the other direction (not available for all drivers)
	void(*start)(uintptr_t motor, bool reverse);

	// Sets the target speed of the motor.
	// For some drivers this may control the actual motor speed while for other drivers it might just set the PWM duty cycle.
	//	speed: target speed (0: minimum, 1: maximum, any value out of range shall be clipped to 0 or 1)
	void(*setSpeed)(uintptr_t motor, float speed);

	// Shuts the motor down.
	//	hardStop: stop immediately by means of shot circuit (not available for all drivers)
	void(*stop)(uintptr_t motor, bool hardStop);

} motor_driver_t;


static inline void motor_start(motor_t *motor, bool direction) {
	motor->driver->start(motor->handle, direction);
}

static inline void motor_set_speed(motor_t *motor, float speed) {
	motor->driver->setSpeed(motor->handle, speed);
}

static inline void motor_stop(motor_t *motor, bool hardStop) {
	motor->driver->stop(motor->handle, hardStop);
}


#endif // __MOTOR_H__
