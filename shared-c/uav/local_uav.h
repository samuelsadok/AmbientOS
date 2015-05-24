/*
*
*
* created: 28.02.15
*
*/

#ifndef __LOCAL_UAV_H__
#define __LOCAL_UAV_H__

#ifdef USING_MPU
void local_uav_set_mpu(mpu_t *mpu);
#endif

extern uav_t localUAV;

#endif // __LOCAL_UAV_H__
