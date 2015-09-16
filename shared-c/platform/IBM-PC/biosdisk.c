
#include <system.h>
#include "biosdisk.h"


#define DBG_INIT(...)	LOGI(__VA_ARGS__)
//#define DBG_INIT(...)
#define DBG_INIT__(...)	LOGI__(__VA_ARGS__)
//#define DBG_INIT__(...)



typedef struct __attribute__((__packed__))
{
	uint16_t bufferSize;
	uint16_t flags;
	uint32_t cylinders;
	uint32_t heads;
	uint32_t sectorsPerTrack;
	uint64_t totalSectors;
	uint16_t bytesPerSector;
} biosdisk_geometry_t;



// sets up the transfer block used by int 13h
void biosdisk_setup_transfer(uint64_t startSector, uint64_t sectorCount, realmode_context_t *regs) {
	*(uint16_t *)&realmodeBuffer2[0] = 0x0010;
	*(uint16_t *)&realmodeBuffer2[2] = sectorCount;
	realmode_buffer_ref((uint16_t *)&realmodeBuffer2[6], (uint16_t *)&realmodeBuffer2[4]);
	realmode_buffer2_ref(&regs->ds, (uint16_t *)&regs->esi);
	*(uint64_t *)&realmodeBuffer2[8] = startSector;
}


// Reads sectors from a BIOS drive. The sectors must be within disk boundaries.
status_t biosdisk_read(disk_t *disk, uint64_t startSector, uint64_t sectorCount, char *buffer) {
	assert(buffer);
	assert(disk);
	assert(disk->sectorCount >= startSector + sectorCount);

	realmode_context_t regs;
	realmode_reset(&regs);

	while (sectorCount) {
		uint64_t count = min(sectorCount, 0x7F); // transfer at most 127 sectors at once

		// set up registers and disk transfer command
		regs.eax = 0x4200;
		regs.edx = (uint8_t)disk->reference;
		biosdisk_setup_transfer(startSector, count, &regs);

		// issue read command
		if (realmode_int(0x13, &regs) & 1) return STATUS_DISK_READ_ERROR;
		if (regs.eax & 0xFF) return STATUS_DISK_READ_ERROR;

		// copy to buffer
		memcpy(buffer, realmodeBuffer, count * disk->bytesPerSector);
		startSector += count;
		sectorCount -= count;
		buffer += count * disk->bytesPerSector;
	}

	return STATUS_SUCCESS;
}


// Writes sectors to a BIOS drive. The sectors must be within disk boundaries.
status_t biosdisk_write(disk_t *disk, uint64_t startSector, uint64_t sectorCount, char *buffer) {
	assert(buffer);
	assert(disk);
	assert(disk->sectorCount >= startSector + sectorCount);

	realmode_context_t regs;
	realmode_reset(&regs);

	while (sectorCount) {
		uint64_t count = max(sectorCount, 0x7F); // transfer at most 127 sectors at once

		// copy to buffer
		memcpy(realmodeBuffer, buffer, count * disk->bytesPerSector);

		// set up registers and disk transfer command
		regs.eax = 0x4300;
		regs.edx = (uint8_t)disk->reference;
		biosdisk_setup_transfer(startSector, count, &regs);

		// issue read command
		if (realmode_int(0x13, &regs) & 1) return STATUS_DISK_WRITE_ERROR;
		if (regs.eax & 0xFF) return STATUS_DISK_WRITE_ERROR;

		startSector += count;
		sectorCount -= count;
		buffer += count * disk->bytesPerSector;
	}

	return STATUS_SUCCESS;
}



// Checks if the specified disk is installed.
// Returns a non-zero error code if this is not the case.
status_t biosdisk_installation_check(uint64_t disk) {
	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x4100;
	regs.ebx = 0x55AA;
	regs.edx = disk;
	if (realmode_int(0x13, &regs) & 1) return STATUS_DEVICE_ERROR; // ignore on error
	if ((regs.eax & 0xFF00) == 0x0100) return STATUS_DEVICE_ERROR; // ignore if not supported
	if ((regs.ebx & 0xFFFF) != 0xAA55) return STATUS_DEVICE_ERROR; // ignore if not installed
	if (!(regs.ecx & 1)) return STATUS_DEVICE_ERROR; // ignore if extended functions not supported
	return STATUS_SUCCESS;
}



// Initializes and returns a list of installed disks.
// If the preferred disk was not found, a scan is performed instead
// and all installed disks are initialized.
// If count is zero, no disk was found or an error occured.
disk_t *biosdisk_init(uint64_t preferredDisk, size_t *count) {
	*count = 0;
	realmode_context_t regs;
	char available[128] = { 0 };

	DBG_INIT("biosdisk installation check (installed: y/n)...");

	if (biosdisk_installation_check(preferredDisk) == STATUS_SUCCESS) {
		// only init preferred disk
		DBG_INIT("  preferred disk installed");
		available[preferredDisk - 128] = 1;
		*count = 1;

	} else {
		// scan for installed disks
		for (int i = 128; i < 256; i++) {
			DBG_INIT__(" %x8 ", (uint8_t)i);
			if (biosdisk_installation_check(i) == STATUS_SUCCESS) {
				DBG_INIT__("(y),");
				available[i - 128] = 1;
				(*count)++;
			} else {
				DBG_INIT__("(n),");
			}
		}
		DBG_INIT__("\n");
	}

	disk_t *disks = (disk_t *)malloc(*count * sizeof(disk_t));
	if (!disks) return (*count = 0), NULL;

	DBG_INIT("biosdisk geometry check (v: valid)...");

	// query metrics of each installed disk
	*count = 0;
	for (int i = 128; i < 256; i++) {
		if (!available[i - 128]) continue;
		DBG_INIT__(" %x8", (uint8_t)i);
		realmode_reset(&regs);
		regs.eax = 0x4800;
		regs.edx = i;
		realmode_buffer_ref(&regs.ds, (uint16_t *)&regs.esi);
		*(uint16_t *)realmodeBuffer = 0x1A;
		if (realmode_int(0x13, &regs) & 1) continue;
		if (regs.eax & 0xFF00) continue;
		uint16_t bytesPerSector = ((biosdisk_geometry_t *)realmodeBuffer)->bytesPerSector;
		if (bytesPerSector < 512) continue;
		DBG_INIT__(" (v)");
		uint16_t flags = ((biosdisk_geometry_t *)realmodeBuffer)->flags;
		disks[(*count)++] = (disk_t) {
			.reference = i,
				.cylinders = ((biosdisk_geometry_t *)realmodeBuffer)->cylinders,
				.heads = ((biosdisk_geometry_t *)realmodeBuffer)->heads,
				.sectorsPerTrack = ((biosdisk_geometry_t *)realmodeBuffer)->sectorsPerTrack,
				.sectorCount = ((biosdisk_geometry_t *)realmodeBuffer)->totalSectors,
				.hasCHS = ((flags >> 1) & 1) && !((flags >> 6) & 1),
				.bytesPerSector = bytesPerSector,
				.read = biosdisk_read,
				.write = biosdisk_write
		};
	}

	DBG_INIT__("\n");

	return disks;
}
