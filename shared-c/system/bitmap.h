
#ifndef __BITMAP_H__
#define __BITMAP_H__

#ifdef USING_GRAPHICS

#include <system/filesystem.h>

typedef struct
{
	unsigned int red : 8;
	unsigned int green : 8;
	unsigned int blue : 8;
	unsigned int alpha : 8;
} color_t;

#define COLOR_WHITE	((color_t) { .red = 255, .green = 255, .blue = 255, .alpha = 255 })
#define COLOR_BLACK	((color_t) { .red = 0, .green = 0, .blue = 0, .alpha = 255 })
#define COLOR_RED	((color_t) { .red = 255, .green = 0, .blue = 0, .alpha = 255 })
#define COLOR_GREEN	((color_t) { .red = 0, .green = 255, .blue = 0, .alpha = 255 })
#define COLOR_BLUE	((color_t) { .red = 0, .green = 0, .blue = 255, .alpha = 255 })

typedef struct
{
	double x, y;
	double width, height;
} rect_t;

typedef struct
{
	uint64_t width, height;
	color_t *data;
} bitmap_t;


bitmap_t *bitmap_alloc(uint64_t width, uint64_t height);
void bitmap_free(bitmap_t *bitmap);
void bitmap_setpixel(bitmap_t *bitmap, int64_t x, int64_t y, color_t color);
void bitmap_paint(bitmap_t *dest, bitmap_t *src, rect_t *destRect, rect_t *srcRect);

#ifdef USING_FILESYSTEM
status_t bitmap_load(file_t *file, bitmap_t **bitmapPtr);
#endif

#endif // USING_GRAPHICS

#endif // __BITMAP_H__
