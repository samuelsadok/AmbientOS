
/*
*
* Provides methods to use graphic capabilities provided by the BIOS (VGA and VESA)
*
* created: 18.01.15
*
*/

#include <system.h>
#include "graphics.h"


typedef struct __attribute__((packed)) {
	uint8_t VESASignature[4];
	uint16_t VESAVersion;
	uint32_t OEMStringPtr;
	uint8_t Capabilities[4];
	uint32_t VideoModePtr;
	uint16_t TotalMemory;
	uint16_t OemSoftwareRev;
	uint32_t OemVendorNamePtr;
	uint32_t OemProductNamePtr;
	uint32_t OemProductRevPtr;
	uint8_t Reserved[222];
	uint8_t OemData[256];
} vesa_info_t;


typedef struct {
	uint16_t ModeAttributes;
	uint8_t WinAAttributes;
	uint8_t WinBAttributes;
	uint16_t WinGranularity;
	uint16_t WinSize;
	uint16_t WinASegment;
	uint16_t WinBSegment;
	uint16_t WinFuncPtr;
	uint16_t WinFuncSegment;
	uint16_t BytesPerScanLine;
	uint16_t XResolution;
	uint16_t YResolution;
	uint8_t XCharSize;
	uint8_t YCharSize;
	uint8_t NumberOfPlanes;
	uint8_t BitsPerPixel;
	uint8_t NumberOfBanks;
	uint8_t MemoryModel;
	uint8_t BankSize;
	uint8_t NumberOfImagePages;
	uint8_t Reserved_page;
	uint8_t RedMaskSize;
	uint8_t RedMaskPos;
	uint8_t GreenMaskSize;
	uint8_t GreenMaskPos;
	uint8_t BlueMaskSize;
	uint8_t BlueMaskPos;
	uint8_t ReservedMaskSize;
	uint8_t ReservedMaskPos;
	uint8_t DirectColorModeInfo;
	uint32_t PhysBasePtr;
	uint32_t OffScreenMemOffset;
	uint16_t OffScreenMemSize;
	uint8_t Reserved[206];
} vesa_mode_info_t;



vesa_mode_info_t currentMode;


// Loads the vesa information block into the real mode buffer.
status_t vesa_load_info() {
	for (int i = 0; i < sizeof(vesa_info_t); i++)
		realmodeBuffer[i] = 0;

	memcpy(realmodeBuffer, "VBE2", 4);

	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x4F00;
	realmode_buffer_ref(&regs.es, (uint16_t *)&regs.edi);
	realmode_int(0x10, &regs);

	if (regs.eax & 0xFF00)
		return STATUS_ERROR;

	return STATUS_SUCCESS;
}


// Loads the mode info block for the specified mode into the real mode buffer
status_t vesa_load_mode_info(int mode) {
	for (int i = 0; i < sizeof(vesa_mode_info_t); i++)
		realmodeBuffer[i] = 0;

	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x4F01;
	regs.ecx = mode;
	realmode_buffer_ref(&regs.es, (uint16_t *)&regs.edi);
	realmode_int(0x10, &regs);

	if (regs.eax & 0xFF00)
		return STATUS_ERROR;

	return STATUS_SUCCESS;
}


// Selects the specified mode and loads the associated info block.
status_t vesa_select_mode(int mode) {
	// load and copy info for this mode
	status_t status = vesa_load_mode_info(mode);
	if (status) return status;

	for (int i = 0; i < sizeof(vesa_mode_info_t); i++)
		((char *)&currentMode)[i] = realmodeBuffer[i];

	// select new mode
	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x4F02;
	regs.ebx = mode;
	realmode_int(0x10, &regs);
	
	if (regs.eax & 0xFF00)
		return STATUS_ERROR;

	return STATUS_SUCCESS;
}


// Selects the specified memory window
static inline void vesa_set_window(int window) {
	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x4F05;
	regs.ebx = 0; // window A
	regs.edx = window;
	// debug(0x67, (uint64_t)&currentMode.WinFuncPtr);
	if (currentMode.WinFuncPtr || currentMode.WinFuncSegment)
		realmode_call(currentMode.WinFuncSegment, currentMode.WinFuncPtr, &regs); // direct function calls are faster
	else
		realmode_int(0x10, &regs);
}

//// Selects the specified memory window
//static inline void vesa_set_window(int window) {
//	out(0x01cf, window);
//}


#define vesa_advance_addr(addr, currentWindow, bytesLeftInWindow, advance) do {				\
	size_t adv = (advance);																	\
	while (adv >= *(bytesLeftInWindow))														\
		vesa_advance_addr_ex(&(addr), (currentWindow), (bytesLeftInWindow), &adv);			\
	*(bytesLeftInWindow) -= adv;															\
	(addr) += adv;																			\
} while (0)


// Advances the frame buffer pointer and switches the bank if necessary
static inline void vesa_advance_addr_ex(char **addr, int *currentWindow, size_t *bytesLeftInWindow, size_t *advance) {
	size_t winSize = currentMode.WinSize << 10;
	size_t winGranularity = currentMode.WinGranularity << 10;

	*advance -= *bytesLeftInWindow;
	*addr -= winSize - *bytesLeftInWindow;
	*bytesLeftInWindow = winSize;

	vesa_set_window(*currentWindow += winSize / winGranularity);
}


// Selects the highest resolution and color depth.
//	Returns a non-zero error code if VESA is not available or the setup failed.
//	In case of success, the width and height fields are set to the resolution that was enabled.
status_t vesa_init()
{
	status_t status;

	if ((status = vesa_load_info())) return status;
	vesa_info_t *info = (vesa_info_t *)realmodeBuffer;
	if (memcmp(info->VESASignature, "VESA", 4)) return STATUS_ERROR;
	
	// parse list of available modes
	int modeCount;
	uint16_t modeList[111]; // the list is stored in the reserved region of the info block, hence there's an upper bound to its size
	uint16_t *modeListSource = (uint16_t *)(KERNEL_OFFSET + ((info->VideoModePtr & 0xFFFF0000) >> 12) + (info->VideoModePtr & 0xFFFF));
	for (modeCount = 0; modeListSource[modeCount] != 0xFFFF; modeCount++)
		modeList[modeCount] = modeListSource[modeCount];

	// find best mode
	uint16_t bestMode = 0xFFFF;
	uint64_t bestResolution = 0;
	uint64_t bestColorDepth = 0;
	while (modeCount--) {
		if ((status = vesa_load_mode_info(modeList[modeCount]))) continue;
		vesa_mode_info_t *modeInfo = (vesa_mode_info_t *)realmodeBuffer;
		
		// accept only graphic color modes and reject disabled modes
		if ((modeInfo->ModeAttributes & 0x19) != 0x19) continue;

		// only support 1 color plane
		if (modeInfo->NumberOfPlanes != 1) continue;

		// only support byte aligned pixels and max. 64bit pixels
		if (modeInfo->BitsPerPixel & 7) continue;
		if (!modeInfo->BitsPerPixel) continue;
		if (modeInfo->BitsPerPixel > 64) continue;

		// only support direct color mode
		if (modeInfo->MemoryModel != 6) continue;

		uint64_t pixels = (uint64_t)modeInfo->XResolution * (uint64_t)modeInfo->YResolution;
		if (pixels > bestResolution || (pixels == bestResolution && modeInfo->BitsPerPixel > bestColorDepth)) {
			bestMode = modeCount;
			bestResolution = pixels;
			bestColorDepth = modeInfo->BitsPerPixel;
		}
	}

	if (bestMode == 0xFFFF)
		return STATUS_ERROR;

	if ((status = vesa_select_mode(modeList[bestMode])))
		return status;

	return STATUS_SUCCESS;
}


// Enables the 320x200x256 VGA graphic mode
void vga_init() {
	// enter graphics mode
	realmode_context_t regs;
	realmode_reset(&regs);
	regs.eax = 0x0013; // 320x200, 256 colors
	realmode_int(0x10, &regs);

	// set up palette
	out(0x03C6, 0xff);	// mask all registers so we can update any of them
	out(0x03C8, 0);		// start at index 0
	for (int i = 0; i < 256; i++) { // only 6 bits are used for each color
		out(0x03C9, (i & 0xE0) >> 2); // bits 7:5: red
		out(0x03C9, (i & 0x1C) << 1); // bits 4:2: green
		out(0x03C9, (i & 0x03) << 4); // bits 1:0: blue
	}

	// set up variables for drawing
	currentMode.BankSize = 0; // no banks
	currentMode.NumberOfBanks = 1;
	currentMode.WinAAttributes = 0x06; // writable, readable, not reloacatable
	currentMode.WinASegment = 0xA000;
	currentMode.WinFuncPtr = 0;
	currentMode.WinFuncSegment = 0;
	currentMode.XResolution = 320;
	currentMode.YResolution = 200;
	currentMode.BitsPerPixel = 8;
	currentMode.BytesPerScanLine = 320;
	currentMode.RedMaskSize = 3;
	currentMode.GreenMaskSize = 3;
	currentMode.BlueMaskSize = 2;
	currentMode.RedMaskPos = 5;
	currentMode.GreenMaskPos = 2;
	currentMode.BlueMaskPos = 0;
}


// Sets graphics to the best available resolution and returns a bitmap of the respective size.
// This bitmap can be used with graphics_draw to draw to the screen.
// Returns NULL if not enough resources were available.
bitmap_t *graphics_init() {
	atomic()
		if (vesa_init())
			vga_init();
	return bitmap_alloc(currentMode.XResolution, currentMode.YResolution);
}


// Draws a bitmap to the screen.
// The bitmap must have the same dimensions as the screen resolution.
status_t graphics_draw(bitmap_t *bitmap) {
	if (bitmap->width != currentMode.XResolution || bitmap->height != currentMode.YResolution)
		return STATUS_ERROR;

	// select address of a writable window (A or B)
	int windowSegment = 0, relocateWindow = 0;
	if (currentMode.WinAAttributes & 0x04) {
		windowSegment = currentMode.WinASegment;
		relocateWindow = currentMode.WinAAttributes & 1;
	} else {
		windowSegment = currentMode.WinBSegment;
		relocateWindow = currentMode.WinBAttributes & 1;
	}
	char *writeAddr = (char *)(KERNEL_OFFSET + (windowSegment << 4));
	color_t *readAddr = bitmap->data;

	//out(0x01ce, 5); // todo: remove
	int currentWindow = 0;
	size_t bytesLeftInWindow = currentMode.WinSize << 10;
	if (relocateWindow)
		vesa_set_window(currentWindow);
	else
		bytesLeftInWindow = -1;

	// precalculate some metrics
	uint64_t pixelMask = 0xFFFFFFFFFFFFFFFFUL << currentMode.BitsPerPixel;
	size_t bytesPerPixel = currentMode.BitsPerPixel >> 3;
	register uint64_t redShiftR = 8 - currentMode.RedMaskSize, greenShiftR = 8 - currentMode.GreenMaskSize, blueShiftR = 8 - currentMode.BlueMaskSize;
	register uint64_t redShiftL = currentMode.RedMaskPos, greenShiftL = currentMode.GreenMaskPos, blueShiftL = currentMode.BlueMaskPos;

	int linesPerBank = bitmap->height / currentMode.NumberOfBanks;
	int linesLeftInBank = linesPerBank;
	int bankPadding = (currentMode.BankSize << 10) - (bitmap->width * bytesPerPixel);
	if (!currentMode.BankSize) bankPadding = 0;
	int excessBytesPerLine = currentMode.BytesPerScanLine - bitmap->width * bytesPerPixel;

	for (uint64_t y = 0; y < bitmap->height; y++) {
		for (uint64_t x = 0; x < bitmap->width; x++) {
			register color_t color = *readAddr;
			register uint64_t pixel = *(uint64_t *)writeAddr & pixelMask;
			pixel |= (color.red >> redShiftR) << redShiftL;
			pixel |= (color.green >> greenShiftR) << greenShiftL;
			pixel |= (color.blue >> blueShiftR) << blueShiftL;
			*(uint64_t *)writeAddr = pixel; // potential issue: writes beyond window

			readAddr++;
			vesa_advance_addr(writeAddr, &currentWindow, &bytesLeftInWindow, bytesPerPixel);
		}


		// scan lines are not necessarily contiguous in the frame buffer
		size_t padding = excessBytesPerLine;
		
		if (!--linesLeftInBank) { // skip rest of bank if necessary
			linesLeftInBank = linesPerBank;
			padding += bankPadding;
		}

		vesa_advance_addr(writeAddr, &currentWindow, &bytesLeftInWindow, padding);
	}

	return STATUS_SUCCESS;
}
