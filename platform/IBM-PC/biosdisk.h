
#ifndef __BIOSDISK_H__
#define __BIOSDISK_H__


#include <system/filesystem.h>


disk_t *biosdisk_init(uint64_t preferredDisk, size_t *count);


#endif // __BIOSDISK_H__
