

#ifndef __STDLIB_H__
#define __STDLIB_H__




// Returns the greater of two numbers
#define		max(a, b)	((a) > (b) ? (a) : (b))

// Returns the less of two numbers
#define		min(a, b)	((a) < (b) ? (a) : (b))

// Returns the absolute value of any number
#define		abs(a)		((a) < 0 ? -(a) : (a))



void *malloc(size_t size);
void *realloc(void *block, size_t size);
void free(void *block);

// Allocates the specified number of elements and clears the memory.
static inline void* calloc(size_t num, size_t size) {
	char *ptr = (char *)malloc(size *= num);
	if (!ptr) return NULL;
	while (size--)
		ptr[size] = 0;
	return ptr;
}


// Copies memory from source to destination
void *memcpy(void *restrict __dest, const void *restrict __src, size_t __n);

// Returns 0 if two blocks of memory are equal
int memcmp(const void *ptr1, const void *ptr2, size_t num);

// Sets a block of memory to the specified value
void *memset(void *ptr, int value, size_t num);


#endif // __STDLIB_H__
