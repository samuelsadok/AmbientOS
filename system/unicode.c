/*
*
*
* created: 03.02.15
*
*/

#include <system.h>
#include "unicode.h"


wchar_t *upperCaseMapping = NULL;
wchar_t *lowerCaseMapping = NULL;


// Sets the uppercase mapping.
// A mapping is a 128kB array that contains a mapped value for each char.
// If a mapping is already set up, the old mapping is freed.
void unicode_set_uppercase(wchar_t *mapping) {
	if (upperCaseMapping)
		free(upperCaseMapping);
	upperCaseMapping = mapping;
}


// Sets the uppercase mapping.
// If a mapping is already set up, the old mapping is freed.
void unicode_set_lowercase(wchar_t *mapping) {
	if (lowerCaseMapping)
		free(lowerCaseMapping);
	lowerCaseMapping = mapping;
}


// Compares two unicode strings.
//	ignoreCase: if 1 and a case mapping is set up, case is ignored
// Return value:
//	<0: str1 is lexicographically before str2
//	0:  str1 and str2 are equal
//	>0: str1 is lexicographically after str2
int unicode_compare(unicode_t *str1, unicode_t *str2, int ignoreCase) {
	wchar_t *mapping = NULL;
	if (ignoreCase) {
		if (upperCaseMapping)
			mapping = upperCaseMapping;
		else
			mapping = lowerCaseMapping;
	}

	for (size_t i = 0; i < min(str1->length, str2->length); i++) {
		wchar_t chr1 = (mapping ? mapping[(size_t)(str1->data[i])] : str1->data[i]);
		wchar_t chr2 = (mapping ? mapping[(size_t)(str2->data[i])] : str2->data[i]);
		if (chr1 != chr2)
			return (int)chr1 - (int)chr2;
	}

	if (str1->length != str2->length)
		return (int)str1->length - (int)str2->length;

	return 0;
}
