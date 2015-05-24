/*
 * invensense_mpu.h
 *
 * Created: 24.07.2013 18:15:19
 *  Author: cpuheater (innovation-labs@appinstall.ch)
 */ 


#ifndef __INVENSENSE_MPU_H__
#define __INVENSENSE_MPU_H__


typedef struct {
	struct {
		unsigned int running : 1;			// the driver and device is running
		unsigned int downloading : 1;		// data is currently being downloaded from the MPU
		unsigned int handling : 1;			// the data callbacks are currently being executed
		unsigned int firstPacket : 1;		// the first packet has not yet been received
	} state;
	i2c_device_t device;					// the underlying I2C device
	ioport_pin_t intPin;					// location where the MPU interrupt pin is connected
	int fifoEmpty;							// the number of times the FIFO was found empty in a row
	char buffer[256]; // the size of this buffer must be equal to the DMP memory bank size
} mpu6050_t;


status_t invensense_mpu_reset(mpu_t *mpu);
status_t invensense_mpu_start(mpu_t *mpu);
status_t invensense_mpu_stop(mpu_t *mpu);



// Instantiates an InvenSense MPU6050 device.
//	device: A i2c_slave_t structure that represents the physical device.
//	intPin: The local pin to which the MPU's interrupt pin is connected.
#define CREATE_MPU6050(_device, _intPin) {		\
	.state = {									\
		.running = 0,							\
		.downloading = 0,						\
		.handling = 0							\
		},										\
	.device = _device,							\
	.intPin = (_intPin)							\
}

// Applies an InvenSense MPU6050 context to a universal MPU structure.
//	mpu6050: A pointer to a structure of the type mpu6050_t
#define APPLY_MPU6050(mpu6050) {		\
	.handle = (uintptr_t)(mpu6050),		\
	.reset = invensense_mpu_reset,		\
	.start = invensense_mpu_start,		\
	.stop = invensense_mpu_stop			\
}

#define MPU6050_ADDR(addrPin)	(0x68 | (addrPin))	// I2C address of the device (depends on the addr-pin)


#if defined(USING_MPU6050)
#include "invensense_mpu.h"
#endif

#endif /* __INVENSENSE_MPU_H__ */
