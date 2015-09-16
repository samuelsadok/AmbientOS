/*
*
*
* created: 03.02.15
*
*/


#ifndef __UNICODE_H__
#define __UNICODE_H__


#ifdef USING_UNICODE


#ifndef _WCHAR_T_DEFINED_
#	error "wchar_t must be defined to use unicode"
#endif

// Use the gcc switch "-fshort-wchar" to force wchar_t to 16-bit width.
#if (__WCHAR_MAX__ != 0xFFFF) && (__WCHAR_MAX__ != 0x7FFF)
#	error "wchar_t must be 16-bit long to use unicode"
#endif

typedef struct
{
	size_t length; // string length (in characters)
	wchar_t *data; // 16-bit characters
} unicode_t;

typedef const struct
{
	const size_t length; // string length (in characters)
	const wchar_t *data;
} const_unicode_t;

/*
* to init a unicode structure use:
* unicode_t str = UNICODE("string")
*/
#define UNICODE(str)	{ .length = sizeof((str)) - 1, .data = CONCAT(L, str) }

void unicode_set_uppercase(wchar_t *mapping);
void unicode_set_lowercase(wchar_t *mapping);
int unicode_compare(unicode_t *str1, unicode_t *str2, int ignoreCase);


#endif // USING_UNICODE

#endif // __UNICODE_H__
