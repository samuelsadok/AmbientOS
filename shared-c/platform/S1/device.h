/*
 * Describes the S1 quadrocopter hardware for both the ATXMega and CSR1010 MCU.
 *
 * Created: 07.03.14
 *  Author: Samuel
 */ 
#ifndef __DEVICE_H__
#define __DEVICE_H__


/* interface between flight controller and baseband processor */

#define FLIGHT_CONTROLLER_ADDRESS	(0x42)

#define DFU_VERSION					(1)
#define DFU_INFO_REG				(0xFFF8)
#define DFU_PROGMEM_INFO_REG		(0xFFF9)
#define DFU_PLATFORM_REG			(0xFFFA)
#define DFU_PLATFORM_LENGTH_REG		(0xFFFB)
#define DFU_APPNAME_REG				(0xFFFC)
#define DFU_APPNAME_LENGTH_REG		(0xFFFD)
#define DFU_VERSION_REG				(0xFFFE)
#define DFU_VERSION_LENGTH_REG		(0xFFFF)

#define BASEBAND_WAKE_ACTIVE_HIGH	(1)



#if defined(__XAP__)
/* Device descriptions and properties for baseband processor */

#define F_CPU	(16000000UL)



//#define LED_R			PWM_1
//#define LED_L			PWM_2
//#define LED_R_GPIO		3
//#define LED_L_GPIO		4
#define PIO_LED_L        3
#define PIO_LED_R        4





/* built-in I2C master */

#define BUILTIN_I2C_SDA				(29)
#define BUILTIN_I2C_SCL				(28)
#define BUILTIN_I2C_MASTER_SPEED	(400000UL)



/* on-board I2C eeprom */

#define I2C_EEPROM_WRITE_PAGE_SIZE		(128)
#define I2C_EEPROM_WRITE_CYCLE_TIME		(5000) // 5ms

#define PMEM_SIZE						(0x10000) // 512kbit EEPROM




#elif defined(__AVR_ARCH__)
/* Device description and properties for the flight controller */

#define F_CPU	(32000000UL)


/* Power monitor/control pins */

#define POWER_SWITCH_PIN			(IOPORT_CREATE_PIN(PORTA, 0)) // power button
#define POWER_SWITCH_ACTIVE_HIGH	(1)
#define POWER_CONTROL_PIN			(IOPORT_CREATE_PIN(PORTA, 1)) // power supply N-channel MOSFET
#define POWER_CONTROL_ACTIVE_HIGH	(1)
#define POWER_WAKE_PIN				(IOPORT_CREATE_PIN(PORTD, 6)) // baseband wake pin
#define POWER_WAKE_ACTIVE_HIGH		(BASEBAND_WAKE_ACTIVE_HIGH)



/* on-board motion sensor */

#define MPU6050_ADDR_PIN			(0)
#define MPU6050_INT_PIN				IOPORT_CREATE_PIN(PORTC, 2)
#define CREATE_ONBOARD_MPU()		mpu6050_t onBoardMpu = CREATE_MPU6050(CREATE_TWIC_DEVICE(MPU6050_ADDR(MPU6050_ADDR_PIN), 1), MPU6050_INT_PIN)
#define ONBOARD_MPU					APPLY_MPU6050(&onBoardMpu)



/* built-in I2C slave controller */

#define BUILTIN_TWIC_SPEED		(400000UL)
#define BUILTIN_TWIC_FAST_MODE	(0)
#define BUILTIN_TWIC_ADDRESS	FLIGHT_CONTROLLER_ADDRESS
#define BUILTIN_TWIC_ADDR_BYTES	(2)
#define BUILTIN_TWIC_INT_LEVEL	TWI_SLAVE_INTLVL_MED_gc
#define TWIC_ENDPOINTS			I2C_ENDPOINTS // defined in application.h



/* Motors */

#define CREATE_MOTOR(_gpio)	CREATE_PWM_MOTOR((_gpio))

#define MOTOR_FL_PIN				IOPORT_CREATE_PIN(PORTC, 5)
#define MOTOR_FR_PIN				IOPORT_CREATE_PIN(PORTC, 4)
#define MOTOR_BL_PIN				IOPORT_CREATE_PIN(PORTC, 7)
#define MOTOR_BR_PIN				IOPORT_CREATE_PIN(PORTC, 6)
#define MOTOR_PINS_ACTIVE_HIGH		(1)



#else // defined([arch])
#	error "there is no such architecture on this device"
#endif

#endif // __DEVICE_H__
