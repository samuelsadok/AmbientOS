/*
*
*
* created: 13.02.15
*
*/

#ifndef __COMMON_NVM_H__
#define __COMMON_NVM_H__


extern int nvmValid;

void nvm_read(size_t offset, char* buffer, size_t length);
void nvm_write(size_t offset, const char* buffer, size_t length);
void nvm_init(void);
status_t nvm_data_init(void);


#endif // __COMMON_NVM_H__
