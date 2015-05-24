/*
* Enables a unified way of managing the main application from within a bootloader.
* System Requirements:
*	read-write program memory
*	a few bytes of no-volatile memory
* 
* Configuration Options:
*	USING_BOOTLOADER	enable bootloader features
*	USING_DFU_MASTER	enable controlling an I2C device that has a DFU mode
*	APP_OFFSET			offset (in bytes) of the main application in program memory
*	NVM_APPINFO_OFFSET	offset (in bytes) of a location in NVM where 6 bytes are reserved
*	NVM_APPINFO_LENGTH	number of reserved bytes for the appinfo block (must be >= 6)
*	DFU_HOLD_CONDITION	a function or constant that returns 1 as long as the user interrupts the boot process (usually a button input pin but can be 0)
*	DFU_SLAVES			an array of i2c_device_t structs that specify the I2C DFU slaves connected to this device
*
* created: 18.02.15
*
*/


#include <system.h>


#ifdef USING_DFU

#ifndef USING_NVM
#  error "NVM must be enabled to use DFU"
#endif

#include <hardware/nvm.h>

#define APPSTATE_PRIORITY	(0xB007)	// the application is valid and will be loaded under any circumstances
#define APPSTATE_VALID		(0xC0DE)	// the application is valid and will be loaded when appropriate (equivalent to priority on some systems)
#define APPSTATE_INVALID	(0xDEAD)	// the application is not bootable


typedef struct
{
	uint16_t appState;
	uint16_t appOffset;

	// LSB: 0: was in application, 1: was in bootloader, 0xFF: unknown
	// MSB: 0: use normal procedure to launch app or bootloader, 1: temporarily force bootloader (except if there is a priority application)
	uint16_t bootloaderState;
} app_info_t;

STATIC_ASSERT(NVM_APPINFO_LENGTH >= sizeof(app_info_t) * WORDSIZE, "reserved NVM region too small");

#ifdef DFU_SLAVES
i2c_device_t dfuSlaves[] = DFU_SLAVES; // list of all DFU slaves connected to this master
const size_t dfuSlaveCount = sizeof(dfuSlaves) / sizeof(i2c_device_t);
#else
const size_t dfuSlaveCount = 0;
#endif

volatile bool dfuDidSwitch = 0;


// Inits the NVM field to keep track of an application switch.
// If checking for a switch after this call, it's always reported true.
void EXTENDED_TEXT dfu_init_nvm(void) {
	uint16_t bootloaderState = 0x00FF;
	nvm_write(NVM_APPINFO_OFFSET + offsetof(app_info_t, bootloaderState) * WORDSIZE, (char *)&bootloaderState, sizeof(uint16_t) * WORDSIZE);
}


// Returns true if since the last call the device switched from DFU mode to
// a normal application or the other way around.
// This must be called once when launching the bootloader or application.
bool EXTENDED_TEXT dfu_did_switch(void) {
	uint16_t bootloaderState;
	nvm_read(NVM_APPINFO_OFFSET + offsetof(app_info_t, bootloaderState) * WORDSIZE, (char *)&bootloaderState, sizeof(uint16_t) * WORDSIZE);
	char didSwitch = bootloaderState & 0xFF;
	bootloaderState &= 0xFF00;

#ifdef USING_BOOTLOADER
	if (didSwitch != 0xFF)
		didSwitch = !didSwitch;
	bootloaderState |= 1;
#endif

	nvm_write(NVM_APPINFO_OFFSET + offsetof(app_info_t, bootloaderState) * WORDSIZE, (char *)&bootloaderState, sizeof(uint16_t) * WORDSIZE);
	return didSwitch;
}


// Invalidates the current application.
// This must be called immediately before writing to program memory,
// so in case the update is cancelled, the device remains locked into DFU mode.
void EXTENDED_TEXT dfu_invalidate_app(void) {
	app_info_t appInfo = { .appState = APPSTATE_INVALID };
	nvm_write(NVM_APPINFO_OFFSET, (char *)&appInfo, sizeof(appInfo) * WORDSIZE);
}


#ifdef USING_BOOTLOADER


#ifdef DFU_SLAVES

// Commands all slave devices to enter DFU mode.
status_t EXTENDED_TEXT dfu_master_enter(void) {
	status_t status;

	dfu_info_t cmd = {
		.state = DFU_STATE_TEMP,
		.domain = 0xFFFF
	};

	for (size_t i = 0; i < dfuSlaveCount; i++)
		if ((status = i2c_master_write(dfuSlaves + i, DFU_INFO_REG, (char *)&cmd, sizeof(dfu_info_t) * WORDSIZE)))
			return status;

	return STATUS_SUCCESS;
}

// Commands all slave devices to exit DFU mode.
// The slave may either refuse (if it has no valid application),
// delay (if the user interrupts the boot process) or accept the command.
status_t EXTENDED_TEXT dfu_master_exit(void) {
	status_t status;
	dfu_info_t info;

	for (size_t i = 0; i < dfuSlaveCount; i++) {
		if (!(i2c_master_read(dfuSlaves + i, DFU_INFO_REG, (char *)&info, sizeof(dfu_info_t) * WORDSIZE))) {
			if (info.state == DFU_STATE_HOLD)
				return STATUS_IN_PROGRESS;
			else if (info.state == DFU_STATE_NONE)
				continue;
		}

		info.state = DFU_STATE_TEMP;
		info.domain = 0xFFFF;

		if ((status = i2c_master_write(dfuSlaves + i, DFU_INFO_REG, (char *)&info, sizeof(dfu_info_t) * WORDSIZE)))
			return status;
	}

	return STATUS_SUCCESS;
}

#endif // DFU_SLAVES



// Returns true if a valid application is present.
// If a priority application is present, it is launched immediately and this function does not return.
// Must not be called before nvm_init().
status_t EXTENDED_TEXT dfu_check_for_app(void) {
	app_info_t appInfo;
	nvm_read(NVM_APPINFO_OFFSET, (char *)&appInfo, sizeof(appInfo) * WORDSIZE);

	// if there is a priority application, start it immediately
	if (appInfo.appState == APPSTATE_PRIORITY)
		bootloader_launch_app(appInfo.appOffset);

	// if the bootloader mode was temporarily forced, clear flag and return with error
	if (appInfo.bootloaderState >> 8) {
		appInfo.bootloaderState &= 0xFF;
		nvm_write(NVM_APPINFO_OFFSET, (char *)&appInfo, sizeof(appInfo) * WORDSIZE);
		return STATUS_INVALID_OPERATION;
	}

	// if the application is valid, report success
	// (when booting, the launch still needs to be authorized)
	if (appInfo.appState == APPSTATE_VALID)
		return STATUS_SUCCESS;

	return STATUS_FILE_NOT_FOUND;
}


// Launches the main application.
// This must not be called if there is not valid application.
// Does not return.
void __attribute__((__noreturn__)) EXTENDED_TEXT dfu_launch_app(void) {
	app_info_t appInfo;
	nvm_read(NVM_APPINFO_OFFSET + offsetof(app_info_t, appOffset) * WORDSIZE, (char *)&appInfo, sizeof(uint16_t) * WORDSIZE);
	bootloader_launch_app(appInfo.appOffset);
}



// Launches the application as soon as the boot hold condition is no longer met
// (e.g. the user released the power button and the slave agreed to exit DFU mode).
// If a time-out occurs, the function returns.
status_t EXTENDED_TEXT dfu_try_launch_app(void) {
	status_t status;

	timer_t holdTimer = CREATE_TIMER(5000, NULL, NULL); // if the boot process is halted for >5s, the device enters DFU mode
	timer_t errorTimer = CREATE_TIMER(100, NULL, NULL); // if any DFU slaves fail to exit DFU for >100ms, the device enters DFU mode
	timer_start(&holdTimer);
	timer_start(&errorTimer);

	do {
		status = STATUS_SUCCESS;

#ifdef DFU_SLAVES
		// tell the DFU slaves to exit DFU mode (they may comply, refuse or delay)
		status = dfu_master_exit();
#endif

		// check local hold condition
		if (!status)
			if ((DFU_HOLD_CONDITION))
				status = STATUS_IN_PROGRESS;

		// break on timeout
		if (!status && status != STATUS_IN_PROGRESS && timer_has_expired(&errorTimer))
			break;
		if (timer_has_expired(&holdTimer)) {
			status = STATUS_TIMEOUT;
			break;
		}

	} while (status);

	timer_stop(&holdTimer);
	timer_stop(&errorTimer);

	if (!status)
		dfu_launch_app();

	return status;
}


// Compares the application at PMEM_APP_OFFSET against the provided checksum.
// The checksum method used is platform dependent.
//	appSize: the size of the app in bytes (must be a multiple of 2)
//	checksum: the checksum to compare against
//	buffer: an empty buffer of at least PMEM_PAGE_SIZE bytes length
// Returns STATUS_SUCCESS if the application is valid.
status_t EXTENDED_TEXT dfu_validate_app(size_t appSize, uint32_t checksum) {
	status_t status;
	uint32_t actualChecksum = 0;

	if ((status = pmem_checksum(APP_OFFSET, appSize, &actualChecksum)))
		return status;

	if (actualChecksum != checksum)
		return STATUS_DATA_CORRUPT;

	// mark app as bootable
	app_info_t appInfo = {
		.appState = APPSTATE_VALID,
		.appOffset = APP_OFFSET
	};
	nvm_write(NVM_APPINFO_OFFSET, (char *)&appInfo, sizeof(app_info_t) * WORDSIZE);

	return STATUS_SUCCESS;
}




volatile bool dfuOnHold = 1;

// When called in the bootloader, carries out any DFU-related tasks.
// Under normal conditions, this will launch the main application and not return.
void EXTENDED_TEXT dfu_init(void) {
	if (!dfu_check_for_app())
		dfu_try_launch_app();
#ifdef DFU_SLAVES
	dfu_master_enter();
#endif
	dfuDidSwitch = dfu_did_switch();
	dfuOnHold = 0;
	bootloader_init();
}

#else // USING_BOOTLOADER

volatile bool dfuOnHold = 0;

// In normal application, no DFU init action is required.
void dfu_init(void) {
	dfuDidSwitch = dfu_did_switch();
}

// Launches the bootloader. If a valid application is present, the bootloader is only entered once.
// This function does not return.
void __attribute__((__noreturn__)) dfu_launch_bootloader(void) {
	uint16_t bootloaderState;
	nvm_read(NVM_APPINFO_OFFSET + offsetof(app_info_t, bootloaderState) * WORDSIZE, (char *)&bootloaderState, sizeof(uint16_t) * WORDSIZE);
	bootloaderState |= 0x0100;
	nvm_write(NVM_APPINFO_OFFSET + offsetof(app_info_t, bootloaderState) * WORDSIZE, (char *)&bootloaderState, sizeof(uint16_t) * WORDSIZE);
	__reset();
}

#endif // USING_BOOTLOADER

#endif // USING_DFU
