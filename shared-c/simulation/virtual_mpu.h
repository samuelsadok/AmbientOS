/*
*
* Enables access to virtual MPUs.
* A virtual MPU is an MPU that resides in a simulation and is attached to some simulated physical body.
* The application can connect to a virtual MPU through the pipe that the simulated device exposes.
*
* created: 01.04.15
*
*/
#ifndef __VIRTUAL_MPU__
#define __VIRTUAL_MPU__


typedef struct
{
	sim_object_t simObj;	// the simulation object associated with this MPU
	long interval;			// update interval (in µs!)

	volatile bool running;
	HANDLE thread;
	DWORD thread_id;
} virtual_mpu_t;


#define CREATE_VIRTUAL_MPU(_simObj, _interval) {	\
	.simObj = _simObj,								\
	.interval = (_interval),						\
	.running = 0									\
}


status_t virtual_mpu_reset(mpu_t *mpu);
status_t virtual_mpu_start(mpu_t *mpu);
status_t virtual_mpu_stop(mpu_t *mpu);


// Applies a virtual MPU context to a universal MPU structure.
//	virtual_mpu: A pointer to a structure of the type virtual_mpu_t
#define APPLY_VIRTUAL_MPU(virtual_mpu) {	\
	.handle = (uintptr_t)(virtual_mpu),		\
	.reset = virtual_mpu_reset,				\
	.start = virtual_mpu_start,				\
	.stop = virtual_mpu_stop				\
}


#endif // __VIRTUAL_MPU__
