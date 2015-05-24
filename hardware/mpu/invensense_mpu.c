/*
 * invensense_mpu.c
 *
 * This file contains the driver for the InvenSense MPU6050 device
 * (could be extended to support other InvenSense devices).
 *
 * Created: 24.07.2013 18:16:57
 *  Author: Noah Zerkin (noazark@gmail.com), cpuheater (innovation-labs@appinstall.ch)
 */ 


#if defined(USING_MPU6050)

#include <system.h>
#include "mpu_config.h"
#include "mpu6050.h"



/*
#define MPU_SUCCESS					0 // the operation was successful
#define MPU_UNKNOWN_ERROR			1 // some unknown error with the device or driver (could indicate a bug)
#define MPU_DEVICE_NOT_FOUND		2 // the device is not connected or broken
#define MPU_DEVICE_NOT_RESPONDING	3 // the device stopped responding (unreliable connection, this can also occur if we don't give the device enough time)
#define MPU_BUS_JAMMED				4 // the I2C bus is jammed or currently controlled by another master
#define MPU_BUS_TIMEOUT				5 // the operation timed out
#define MPU_LINK_BROKEN				6 // the device stopped sending ACK
#define MPU_INVALID_DEVICE			7 // the addressed device is not a MPU6050
#define MPU_FIFO_OVERFLOW			8 // an overflow in the device's FIFO occurred and the FIFO was reset
#define MPU_FIFO_CORRUPT			9 // the FIFO packet was corrupt and the FIFO was reset, the received packet should be discarded
#define MPU_NO_SYSTEM_RESOURCES		10 // a new callback couldn't be registered because the array was full
#define MPU_INVALID_ARGUMENT		11 // the passed argument is invalid
#define MPU_BUS_BUSY				12 // the I2C bus is already in use
#define MPU_DMP_SETUP_FAILED		13 // the DMP memory setup verification failed
#define MPU_INVALID_VERSION			14 // the MPU returns 0 as version number (can happen if the device is damaged) edit: not really
*/


//#define MPU_PACKET_LENGTH	42	// length of one FIFO packet, including the footer



typedef int32_t mpu_int_t;

static inline int16_t mpu_int_to_int16(mpu_int_t val) {
	if (is_little_endian())
		return ((int16_t)((val & 0xFFUL) << 8)) | ((int16_t)((val & 0xFF00UL) >> 8));
	return (int16_t)(val >> 16);
};

typedef struct __attribute__((__packed__)) {
	mpu_int_t QuaternionReal;
	mpu_int_t QuaternionI;
	mpu_int_t QuaternionJ;
	mpu_int_t QuaternionK;
	mpu_int_t GyroX;
	mpu_int_t GyroY;
	mpu_int_t GyroZ;
	mpu_int_t AccelX;
	mpu_int_t AccelY;
	mpu_int_t AccelZ;
	uint16_t Footer;
} invensense_mpu_packet_t;


/*
 *
 * A FIFO packet seems to look as follows:
 * 3:0		Quaternion real component (= cos(angle/2))
 * 7:4		Quaternion i component (X of rotation axis)
 * 11:8		Quaternion j component (Y of rotation axis)
 * 15:12	Quaternion k component (Z of rotation axis)
 * 19:16	Raw Gyro
 * 23:20	Raw Gyro
 * 27:24	Raw Gyro
 * 21:28	Raw Accelerometer (X) 0x10000000 is about 1g, 0xF0000000 is about -1g
 * 35:32	Raw Accelerometer (Y)
 * 39:36	Raw Accelerometer (Z)
 *
 * All values are 32-bit big endian signed integers.
 *
 * The quaternion numbers are signed 32-bit values. The value 0x40000000 represents a 1, so the displayable range is about -2 to +2
 * Normally only the upper 16 bits are significant, the lower 16 bits are very noisy.
 *
 */





const char dmpMem[] PROGMEM = MPU_DMP_MEM; // the 2kB DMP RAM file to be uploaded to the device (defined in mpu6050.h)
const char setupData[] PROGMEM = MPU_SETUP_DATA; // descriptors that define the further setup procedure (defined in mpu6050.h)


#if MPU_LOG_VERBOSITY > 0
const char MPU_LOG_STR[] PROGMEM = "MPU";
#endif




// Reads from a register on the device
status_t mpu_reg_read(i2c_device_t *device, uint8_t addr, char *val) {
	return i2c_master_read(device, addr, val, 1);
}

// Writes to a register on the device
status_t mpu_reg_write(i2c_device_t *device, uint8_t addr, char val) {
	return i2c_master_write(device, addr, &val, 1);
}

// Selects an address for DMP memory read/write operations.
//	bank: memory bank to be selected (there are 8 banks, each 256 bytes in size)
//	addr: the address in the bank
status_t mpu_addr_sel(i2c_device_t *device, uint8_t bank, uint8_t addr) {
	status_t status;
	if ((status = mpu_reg_write(device, MEM_BANK_SEL, bank)))
		return status;
	if ((status = mpu_reg_write(device, MEM_START_ADDR, addr)))
		return status;
	return STATUS_SUCCESS;
}


// Writes the 2kB DMP firmware file to the device. The memory is verified afterwards.
status_t mpu_dmp_init(i2c_device_t *device, char *buffer) {
	status_t status;
	
	MPU_LOGI(PSTR("transmit MPU RAM\n"));
	
	for (size_t blockNum = 0; blockNum <= ((sizeof(dmpMem) - 1) >> 8); blockNum++) {
		// load memory block
		memcpy_P(buffer, dmpMem + (blockNum << 8), DMP_BLOCK_SIZE);
		
		// write memory block
		if ((status = mpu_addr_sel(device, blockNum, 0)))
			return status;
		if ((status = i2c_master_write(device, MEM_R_W, buffer, DMP_BLOCK_SIZE)))
			return status;
		
		memset(buffer, 0, DMP_BLOCK_SIZE);
		
		// read memory block
		if ((status = mpu_addr_sel(device, blockNum, 0)))
			return status;
		if ((status = i2c_master_read(device, MEM_R_W, buffer, DMP_BLOCK_SIZE)))
			return status;
		
		// verify memory block
		uint8_t skip = 1; // skip first bytes
		if ((status = memcmp_P(buffer + skip, dmpMem + (((uint16_t)blockNum) << 8) + skip, DMP_BLOCK_SIZE - skip)))
			return STATUS_DATA_CORRUPT;
	}
	
	return STATUS_SUCCESS;
}


//// Writes the 2kB DMP firmware file to the device
//// Returns MPU_SUCCESS if the operation was successful, otherwise a non-zero error code
//uint8_t MPU_DMP_init(void) {
//	uint8_t result;
//	MPU_LOGI(PSTR("transmit MPU RAM\n"));
//	for (uint16_t i = 0; i < sizeof(dmpMem); i++) {
//		if ((i & 0xFF) == 0x00) {	// select next bank every 256th time
//			MPU_ASSERT_SUCCESS(addr_sel(i >> 8, 0));
//			result = I2C_start(MPU_ADDR_W);
//			if (!result) result = I2C_write(MEM_R_W);
//			if (result) break;
//		}
//		result = I2C_write(pgm_read_byte(&(dmpMem[i])));
//		if (result) break;
//		if ((i & 0xFF) == 0xFF)
//			I2C_stop(); // todo: the reference software verifies the bank at this point
//	}
//	I2C_stop();
//	return MPU_status_from_TWI_status(result);
//}


// Writes registers and DMP memory regions in the MPU using predefined setup descriptors
status_t mpu_complex_write(i2c_device_t *device, char *buffer, const char *updateDescriptorsPMEM, size_t descriptorLength) {
	const char *endOfArray = updateDescriptorsPMEM + descriptorLength;
	uint8_t bank, addr, length = 0;
	status_t status;
	
	MPU_LOGI(PSTR("complex MPU write... "));
	
	for (const char *ptr = updateDescriptorsPMEM; ptr < endOfArray; ptr += length) {
		bank = pgm_read_byte(ptr++);
		addr = pgm_read_byte(ptr++);
		
		if (bank < 0xF0) { // memory write
			MPU_LOGI(PSTR("transmit mem, "));
			if ((status = mpu_addr_sel(device, bank, addr)))
				return status;
			length = pgm_read_byte(ptr++);
			addr = MEM_R_W;
		} else { // register write (max 16 bytes)
			MPU_LOGI(PSTR("transmit reg, "));
			length = bank & 0x0F;
		}
		
		memcpy_P(buffer, ptr, length);
		if ((status = i2c_master_write(device, addr, buffer, length)))
			return status;
		
		_delay_ms(2 * MPU_DELAY_MULTIPLIER); // the device seems to act unreliably if we don't delay write operations during setup
		if (addr == 0x6B) _delay_ms(50 * MPU_DELAY_MULTIPLIER); // if we modified the power register, give some extra time
	}
	
	MPU_LOGI(PSTR("complex write done\n"));
	return STATUS_SUCCESS;
}


// Retrieves the current number of bytes in the device FIFO.
status_t mpu_check_fifo(i2c_device_t *device, size_t *count) {
	uint16_t buffer;
	status_t status = i2c_master_read(device, 0x72, (char *)&buffer, 2);
	*count = ((size_t)(buffer & 0xFF) << 8) | (size_t)(buffer >> 8);
	return status;
}

// Clears the FIFO on the device.
status_t mpu_reset_fifo(i2c_device_t *device) {
	char val;
	status_t status;
	if ((status = mpu_reg_read(device, 0x6A, &val)))
		return status;
	val |= (1 << 2);
	if ((status = mpu_reg_write(device, 0x6A, val)))
		return status;
	val &= ~(1 << 2);
	if ((status = mpu_reg_write(device, 0x6A, val)))
		return status;
	return STATUS_SUCCESS;
}


// reads a motion data packet from the device
// Returns MPU_SUCCESS if the operation was successful, otherwise a non-zero error code
// DEPREACED: the download is now conducted in background
//	uint8_t getPacket(uint8_t *packet) {
//		uint8_t result = I2C_start(MPU_ADDR_W);
//		if (!result) result = I2C_write(0x74);
//		if (!result) result = I2C_start(MPU_ADDR_R);
//		
//		for(uint8_t i = 0; i < MPU_PACKET_LENGTH; i++) {
//			if (result) break;
//			result = I2C_read(&(packet[i]), 0);
//		}
//		
//		// check packet footer
//		if (result) return MPU_status_from_TWI_status(result);
//		
//		if (((packet[MPU_PACKET_LENGTH-2] << 8) | packet[MPU_PACKET_LENGTH-1]) != MPU_PACKET_FOOTER) {
//			MPU_ASSERT_SUCCESS(resetFIFO());
//			return MPU_FIFO_CORRUPT;
//		}
//		return MPU_SUCCESS;
//	}


// Checks the connection with the device
// Returns STATUS_SUCCESS if the device was found, otherwise a non-zero error code
status_t mpu_check(i2c_device_t *device) {
	char result;
	status_t status;
	if ((status = mpu_reg_read(device, 0x75, &result)))
		return status;
	if (result != 0x68)
		return STATUS_INCOMPATIBLE;
	MPU_LOGI(PSTR("Found MPU6050\n"));
	return STATUS_SUCCESS;
}



// Used as a callback function for when the packet download completed.
// Checks the packet and executes the FIFO rate callbacks
//	context: a pointer to the mpu structure
//	status: the status of the download operation
void mpu_download_complete(uintptr_t context, status_t status) {

	// load MPU context
	mpu_t *mpu = (mpu_t *)context;
	mpu6050_t *mpu6050 = (mpu6050_t *)(mpu->handle);
	i2c_device_t *device = &(mpu6050->device);
	char *buffer = mpu6050->buffer;
	
	if (!status) {
		if (((invensense_mpu_packet_t *)buffer)->Footer != MPU_PACKET_FOOTER)
			if (!(status = mpu_reset_fifo(device)))
				status = STATUS_DATA_CORRUPT;
	}
	
	if (!status) {
		if (mpu6050->state.firstPacket) {
			_delay_ms(1 * MPU_DELAY_MULTIPLIER);
			status = mpu_addr_sel(device, 0, 0x60);
			buffer[0] = 0x04; buffer[1] = 0x00; buffer[2] = 0x00; buffer[3] = 0x00;
			if (!status) status = i2c_master_write(device, MEM_R_W, buffer, 4);  // this has to do with how fast the acc values settle, if we skip it, the quaternion overdrives after fast turns
			if (!status) mpu6050->state.firstPacket = 0;
			// Wire_send(0x00); Wire_send(0x80); Wire_send(0x00); Wire_send(0x00);
		}
	}	


	mpu6050->state.downloading = 0;

	// We have a potential race condition here but since the MPU cannot complete 2 downloads at the same time, this shouldn't be an issue.
	if (mpu6050->state.handling)
		return; // ignore packet if we're still busy handling another one
	mpu6050->state.handling = 1;
	
	if (!status) {
		invensense_mpu_packet_t *motionData = (invensense_mpu_packet_t *)buffer;

		math_quaternion_t attitude = {
			.w = mpu_int_to_int16(motionData->QuaternionReal),
			.x = mpu_int_to_int16(motionData->QuaternionI),
			.y = mpu_int_to_int16(motionData->QuaternionJ),
			.z = mpu_int_to_int16(motionData->QuaternionK)
		};

		math_ypr_t angularVelocity = {
			.Yaw = mpu_int_to_int16(motionData->GyroZ), // todo: find out why this reads as zero
			.Pitch = mpu_int_to_int16(motionData->GyroX), // todo: verify
			.Roll = mpu_int_to_int16(motionData->GyroY)
		};

		mpu_notify_data(mpu, &attitude, &angularVelocity);

	} else {
		mpu_notify_error(mpu, status);
	}

	mpu6050->state.handling = 0;
}


// Starts downloading new motion data from the device if available. After new data is downloaded, the new-data-callbacks are called.
// It is recommended to execute at FIFO rate or faster, otherwise a FIFO overflow will occur and the FIFO will be reset.
// After calling this function, interrupts must be enabled from time to time, otherwise the download will never complete.
// This function returns immediately. The callback handlers are only called if the download succeeded.
// The entire download from MPU_loop call to the execution of the callback handlers takes about 1.5ms @ 8MHz CPU and 400kHz I2C.
// Call MPU_setup() prior to this function.
// Returns MPU_SUCCESS if the operation was successful, otherwise a non-zero error code. Errors in this function are normally uncritical.
void mpu_interrupt(uintptr_t context) {

	// load MPU context
	mpu_t *mpu = (mpu_t *)context;
	mpu6050_t *mpu6050 = (mpu6050_t *)(mpu->handle);
	i2c_device_t *device = &(mpu6050->device);
	char *buffer = mpu6050->buffer;

	if (!mpu6050->state.running)
		return; // ignore if we weren't running

	// todo: get interrupt status from register 0x3A

	if (mpu6050->state.downloading)
		return; // ignore interrupt if we're busy

	size_t fifoCount;
	status_t status;
	if ((status = mpu_check_fifo(device, &fifoCount))) {
		mpu_notify_error(mpu, status);
		return;
	}

	// ignore if there isn't a full packet available yet
	if (fifoCount < sizeof(invensense_mpu_packet_t)) {
		if (++(mpu6050->fifoEmpty) >= 200) // if the FIFO is empty several times in a row, reset FIFO
			if ((status = mpu_reset_fifo(device)))
				mpu_notify_error(mpu, status);
		return;
	}

	mpu6050->state.downloading = 1;
	mpu6050->fifoEmpty = 0;

	i2c_master_read_async(device, 0x74, buffer, sizeof(invensense_mpu_packet_t), mpu_download_complete, context);
}





// initializes and starts the device and its DMP
// required actions prior to this call:
//	1. init the log system (not required if LOG_VERBOSITY = 0)
//	2. init the I2C driver (a clock speed of up to 400kHz should be acceptable)
//	3. wait for 100ms (this gives the device time to get ready)
status_t invensense_mpu_reset(mpu_t *mpu) {
	mpu6050_t *mpu6050 = (mpu6050_t *)(mpu->handle);
	i2c_device_t *device = &(mpu6050->device);
	char *buffer = mpu6050->buffer;

	status_t status = STATUS_SUCCESS;
	char mpuVer, mpuRev;

	mpu6050->state.running = 0;
	mpu6050->state.downloading = 0;
	mpu6050->state.handling = 0;
	mpu6050->state.firstPacket = 1;

	_delay_ms(100 * MPU_DELAY_MULTIPLIER); // give the device some time to start up

	if (mpu_check(device) != STATUS_SUCCESS) {
		MPU_LOGE(PSTR("MPU-6050 not found!\n"));
		return STATUS_DEVICE_NOT_FOUND;
	}
	if (!status) status = mpu_reg_write(device, 0x6B, 0xC0);	// reset device and put it into sleep mode
	_delay_ms(10 * MPU_DELAY_MULTIPLIER); // caution: the device needs some time here, else the next instruction will fail
	if (!status) status = mpu_reg_write(device, 0x6C, 0x00);	// wake up all gyros and accs
	if (!status) status = mpu_reg_write(device, 0x6B, 0x00); // wake up device

	if (!status) status = mpu_reg_read(device, 0x0C, &mpuVer); // read product version - only silicon revision B1 and later have this register
	if (!status) status = mpu_addr_sel(device, 0x70, 0x06);
	if (!status) status = mpu_reg_read(device, MEM_R_W, &mpuRev); // read product revision
	MPU_LOGI("silicon: %d.%d", (mpuVer & 0x0F), (mpuRev >> 2));

	//if (!(mpu_ver & 0x0F)) return MPU_INVALID_VERSION;

	// read factory gyro offset temperature coefficient and do nothing with it
	//mpu_reg_read(device, 0x00, &temp); // gyro X, 0x81 on test device (these are probably supposed to be masked by 0x7E and right shifted by one)
	//mpu_reg_read(device, 0x01, &temp); // gyro Y, 0x00 on test device
	//mpu_reg_read(device, 0x02, &temp); // gyro Z, 0x00 on test device

	// todo: init slaves on the device's aux I2C here if required

	if (!status) status = mpu_dmp_init(device, buffer);
	if (!status) status = mpu_complex_write(device, buffer, setupData, sizeof(setupData));

	_delay_ms(20 * MPU_DELAY_MULTIPLIER);

	if (!status) status = mpu_reset_fifo(device); // move to start procedure

	// map the hardware interrupt pin
	interrupt_register(mpu6050->intPin, 1, mpu_interrupt, (uintptr_t)mpu);

	return STATUS_SUCCESS;
}


// Starts periodic data acquisitions from the MPU
status_t invensense_mpu_start(mpu_t *mpu) {
	status_t status;
	if (!(status = mpu_reset_fifo(&(((mpu6050_t *)(mpu->handle))->device))))
		((mpu6050_t *)(mpu->handle))->state.running = 1;
	return status;
}


// Stops receiving data from the MPU.
// A data update signal may still trigger if a download is in progress.
status_t invensense_mpu_stop(mpu_t *mpu) {
	((mpu6050_t *)(mpu->handle))->state.running = 1;
	return STATUS_SUCCESS;
}



#endif // USING_MPU6050
