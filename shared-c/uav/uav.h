/*
*
* Provides a universal interface to control local and remote UAVs.
*
* created: 10.03.15
*
*/
#ifndef __UAV_H__
#define __UAV_H__


// Represents a UAV controller configuration
typedef struct
{
	// sensor kalman filter configuration
	uint8_t reactivenessY, predictionTimeY; // yaw
	uint8_t reactivenessR, predictionTimeR; // pitch
	uint8_t reactivenessP, predictionTimeP; // roll

	// attitude PID controller configuration
	float PValY, IValY, DValY, maxIY; // yaw
	float PValP, IValP, DValP, maxIP; // pitch
	float PValR, IValR, DValR, maxIR; // roll
} uav_config_t;


struct uav_driver_t;

// Represents a local or remote UAV
typedef struct uav_t
{
	void *context;
	struct uav_driver_t *driver;		// must not be NULL
} uav_t;


// Represents a driver to control a UAV on a specific port.
typedef struct uav_driver_t
{
	status_t(*init)(uav_t *uav);
	status_t(*config)(uav_t *uav, uav_config_t *config);
	status_t(*on)(uav_t *uav);
	status_t(*control)(uav_t *uav, float throttle, math_ypr_t *attitude);
	status_t(*off)(uav_t *uav);
} uav_driver_t;


static inline status_t uav_init(uav_t *uav) {
	if (!uav->driver->init)
		return STATUS_SUCCESS;
	return uav->driver->init(uav);
}

static inline status_t uav_config(uav_t *uav, uav_config_t *config) {
	if (!uav->driver->config)
		return STATUS_INVALID_OPERATION;
	return uav->driver->config(uav, config);
}

static inline status_t uav_on(uav_t *uav) {
	if (!uav->driver->on)
		return STATUS_INVALID_OPERATION;
	return uav->driver->on(uav);
}

static inline status_t uav_control(uav_t *uav, float throttle, math_ypr_t *attitude) {
	if (!uav->driver->control)
		return STATUS_INVALID_OPERATION;
	return uav->driver->control(uav, throttle, attitude);
}

static inline status_t uav_off(uav_t *uav) {
	if (!uav->driver->off)
		return STATUS_INVALID_OPERATION;
	return uav->driver->off(uav);
}



#ifdef USING_LOCAL_UAV
#	include "local_uav.h"
#endif


#endif // __UAV_H__
