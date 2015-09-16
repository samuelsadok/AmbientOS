/*
*
*
* created: 05.03.15
*
*/

#ifndef __BUILD_H__
#define __BUILD_H__

#ifdef USING_DFU

// unicode.h must be included before this file

#ifndef USING_UNICODE
#	error "unicode must be enabled to use DFU strings"
#endif

extern const_unicode_t platformStr;
extern const_unicode_t appNameStr;
extern const_unicode_t versionStr;

#endif

#endif // __BUILD_H__
