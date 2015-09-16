/*
*
*
* created: 02.02.15
*
*/

#ifndef __FILESYSTEM_H__
#define __FILESYSTEM_H__


#ifdef USING_FILESYSTEM

#ifndef USING_UNICODE
#	error "the filesystem depends on unicode"
#endif

#include <system/unicode.h>


typedef struct disk_t
{
	uint64_t sectorCount;
	uint64_t bytesPerSector;
	
	int hasCHS;
	uint16_t cylinders;
	uint16_t heads;
	uint16_t sectorsPerTrack;

	uint64_t reference;			// disk reference specific to the underlying driver
	status_t(*read)(struct disk_t *disk, uint64_t startSector, uint64_t sectorCount, char *buffer);
	status_t(*write)(struct disk_t *disk, uint64_t startSector, uint64_t sectorCount, char *buffer);
} disk_t;


typedef struct volume_t
{
	disk_t *disk;
	uint64_t startSector;
	uint64_t sectorCount;
} volume_t;


volume_t* volume_init(disk_t *disk, size_t *count);
status_t volume_read(volume_t *volume, uint64_t offset, uint64_t count, char *buffer);
status_t volume_write(volume_t *volume, uint64_t offset, uint64_t count, char *buffer);


typedef status_t(*file_open_proc_t)(void *fsContext, uint64_t handle, void **filePtr);
// for the following operations the file or directory must be opened first
typedef status_t(*file_close_proc_t)(void *fsContext, void *file);
typedef status_t(*file_get_name_proc_t)(void *fsContext, void *file, unicode_t *name);
typedef status_t(*file_get_child_proc_t)(void *fsContext, void *dir, unicode_t *name, uint64_t *childReference, uint64_t *size, int isDir);
typedef status_t(*file_read_proc_t)(void *fsContext, void *file, uint64_t offset, uint64_t count, char *buffer);


typedef struct fs_t
{
	file_open_proc_t open;
	file_close_proc_t close;
	file_get_name_proc_t getName;
	file_get_child_proc_t getChild;
	file_read_proc_t read;

	//size_t contextLength;	// the context is located immediately after this struct
	char context[1];		// filesystem specific context
} fs_t;

typedef status_t(*fs_init_proc_t)(volume_t *volume, char *vbr, fs_t **fsPtr, uint64_t *rootReference);


typedef struct
{
	fs_t *filesystem;
	uint64_t reference;	// file system specific file identifier
	void *data;			// file system specific data (only valid while the file is open)
	int isDir;			// 1 if this is a directory
	uint64_t position;	// current read position (only valid for files)
	uint64_t size;		// file size (only valid for files)
} file_t;




status_t fs_init(volume_t *volume, file_t *root);
status_t file_open(file_t *file);
status_t file_close(file_t *file);
status_t file_get_name(file_t *file, unicode_t *name);
status_t file_get_child(file_t *dir, unicode_t *name, file_t *file, int isDir);
status_t file_navigate(file_t *dir, unicode_t *path, file_t *file, int isDir);
status_t file_read(file_t *file, uint64_t count, char *buffer);

#endif // USING_FILESYSTEM

#endif // __FILESYSTEM_H__
