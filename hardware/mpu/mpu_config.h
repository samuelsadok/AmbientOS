/*
 * mpu_config.h
 *
 * Use this file to configure all MPU driver settings
 *
 * Created: 24.07.2013 18:13:31
 *  Author: cpuheater
 */ 


#ifndef MPU_CONFIG_H_
#define MPU_CONFIG_H_


#define MPU_SAMPLE_RATE_DIVIDER		3	// divides the gyro sample rate (which is normally 1kHz) - range: 3...256 - original: 5
#define MPU_FIFO_RATE_DIVIDER		1	// another divider - range: 1...20 (theoretically 65536) - original: 11
// the final FIFO rate will be:    fifo rate = 1kHz (or 8kHz) / sample rate divider / fifo rate divider

#define MPU_ORIENT_XYZ

#define MPU_MAX_FIFO_RATE_CALLBACKS	2 // maximum number of FIFO rate callbacks that will be registered using MPU_register_FIFO_rate_callback. range: 0...255

#define MPU_DELAY_MULTIPLIER	2

#define MPU_LOG_VERBOSITY	1



#endif /* MPU_CONFIG_H_ */