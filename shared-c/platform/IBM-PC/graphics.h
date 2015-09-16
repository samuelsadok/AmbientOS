
#ifndef __GRAPHICS_H__
#define __GRAPHICS_H__

#include <system/bitmap.h>

bitmap_t *graphics_init();
status_t graphics_draw(bitmap_t *bitmap);

#endif // __GRAPHICS_H__
