
/*
*
* 
* created: 18.01.15
*
*/


#include <system.h>
#include "bitmap.h"

#ifdef USING_GRAPHICS


// Allocates a blank bitmap with the specified dimensions. The bitmap must be freed using bitmap_free.
bitmap_t *bitmap_alloc(uint64_t width, uint64_t height) {
	bitmap_t *bitmap = (bitmap_t *)malloc(sizeof(bitmap_t));
	if (!bitmap)
		return NULL;

	bitmap->width = width;
	bitmap->height = height;
	bitmap->data = (color_t *)calloc(width * height, sizeof(uint32_t));
	if (!bitmap->data)
		return free(bitmap), NULL;

	return bitmap;
}

// Frees a bitmap that was allocated using bitmap_alloc
void bitmap_free(bitmap_t *bitmap) {
	free(bitmap->data);
	free(bitmap);
}

// Sets a pixel in the bitmap in case it is within the frame.
void bitmap_setpixel(bitmap_t *bitmap, int64_t x, int64_t y, color_t color) {
	if (x >= 0 && y >= 0 && x < bitmap->width && y < bitmap->height)
		bitmap->data[bitmap->width * y + x] = color;
}






//CPUACCELFUNC(int, test_func, (int arg1, int arg2), {
//	return arg1 + arg2;
//})


CPUACCELDECL(int, test_func, (int arg1, int arg2));
CPUACCELFUNC(int, test_func, (int arg1, int arg2), {
	return arg1 + arg2;
})


// Paints one bitmap onto another bitmap. (Currently not good for scaling down)
//	dest: destination image
//	src: source image
//	destRect: rectangle in the destination image to paint to (paints over the whole image if NULL)
//	srcRect: rectangle in the source image to read from (uses the whole image if NULL)
void bitmap_paint(bitmap_t *dest, bitmap_t *src, rect_t *destRect, rect_t *srcRect) {
	int b = test_func(23, 45);
	assert(dest); assert(src);
	rect_t destFrame = (rect_t) { .x = 0, .y = 0, .width = (double)dest->width, .height = (double)dest->height };
	rect_t srcFrame = (rect_t) { .x = 0, .y = 0, .width = (double)src->width, .height = (double)src->height };
	if (!destRect) destRect = &destFrame;
	if (!srcRect) srcRect = &srcFrame;
	assert(destRect->width > 0); assert(destRect->height > 0);
	assert(srcRect->width > 0); assert(srcRect->height > 0);

	double ratioX = srcRect->width / destRect->width;
	double ratioY = srcRect->height / destRect->height;

	// paint pixel by pixel in the destination rectangle
	for (int64_t y = max(0, -destRect->y); y < min(destRect->height, dest->height - destRect->y); y++) {
		for (int64_t x = max(0, -destRect->x); x < min(destRect->width, dest->width - destRect->x); x++) {
			// find the theoretical coordinates in the source image
			double srcX = (double)x * ratioX;
			double srcY = (double)y * ratioY;
			double pX = srcX - (double)(int64_t)srcX;
			double pY = srcY - (double)(int64_t)srcY;
			double p00 = (1 - pX) * (1 - pY);
			double p01 = (1 - pX) * pY;
			double p10 = pX * (1 - pY);
			double p11 = pX * pY;

			// find the 4 colors that surround the theoretical point in the source image
			int64_t srcXint = (int64_t)(srcX + srcRect->x);
			int64_t srcYint = (int64_t)(srcY + srcRect->y);
			int withinX0 = (srcXint >= 0 && srcXint < src->width);
			int withinX1 = ((srcXint + 1) >= 0 && (srcXint + 1) < src->width);
			int withinY0 = (srcYint >= 0 && srcYint < src->height);
			int withinY1 = ((srcYint + 1) >= 0 && (srcYint + 1) < src->height);
			color_t color00 = (withinX0 && withinY0) ? src->data[srcYint * src->width + srcXint] : COLOR_BLACK;
			color_t color01 = (withinX0 && withinY1) ? src->data[(srcYint + 1) * src->width + srcXint] : COLOR_BLACK;
			color_t color10 = (withinX1 && withinY0) ? src->data[srcYint * src->width + srcXint + 1] : COLOR_BLACK;
			color_t color11 = (withinX1 && withinY1) ? src->data[(srcYint + 1) * src->width + srcXint + 1] : COLOR_BLACK;

			// mix the 4 surrounding colors
			color_t color;
			color.red = (double)color00.red * p00 + (double)color01.red * p01 + (double)color10.red * p10 + (double)color11.red * p11;
			color.green = (double)color00.green * p00 + (double)color01.green * p01 + (double)color10.green * p10 + (double)color11.green * p11;
			color.blue = (double)color00.blue * p00 + (double)color01.blue * p01 + (double)color10.blue * p10 + (double)color11.blue * p11;
			dest->data[((y + (size_t)destRect->y) * dest->width + x + (size_t)destRect->x)] = color;
		}
	}
}



#ifdef USING_FILESYSTEM


typedef struct __attribute__((__packed__)) {
	uint16_t magicNumber;
	uint32_t fileSize;
	uint32_t reserved;
	uint32_t dataOffset;

	// DIB header
	uint32_t dibHeaderSize;
	int32_t width, height;
	uint16_t colorPlanes;
	uint16_t bitsPerPixel;
	uint32_t compressionMethod;
	uint32_t dataSize;
	uint32_t hRes, vRes;
	uint32_t colorsInPalette;
	uint32_t importantColorCount;
} bmp_header_t;




// Loads a *.bmp file from disk. The bitmap must later be freed using bitmap_free.
status_t bitmap_load(file_t *file, bitmap_t **bitmapPtr) {
	status_t status;
	*bitmapPtr = NULL;

	char *buffer = (char *)malloc(file->size);
	if (!buffer)
		return STATUS_OUT_OF_MEMORY;

	// load from disk
	if ((status = file_open(file)))
		return free(buffer), status;
	status = file_read(file, file->size, buffer);
	file_close(file);
	if (status)
		return free(buffer), status;

	// parse header
	bmp_header_t *header = (bmp_header_t *)buffer;
	if (header->magicNumber != *(uint16_t *)"BM")
		return free(buffer), STATUS_DATA_CORRUPT;
	if (header->fileSize != file->size)
		return free(buffer), STATUS_DATA_CORRUPT;
	if ((header->dibHeaderSize != 40) && (header->dibHeaderSize != 52) && (header->dibHeaderSize != 56) && (header->dibHeaderSize != 108) && (header->dibHeaderSize != 124))
		return free(buffer), STATUS_NOT_IMPLEMENTED;
	if (header->colorPlanes != 1)
		return free(buffer), STATUS_NOT_IMPLEMENTED;
	if (header->colorsInPalette && (header->colorsInPalette != (1 << header->bitsPerPixel)))
		return free(buffer), STATUS_NOT_IMPLEMENTED;

	switch (header->bitsPerPixel) {
		case 24:
			break;
		default:
			return free(buffer), STATUS_NOT_IMPLEMENTED;
	}


	bitmap_t *bitmap = bitmap_alloc(abs(header->width), abs(header->height));
	if (!bitmap)
		return free(buffer), STATUS_OUT_OF_MEMORY;

	// copy to internal bitmap format
	char *ptr = buffer + header->dataOffset;
	size_t padding = 4 - ((header->width * 3) & 3);
	if (padding == 4) padding = 0;
	for (int64_t y = ((header->height > 0) ? (header->height - 1) : (0)); ((header->height > 0) ? (y >= 0) : (y < -header->height)); ((header->height > 0) ? (y--) : (y++))) {
		for (int64_t x = ((header->width > 0) ? (0) : (-header->width - 1)); ((header->width > 0) ? (x < header->width) : (x >= 0)); ((header->width > 0) ? (x++) : (x--))) {
			color_t color;
			color.blue = *(ptr++);
			color.green = *(ptr++);
			color.red = *(ptr++);
			//color.blue = color.green = color.red = 0x7F;
			bitmap->data[y * bitmap->width + x] = color;
		}

		//while (((uintptr_t)ptr - header->dataOffset) & 3) // lines are padded to multiples of 4
		ptr += padding;
	}

	free(buffer);
	*bitmapPtr = bitmap;
	return STATUS_SUCCESS;
}

#endif // USING_FILESYSTEM


#endif // USING_GRAPHICS
