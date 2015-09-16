/*
*
*
* created: 18.02.15
*
*/

#ifndef __DFU_H__
#define __DFU_H__

#ifdef USING_DFU

extern volatile bool dfuOnHold;
extern volatile bool dfuDidSwitch;
#ifdef DFU_SLAVES
extern i2c_device_t dfuSlaves[];
#endif
extern const size_t dfuSlaveCount;

void dfu_init_nvm(void);
void dfu_init(void);
void dfu_invalidate_app(void);

#ifdef USING_BOOTLOADER

status_t dfu_check_for_app(void);
status_t dfu_try_launch_app(void);
status_t dfu_validate_app(size_t appSize, uint32_t checksum);

#else

void __attribute__((__noreturn__)) dfu_launch_bootloader(void);

#endif

#endif


#endif // __DFU_H__
