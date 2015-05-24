/*
 * math.h
 *
 * Provides some arithmetic operations used for control tasks.
 *
 * The actual functions are mostly implemented in assembler in
 * arch/[...]/math.S to allow for most efficient calculation.
 *
 * Config options:
 *	USING_FAST_PID	enables a faster, less accurate PID controller implemented in assembler
 *
 * Created: 25.07.2013 21:18:55
 *  Author: cpuheater
 */ 


#ifndef __MATH_H__
#define __MATH_H__


// represents an orientation expressed in yaw-pitch-roll angles
typedef struct {
	int16_t Yaw;
	int16_t Pitch;
	int16_t Roll;
	int8_t Flipped;
} math_ypr_t;

typedef struct
{
	float w, x, y, z;
} math_quaternion_t;


// this function converts a quaternion into yaw, pitch and roll angles (in this order)
// the quaternion components are expected as 16-Bit signed values, 1 being represented by 0x4000
// the results are 16-Bit signed values, with 0x1000 representing a 1.
// EDIT: yaw is not calculated.
void math_quaternion_to_ypr(int16_t qW, int16_t qX, int16_t qY, int16_t qZ, math_ypr_t *output);

// this function is the same as the one above but takes the MPU FIFO packet directly as an argument. This is more efficient than a direct C call.
// In addition to the above function, the Z gyro output is copied to MPU_yaw.
//void math_quaternion_data_to_ypr(uint8_t *data);


// this structure represents a Kalman Filter context.
typedef struct {
	uint8_t reactiveness; // this parameter defines the behavior of the filter. 0xFF: immediate reaction, no noise rejection, 0x00: very slow reaction, maximum noise rejection
	uint8_t predictionTime; // number of time steps that the filter will predict into the future. Example: Kalman Filter executed every 1ms, predictionTime = 100 -> filter output is the prediction for 100ms into the future
	int16_t lastPrediction;
	uint8_t lastSlopeB0, lastSlopeB1, lastSlopeB2;
} math_kalman_t;

#define CREATE_KALMAN(_reactiveness, _predictionTime) { .reactiveness = (_reactiveness), .predictionTime = (_predictionTime), .lastPrediction = 0, .lastSlopeB0 = 0, .lastSlopeB1 = 0, .lastSlopeB2 = 0 } // inits a new Kalman Filter context
#define CREATE_KALMAN_INLINE(_reactiveness, _predictionTime) ((math_kalman_t) CREATE_KALMAN((_reactiveness), (_predicionTime)))

// Executes a single kalman calculation step. This function must be called at regular intervals.
// Defined in the architecture specific assembler file.
int16_t math_kalman_filter(math_kalman_t *context, int16_t currentValue);

#ifdef USING_FAST_PID

	// this structure represents a PID controller context. Set the P, I and D parameters and the I limit before using it.
	typedef struct {
		uint8_t P;	// proportional coefficient
		uint8_t I; // integral coefficient
		uint8_t D; // derivative coefficient
		uint8_t ILimit;
		int32_t ErrSum; // used by the I-controller
		int16_t ErrPrev; // used by the D-controller
	} math_pid_t;
	
	// This is a 16-bit PID controller with 8-bit parameters.
	// If you call this function at a regular interval, it will attempt to adjust the error to zero, using the P, I and D parameters specified in the context struct.
	int16_t math_pid_controller(math_pid_t *context, int16_t error);

#else

	// this structure represents a PID controller context. Set the P, I and D parameters and the I limit before using it.
	typedef struct {
		float P;		// proportional coefficient
		float I;		// integral coefficient
		float D;		// derivative coefficient
		float maxI;		// limit for the integrated error (can grow quickly)
		float sum;		// used by the I-controller
	} math_pid_t;
	
	// This is a 32-bit floating point PID controller.
	// If you call this function at a regular interval, it will attempt to control the value to zero, using the P, I and D parameters specified in the PID struct.
	float math_pid_controller(math_pid_t *pid, float value, float differential);
#endif


#define CREATE_PID(_p, _i, _d, _maxI) { .P = (_p), .I = (_i), .D = (_d), .maxI = (_maxI), .sum = 0 } // inits a new PID controller context
#define CREATE_PID_INLINE(_p, _i, _d, _maxI) ((math_pid_t) CREATE_PID((_p), (_i), (_d), (_maxI)))


static inline float constrain(float val, float minimum, float maximum) {
	if (val > maximum) return maximum;
	if (val < minimum) return minimum;
	return val;
}

static inline int32_t constrain32(int32_t val, int32_t minimum, int32_t maximum) {
	if (val > maximum) return maximum;
	if (val < minimum) return minimum;
	return val;
}

#endif /* __MATH_H__ */
