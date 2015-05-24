/*
*
*
* created: 01.04.15
*
*/

#include <system.h>


#ifdef USING_VIRTUAL_MOTOR


const char throttleField[] = "throttle";
const size_t throttleFieldLength = sizeof(throttleField) - 1;


// Sets the virtual motor to the specified motor speed.
void virtual_motor_set_speed(uintptr_t motor, float speed) {
	sim_object_t obj = {
		.name = (const char *)motor,
		.nameLength = strlen((const char *)motor)
	};

	sim_write_d1(&obj, throttleField, throttleFieldLength, speed);
}

// Enables the virtual motor at minimal speed.
void virtual_motor_start(uintptr_t motor, bool direction) {
	virtual_motor_set_speed(motor, 0);
}

// Disables the virtual motor.
void virtual_motor_stop(uintptr_t motor, bool hardStop) {
	virtual_motor_set_speed(motor, 0); // todo: simulate
}

// Expose driver functions.
motor_driver_t virtualMotorDriver = {
	.start = virtual_motor_start,
	.setSpeed = virtual_motor_set_speed,
	.stop = virtual_motor_stop
};


#endif
