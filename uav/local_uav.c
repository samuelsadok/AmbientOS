/*
*
* Contains routines to control and stabilize a multirotor UAV.
*
* created: 27.02.15
*
*/


#ifdef USING_UAV

#include <system.h>
#include "uav.h"



typedef struct
{
	int8_t x;
	int8_t y;
} coord8_t;


typedef struct
{
	motor_t motor;
	coord8_t position;		// position relative to the center of gravity (the Y-axis points forward, the X-axis points to the right)
	bool clockwise;			// rotation direction
#ifdef USING_MPU
	math_kalman_t filter;
#endif
} rotor_t;


rotor_t rotors[] = UAV_ROTORS;	// defined in device.h
#define UAV_ROTOR_COUNT (sizeof(rotors) / sizeof(rotor_t))


// controllers
math_kalman_t kalmanY;
math_kalman_t kalmanP;
math_kalman_t kalmanR;
math_pid_t pidY;
math_pid_t pidP;
math_pid_t pidR;


// current control state
volatile float controlThrottle = 0;
volatile math_ypr_t controlAttitude = { .Yaw = 0, .Pitch = 0, .Roll = 0 };

// setting inactive to 1 locks the motors down until throttle is removed
volatile uint8_t inactive = 0;







/*
// Adds two signed 16-bit integers. If an overflow occurs, the maximum positive/negative value is returned.
int16_t math_add16_safe(int16_t a, int16_t b) {
	int16_t result = a + b;
	if ((a & 0x8000) && (b & 0x8000) && !(result & 0x8000)) return 0x8000;
	if (!(a & 0x8000) && !(b & 0x8000) && (result & 0x8000)) return 0x7FFF;
	return result;
}
*/


// Calculates the target motor speed required to contribute to the specified motion
static inline float math_calc_rotor(rotor_t *rotor, float throttle, float pitch, float roll, float yaw) {
	pitch *= rotor->position.x;
	roll *= rotor->position.y;
	if (rotor->clockwise) yaw = -yaw;
	return throttle + pitch + roll + yaw;
}

// Regulates each motor to its individual speed to generate the specified motion
void uav_set_rotors(float throttle, float pitch, float roll, float yaw) {
	for (size_t i = 0; i < UAV_ROTOR_COUNT; i++) {
		//int16_t speed = throttle - multirotor->Motors[i].Position.X * pitch - multirotor->Motors[i].Position.Y * roll - multirotor->Motors[i].RotationDirection * yaw;
		float speed = math_calc_rotor(&rotors[i], throttle, pitch, roll, yaw);
		//speed = math_kalman_filter(&(multirotor->Motors[i].filter), speed);
		motor_set_speed(&rotors[i].motor, constrain(speed, 0.0f, 1.0f));
	}
}




#ifdef USING_MPU

// This function must be called at the output rate of the motion processing unit. This rate must be constant.
void uav_control_iteration(mpu_t *mpu, math_quaternion_t *quaternion, math_ypr_t *angularVelocity) {
	wdt_reset();

	// update physical state structure from new MPU data
	math_ypr_t attitude;
	math_quaternion_to_ypr(quaternion->w, quaternion->x, quaternion->y, quaternion->z, &attitude);


	// adapt to our configuration
#ifdef SWAP_PITCH_ROLL
	int16_t temp = attitude.Roll;
	attitude.Roll = attitude.Pitch;
	attitude.Pitch = temp;

	temp = angularVelocity->Roll;
	angularVelocity->Roll = angularVelocity->Pitch;
	angularVelocity->Pitch = temp;
#endif
#ifdef INVERT_PITCH
	attitude.Pitch = -attitude.Pitch;
	angularVelocity->Pitch = -angularVelocity->Pitch;
#endif
#ifdef INVERT_ROLL
	attitude.Roll = -attitude.Roll;
	angularVelocity->Roll = -angularVelocity->Roll;
#endif


	// safety measure for testing
#ifdef SAFETY_LIMIT
	//if (attitude.Flipped) inactive = 1;
	if (attitude.Pitch > SAFETY_LIMIT || attitude.Pitch < -(SAFETY_LIMIT)) inactive = 1;
	if (attitude.Roll > SAFETY_LIMIT || attitude.Roll < -(SAFETY_LIMIT)) inactive = 1;
#endif


	float throttle = 0, yaw = 0, pitch = 0, roll = 0;
	LOGI("pitch = %d, roll = %d", attitude.Pitch, attitude.Roll);


	// apply controllers (must be atomic to prevent concurrent access by uav_config)
	atomic() {

		// apply kalman filters (if enabled)
		if (kalmanY.predictionTime != 0)
			attitude.Yaw = math_kalman_filter(&(kalmanY), attitude.Yaw);
		if (kalmanP.predictionTime != 0)
			attitude.Pitch = math_kalman_filter(&(kalmanP), attitude.Pitch);
		if (kalmanR.predictionTime != 0)
			attitude.Roll = math_kalman_filter(&(kalmanR), attitude.Roll);


		// fetch control inputs (atomic to prevent concurrent access by uav_control)
		atomic() {
			throttle = controlThrottle;
			yaw = controlAttitude.Yaw - attitude.Yaw;
			pitch = controlAttitude.Pitch - attitude.Pitch;
			roll = controlAttitude.Roll - attitude.Roll;
		}

		//// calculate angular rate
		//angularRate.Pitch = math_add16_safe(state.PhysicalState.Attitude.Pitch, -attitude.Pitch);
		//angularRate.Roll = math_add16_safe(state.PhysicalState.Attitude.Roll, -attitude.Roll);


		// apply PID controllers
		yaw = 0; // math_pid_controller(&pidY, yaw, angularVelocity.Yaw);
		pitch = math_pid_controller(&pidP, pitch, angularVelocity->Pitch);
		roll = math_pid_controller(&pidR, roll, angularVelocity->Roll);
	}

	//LOGI("setting throttle to %d%%%%, control is %d%%%%", (int)(throttle * 1000.0f), (int)(controlThrottle * 1000.0f));

	// command calculated motion
	if (throttle && !inactive)
		uav_set_rotors(throttle, pitch, roll, yaw);
	else
		uav_set_rotors(0, 0, 0, 0);

	if (!throttle)
		inactive = 0;

	/*if (throttleCommand) {
		state.DebugLog.PitchSensorLog[state.DebugLog.PitchLogCursor] = state.PhysicalState.Attitude.Pitch;
		state.DebugLog.PitchActionLog[state.DebugLog.PitchLogCursor] = iPitchCommand;
		if (++state.DebugLog.PitchLogCursor >= PITCH_LOG_SIZE) state.DebugLog.PitchLogCursor = 0;
	}*/
}



// Attaches the local UAV controller to an MPU.
// The MPU must be initialized and started separately.
// If this function is not called, the UAV cannot stabilize (e.g. in bootloader mode).
void local_uav_set_mpu(mpu_t *mpu) {
	mpu_subscribe(mpu, uav_control_iteration);
}


// Sets the configuration of the stabilization controllers
status_t local_uav_config(uav_t *uav, uav_config_t *config) {
	atomic() {
		kalmanY.reactiveness = config->reactivenessY; kalmanY.predictionTime = config->predictionTimeY;
		kalmanP.reactiveness = config->reactivenessP; kalmanP.predictionTime = config->predictionTimeP;
		kalmanR.reactiveness = config->reactivenessR; kalmanR.predictionTime = config->predictionTimeR;
		pidY.P = config->PValY; pidY.I = config->IValY; pidY.D = config->DValY;
		pidP.P = config->PValP; pidP.I = config->IValP; pidP.D = config->DValP;
		pidR.P = config->PValR; pidR.I = config->IValR; pidR.D = config->DValR;
	}
	return STATUS_SUCCESS;
}

#endif // USING_MPU


// Starts all motors
status_t local_uav_on(uav_t *uav) {
	for (size_t i = 0; i < UAV_ROTOR_COUNT; i++)
		motor_start(&rotors[i].motor, 0);
	return STATUS_SUCCESS;
}


// Applies a desired control input.
// If this is invoked while the motors are switched off, this doesn't
// have an effect until they are started.
//	throttle: 0: for minimum throttle, 1: full throttle
//	attitude: the desired attitude (can be NULL to only adjust throttle)
status_t local_uav_control(uav_t *uav, float throttle, math_ypr_t *attitude) {
	//LOGI("setting throttle to %d%%%%", (int)(throttle * 1000.0f));
	atomic() {
		controlThrottle = throttle;
		if (attitude)
			controlAttitude = *attitude;
	}
	return STATUS_SUCCESS;
}


// Shuts down all motors
status_t local_uav_off(uav_t *uav) {
	for (size_t i = 0; i < UAV_ROTOR_COUNT; i++)
		motor_stop(&rotors[i].motor, 0);
	return STATUS_SUCCESS;
}



uav_driver_t localUAVDriver = {
	.init = NULL,
#ifdef USING_MPU
	.config = local_uav_config,
#else
	.config = NULL,
#endif
	.on = local_uav_on,
	.control = local_uav_control,
	.off = local_uav_off,
};

uav_t localUAV = {
	.context = NULL,
	.driver = &localUAVDriver
};


#endif // USING_UAV
