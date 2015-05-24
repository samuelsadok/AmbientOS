/*
*
* Exposes a universal interface used by motion processing units.
*
* created: 28.02.15
*
*/

#ifndef __MPU_H__

struct mpu_t;

typedef void(*mpu_data_callback_t)(struct mpu_t *mpu, math_quaternion_t *attitude, math_ypr_t *angularVelocity);
typedef void(*mpu_error_callback_t)(struct mpu_t *mpu, status_t status);

// Universal interface for a motion processing unit
typedef struct mpu_t
{
	uintptr_t handle;	// driver specific handle to identity this MPU instance

	// Inits the MPU
	status_t(*reset)(struct mpu_t *mpu);

	// Starts sampling of MPU data.
	// This shall not block but rather start periodic calls to mpu_notify.
	status_t(*start)(struct mpu_t *mpu);

	// Stops sampling of MPU data.
	status_t(*stop)(struct mpu_t *mpu);

	// Each of the non-NULL callbacks in this array will be called whenever new data is available from the MPU.
	// This normally occurs at a constant rate.
	mpu_data_callback_t callbacks[MPU_MAX_CALLBACKS];

	// This callback (if non-NULL) will be invoked when an error occurs on the device or in the device driver.
	mpu_error_callback_t errorCallback;
} mpu_t;



// Inits the MPU
static inline status_t mpu_reset(mpu_t *mpu) {
	if (!mpu->reset)
		return STATUS_NOT_IMPLEMENTED;
	return mpu->reset(mpu);
}


// Starts sampling data.
// This returns immediately and afterwards invokes the MPU's callback handlers periodically.
static inline status_t mpu_start(mpu_t *mpu) {
	if (!mpu->start)
		return STATUS_NOT_IMPLEMENTED;
	return mpu->start(mpu);
}


// Starts sampling data.
// This returns immediately and afterwards invokes the MPU's callback handlers periodically.
static inline status_t mpu_stop(mpu_t *mpu) {
	if (!mpu->stop)
		return STATUS_NOT_IMPLEMENTED;
	return mpu->stop(mpu);
}


// Registers a callback to be invoked everytime new data arrives from this MPU.
// Must not be called while the MPU is running (after mpu_start)
static inline status_t mpu_subscribe(mpu_t *mpu, mpu_data_callback_t callback) {
	for (int i = 0; i < MPU_MAX_CALLBACKS; i++)
		if (!mpu->callbacks[i])
			return (mpu->callbacks[i] = callback), STATUS_SUCCESS;
	return STATUS_OUT_OF_MEMORY;
}


// Signals the availability of new data.
static inline void mpu_notify_data(mpu_t *mpu, math_quaternion_t *attitude, math_ypr_t *angularVelocity) {
	for (int i = 0; i < MPU_MAX_CALLBACKS; i++)
		if (mpu->callbacks[i])
			mpu->callbacks[i](mpu, attitude, angularVelocity);
}


// Signals the occurrence of an error.
static inline void mpu_notify_error(mpu_t *mpu, status_t status) {
	if (mpu->errorCallback)
		mpu->errorCallback(mpu, status);
}


#if defined(USING_MPU6050)
#include "invensense_mpu.h"
#endif

#endif // __MPU_H__
