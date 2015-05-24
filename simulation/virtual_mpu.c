/*
*
*
* created: 01.04.15
*
*/

#include <system.h>
#include "virtual_mpu.h"


status_t virtual_mpu_reset(mpu_t *mpu) {
	// nothing to do
	return STATUS_SUCCESS;
}


const char orientField[] = "orient";
const size_t orientFieldLength = sizeof(orientField) - 1;
const char gyroField[] = "gyro";
const size_t gyroFieldLength = sizeof(gyroField) - 1;
const char accelField[] = "accel";
const size_t accelFieldLength = sizeof(accelField) - 1;


DWORD WINAPI WorkerThreadProc(LPVOID lpParam) {
	mpu_t *mpu = (mpu_t *)lpParam;
	virtual_mpu_t *vmpu = (virtual_mpu_t *)mpu->handle;


	// determine the ticks to wait for each period
	__int64 freq;
	QueryPerformanceFrequency((LARGE_INTEGER *)&freq);
	__int64 interval = vmpu->interval * freq / 1000000UL;

	__int64 nextUpdate, ticks;
	QueryPerformanceCounter((LARGE_INTEGER *)&nextUpdate);

	while (vmpu->running) {

		// spin until the next update
		// note: we're most likely not running on a real-time OS, so while other threads are scheduled,
		// we might miss multiple update periods. In this case, all due updates will happen in a burst.
		// This is important as some controllers that rely on this MPU may assume that the update rate is constant.
		do {
			QueryPerformanceCounter((LARGE_INTEGER *)&ticks);
		} while (ticks < nextUpdate);
		nextUpdate += interval;

		double w, x, y, z;

		sim_read_d4(&(vmpu->simObj), orientField, orientFieldLength, &w, &x, &y, &z);

		math_quaternion_t attitude = {
			.w = w * (double)0x4000,
			.x = -x * (double)0x4000,
			.y = y * (double)0x4000,
			.z = -z * (double)0x4000
		};

		sim_read_d3(&(vmpu->simObj), gyroField, gyroFieldLength, &x, &y, &z);

		math_ypr_t angularVelocity = {
			.Yaw = x, // todo: use same scale as MPU6050
			.Pitch = y,
			.Roll = z
		};

		mpu_notify_data(mpu, &attitude, &angularVelocity);
	}

	return 0;
}


// Starts a worker thread that will issue periodic motion state updates
status_t virtual_mpu_start(mpu_t *mpu) {
	virtual_mpu_t *vmpu = (virtual_mpu_t *)mpu->handle;

	vmpu->running = 1;

	vmpu->thread = CreateThread(
		NULL,                   // default security attributes
		0,                      // use default stack size  
		WorkerThreadProc,       // thread function name
		mpu,                    // argument to thread function 
		0,                      // use default creation flags 
		&vmpu->thread_id);      // returns the thread identifier

	return STATUS_SUCCESS;
}

// Stops the worker thread
status_t virtual_mpu_stop(mpu_t *mpu) {
	virtual_mpu_t *vmpu = (virtual_mpu_t *)mpu->handle;

	vmpu->running = 0;
	DWORD status = WaitForSingleObject(vmpu->thread, INFINITE);
	return (status == WAIT_OBJECT_0 ? STATUS_SUCCESS : STATUS_ERROR);
}

