/*
*
*
* created: 02.02.15
*
*/

#include <system.h>
#include "filesystem.h"

#ifdef USING_FILESYSTEM


typedef struct
{
	uint8_t flags;
	uint8_t h1, s1, c1;
	uint8_t type;
	uint8_t h2, s2, c2;
	uint32_t startSector;
	uint32_t sectorCount;
} partition_entry_t;


// Calculates the linear sector address (LBA) from a CHS address
status_t disk_get_lba(disk_t *disk, uint16_t cylinder, uint16_t head, uint16_t sector, uint64_t *lba) {
	if (!disk->hasCHS) return STATUS_NOT_SUPPORTED;
	if (cylinder >= disk->cylinders) return STATUS_OUT_OF_RANGE;
	if (head >= disk->heads) return STATUS_OUT_OF_RANGE;
	if ((!sector) || (sector > disk->sectorsPerTrack)) return STATUS_OUT_OF_RANGE;
	return ((uint64_t)cylinder * (uint64_t)disk->heads + (uint64_t)head) * (uint64_t)disk->sectorsPerTrack + (uint64_t)sector - 1;
}


// Returns a list of all volumes on the current drive.
// The returned list is empty in case of an error.
// The list must be released using free().
// While a volume is in use, the underlying disk struct must not be freed.
volume_t* volume_init(disk_t *disk, size_t *count) {
	//uint64_t ticks;
	//for (;;) {
	//	LOGI("count to 50");
	//	ticks = systemTicks;
	//	while (ticks + 50 > systemTicks);
	//}

	// try to load MBR
	char mbr[disk->bytesPerSector];
	if (disk->read(disk, 0, 1, mbr))
		return *count = 0, NULL;

	//LOGI("mbr was read");
	//ticks = systemTicks;
	//while (ticks + 20 > systemTicks);

	volume_t volumes[4];
	*count = 0;
	if ((mbr[0] == (char)0xE9) || (mbr[0] == (char)0xEB)) { // a VBR must start with these bytes
		volumes[(*count)++] = (volume_t) { .disk = disk, .startSector = 0, .sectorCount = disk->sectorCount };
	} else {
		// read partition table entries
		partition_entry_t *table = (partition_entry_t *)(mbr + 0x1BE);
		for (int i = 0; i < 4; i++)
			if (!(table[i].flags & 0x7F))
				volumes[(*count)++] = (volume_t) { .disk = disk, .startSector = table[i].startSector, .sectorCount = table[i].sectorCount };
	}

	// copy to output
	volume_t *list = (volume_t *)malloc(*count * sizeof(volume_t));
	if (!list) return (*count = 0), NULL;
	for (int i = 0; i < *count; i++)
		list[i] = volumes[i];
	return list;
}


// Reads or writes a fragment of a sector
status_t volume_readwrite_frag(volume_t *volume, int read, uint64_t sector, uint64_t offset, uint64_t count, char *buffer) {
	status_t status;

	char tempBuf[volume->disk->bytesPerSector];

	if ((status = volume->disk->read(volume->disk, volume->startSector + sector, 1, tempBuf)))
		return status;

	if (read) {
		memcpy(buffer, tempBuf + offset, count);
	} else {
		memcpy(tempBuf + offset, buffer, count);
		if ((status = volume->disk->write(volume->disk, volume->startSector + sector, 1, tempBuf)))
			return status;
	}

	return STATUS_SUCCESS;
}


// Reads or writes from the specified volume.
status_t volume_readwrite(volume_t *volume, int read, uint64_t offset, uint64_t count, char *buffer) {
	assert(volume);
	assert(buffer);
	assert(offset <= volume->sectorCount * volume->disk->bytesPerSector);
	assert(offset + count <= volume->sectorCount * volume->disk->bytesPerSector);
	if (!count) return STATUS_SUCCESS;
	status_t status;

	// calculate some metrics
	int startMissAligned = ((offset % volume->disk->bytesPerSector) ? 1 : 0);
	int endMissAligned = (((offset + count) % volume->disk->bytesPerSector) ? 1 : 0);
	uint64_t firstSector = offset / volume->disk->bytesPerSector;
	uint64_t bytesInFirstSector = ((firstSector + startMissAligned) * volume->disk->bytesPerSector) - offset;
	uint64_t fullSectors = ((count > bytesInFirstSector) ? ((count - bytesInFirstSector) / volume->disk->bytesPerSector) : 0);
	uint64_t lastSector = (offset + count) / volume->disk->bytesPerSector;
	uint64_t bytesInLastSector = (offset + count) - (lastSector * volume->disk->bytesPerSector);

	// handle special case
	if (startMissAligned && endMissAligned && (firstSector == lastSector)) {
		if ((status = volume_readwrite_frag(volume, read, firstSector, volume->disk->bytesPerSector - bytesInFirstSector, count, buffer)))
			return status;
		return STATUS_SUCCESS;
	}

	// transfer part of the first sector
	if (startMissAligned) {
		if ((status = volume_readwrite_frag(volume, read, firstSector, volume->disk->bytesPerSector - bytesInFirstSector, bytesInFirstSector, buffer)))
			return status;
		buffer += bytesInFirstSector;
	}

	// transfer full sectors
	if (fullSectors) {
		if (read)
			status = volume->disk->read(volume->disk, volume->startSector + firstSector + startMissAligned, fullSectors, buffer);
		else
			status = volume->disk->write(volume->disk, volume->startSector + firstSector + startMissAligned, fullSectors, buffer);
		if (status)
			return status;
		buffer += fullSectors * volume->disk->bytesPerSector;
	}

	// transfer part of the last sector
	if (endMissAligned) {
		if ((status = volume_readwrite_frag(volume, read, lastSector, 0, bytesInLastSector, buffer)))
			return status;
	}

	return STATUS_SUCCESS;
}


// Reads from the specified volume. Reading beyond the volume is not allowed.
status_t volume_read(volume_t *volume, uint64_t offset, uint64_t count, char *buffer) {
	return volume_readwrite(volume, 1, offset, count, buffer);
}


// Writes to the specified volume. Writing beyond the volume is not allowed.
status_t volume_write(volume_t *volume, uint64_t offset, uint64_t count, char *buffer) {
	return volume_readwrite(volume, 0, offset, count, buffer);
}


// Tries to initialize the filesystem on the specified volume.
status_t fs_init(volume_t *volume, file_t *root) {
	status_t status;

	root->data = NULL;
	root->size = 0;
	root->position = 0;
	root->isDir = 1;

	char vbr[512];
	if ((status = volume_read(volume, 0, 512, vbr)))
		return status;

	for (driver_t *driver = driver_getlist(DRIVER_TYPE_VOLUME); driver; driver = driver->next)
		if (!(status = ((fs_init_proc_t)driver->initProc)(volume, vbr, &(root->filesystem), &(root->reference))))
			return STATUS_SUCCESS;

	return STATUS_INCOMPATIBLE;
}


// Opens a file or directory to allow retrieving data and reading from the file.
// This must be called on any file or directory before anything else is done with it.
// A file or directory can be opened multiple times at once.
// If a file is opened, the read/write position is set to 0.
status_t file_open(file_t *file) {
	assert(file); assert(file->filesystem);
	status_t status = file->filesystem->open(file->filesystem->context, file->reference, &(file->data));
	file->position = 0;
	return status;
}


// Closes a file or directory.
status_t file_close(file_t *file) {
	assert(file); assert(file->filesystem); assert(file->data);
	status_t status = file->filesystem->close(file->filesystem->context, file->data);
	file->data = NULL;
	file->position = 0;
	return status;
}


// Returns the name of the specifide file or directory.
//	name: a pointer to a unicode string that will be filled with the name of the file (the buffer of the unicode string must be released by the caller)
status_t file_get_name(file_t *file, unicode_t *name) {
	assert(file); assert(file->filesystem); assert(file->data);
	return file->filesystem->getName(file->filesystem->context, file->data, name);
}


// Returns a direct child file or directory within the specified directory.
//	dir: must be a directory
//	name: the name of the file or directory that is requested
//	file: an uninitialized file structure
//	isDir: if non-zero, a directory is returned, else a file is returned
status_t file_get_child(file_t *dir, unicode_t *name, file_t *file, int isDir) {
	assert(dir); assert(dir->filesystem); assert(dir->data); assert(dir->isDir); assert(name); assert(file);
	status_t status = dir->filesystem->getChild(dir->filesystem->context, dir->data, name, &(file->reference), &(file->size), isDir);
	file->filesystem = dir->filesystem;
	file->data = NULL;
	file->position = 0;
	file->isDir = isDir;
	return status;
}


// Returns the file or directory at the specified path in the specified directory
//	dir: must be a directory
//	path: the path to the file or directory that is requested (delimiters: '/' or '\', empty path elements are ignored)
//	file: an uninitialized file structure
//	isDir: if non-zero, a directory is returned, else a file is returned
status_t file_navigate(file_t *dir, unicode_t *path, file_t *file, int isDir) {
	size_t pos = 0;
	size_t length = 0;
	file_t dir1 = *dir, dir2;
	file_t *currentDir = &dir1, *nextDir = &dir2;
	status_t status;

	// ignore trailing path delimiters
	size_t pathLength = path->length;
	if (pathLength)
		while ((path->data[pathLength - 1] == L'/') || (path->data[pathLength - 1] == L'\\'))
			pathLength--;

	while (pos < pathLength) {
		// bracket next path element
		length = 0;
		while ((pos < pathLength) ? ((path->data[pos] == L'/') || (path->data[pos] == L'\\')) : 0)
			pos++;
		while ((pos + length < pathLength) ? ((path->data[pos + length] != L'/') && (path->data[pos + length] != L'\\')) : 0)
			length++;

		// navigate to next path element
		if (length) {
			if ((length != 1) || (path->data[pos] != L'.')) {
				unicode_t pathElement = { .data = path->data + pos, .length = length };

				if ((status = file_open(currentDir)))
					return status;
				if ((status = file_get_child(currentDir, &pathElement, nextDir, ((pos + length == pathLength) ? isDir : 1))))
					return status;
				if ((status = file_close(currentDir)))
					return status;

				file_t *unusedDir = currentDir;
				currentDir = nextDir;
				nextDir = unusedDir;
			}
			pos += length;
		}
	}

	*file = *currentDir;
	return STATUS_SUCCESS;
}


// Reads the specified number of bytes from the file.
//	file: must be a file
//	count: the number of bytes to read
//	buffer: the buffer where the bytes should be loaded
status_t file_read(file_t *file, uint64_t count, char *buffer) {
	assert(file); assert(file->filesystem); assert(file->data); assert(!file->isDir);
	status_t status = file->filesystem->read(file->filesystem->context, file->data, file->position, count, buffer);
	if (!status)
		file->position += count;
	return status;
}

#endif
