/*
*
* This file should be recompiled everytime anything is recompiled.
*
* created: 05.03.15
*
*/

#ifdef USING_DFU

#include <system.h>

const_unicode_t platformStr = UNICODE(PLATFORM_NAME); // platform identification string, passed as a compiler argument
const_unicode_t appNameStr = UNICODE(APPLICATION_NAME); // application name string, passed as a compiler argument
const_unicode_t versionStr = UNICODE(BUILD_TIME); // build date and time (UTC), passed as a compiler argument, used to identify the binary version

#endif
