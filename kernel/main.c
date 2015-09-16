

#include <system.h>

//#include <system/bitmap.h>
//#include <system/heap.h>
//#include <system/memory.h>
//#include <system/threading.h>
//#include <system/unicode.h>
//#include <system/filesystem.h>
//#include <system/ntfs.h>
//#include <system/debug.h>
//#include <system/asciiart.h>








char tStack[100];
void hello_thread(void *param) {
	while (1) {
		teletype_print_string("hello\n", 6, 0x07);
		thread_sleep(20);
	}
}







status_t init_bootvid(uint64_t bootdisk, uint64_t volumeStart, file_t *rootDir) {
	status_t status;

	LOGI("init disks, (bootdisk is %x8)...", (uint8_t)bootdisk);

	size_t diskCount;
	disk_t *disks = biosdisk_init(bootdisk, &diskCount);

	status = STATUS_END_OF_STREAM;
	for (int i = 0; i < diskCount; i++) {
		if (disks[i].reference == bootdisk) {
			diskCount = i;
			status = STATUS_SUCCESS;
		}
	}

	if (status) {
		LOGE("boot disk not found");
		return status;
	}

	LOGI("init volumes...");

	size_t volumeCount;
	volume_t *volumes = volume_init(&disks[diskCount], &volumeCount);
	status = STATUS_END_OF_STREAM;
	for (int i = 0; i < volumeCount; i++) {
		if (volumes[i].startSector == volumeStart) {
			volumeCount = i;
			status = STATUS_SUCCESS;
		}
	}

	if (status) {
		LOGE("boot volume not found");
		return status;
	}

	LOGI("init filesystem...");

	if ((status = fs_init(&volumes[volumeCount], rootDir))) {
		LOGE("filesystem corrupt or not supported (%d)", status);
		return status;
	}

	return STATUS_SUCCESS;
}


status_t load_bootimg(int num, file_t *rootDir, bitmap_t **bmpPtr) {
	unicode_t path = (num == 1 ? UNICODE("bootimg1.bmp") : UNICODE("bootimg2.bmp"));
	file_t file;
	status_t status;
	LOGI("  navigate to file...");
	if ((status = file_navigate(rootDir, &path, &file, 0))) {
		LOGE("bootimg file not found (%d)", status);
		return status;
	}
	LOGI("  load file...");
	if ((status = bitmap_load(&file, bmpPtr))) {
		LOGE("bootimg could not be loaded (%d)", status);
		return status;
	}
	return STATUS_SUCCESS;
}






// rdi:	physical address of the bootloader code
// rsi:	linear address of an array of memory map entries obtained by invoking int 0x15
// rdx: linear address of the TSS descriptor in the GDT
// rcx: number of bios bootdisk
// r8: start sector of boot volume
// r9: unused
void __attribute__((noreturn)) main() {

	debug_report_features();

	LOGI("initializing threading...");

	// init stacks, interrupts, the local APIC, and threading
	threading_init();
	interrupts_on();


	//// todo: put in test function
	//mmu_dump(3);
	//phy_dump();
	//debug(0x59, 0);
	//void *page = page_alloc(PAGE_SIZE * 220);
	//mmu_dump(3);
	//phy_dump();
	//debug(0x59, (uint64_t)page);
	//page_free(page, PAGE_SIZE * 220);
	//mmu_dump(3);
	//phy_dump();
	//debug(0x59, 0);



	//teletype_print_string(asciiart, sizeof(asciiart), 0x07);



	ntfs_register();

	LOGI("loading root directory...");

	file_t rootDir;
	bitmap_t *bmp1, *bmp2;
	if (init_bootvid(*bootdisk, *volumeStart, &rootDir))
		for (;;);

	LOGI("loading bitmap...");

	if (load_bootimg(1, &rootDir, &bmp1))
		for (;;);

	LOGI("switching graphics...");

	bitmap_t *screen = graphics_init();
	bitmap_paint(screen, bmp1, NULL, NULL);
	//debug(0x63, (uint64_t)screen->data);



	/*
	for (int xy = 0; xy < 10; xy++) {
		bitmap_setpixel(screen, xy, xy, (color_t) { .red = 255, .green = 255, .blue = 255 });
		bitmap_setpixel(screen, xy, 199 - xy, (color_t) { .red = 255, .green = 255, .blue = 255 });
		bitmap_setpixel(screen, 319 - xy, xy, (color_t) { .red = 255, .green = 255, .blue = 255 });
		bitmap_setpixel(screen, 319 - xy, 199 - xy, (color_t) { .red = 255, .green = 255, .blue = 255 });
	}

	for (int y = 10; y < 190; y++)
		for (int x = 10; x < 60; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 255, .green = 0, .blue = 0 });
	for (int y = 10; y < 190; y++)
		for (int x = 60; x < 110; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 0, .green = 255, .blue = 0 });
	for (int y = 10; y < 190; y++)
		for (int x = 110; x < 160; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 0, .green = 0, .blue = 255 });
	for (int y = 10; y < 190; y++)
		for (int x = 160; x < 210; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 255, .green = 255, .blue = 0 });
	for (int y = 10; y < 190; y++)
		for (int x = 210; x < 260; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 0, .green = 255, .blue = 255 });
	for (int y = 10; y < 190; y++)
		for (int x = 260; x < 310; x++)
			bitmap_setpixel(screen, x, y, (color_t) { .red = 255, .green = 0, .blue = 255 });

	for (int x = 0; x < screen->width; x++) {
		bitmap_setpixel(screen, x, 0, (color_t) { .red = x >> 2, .green = x >> 4, .blue = x });
		bitmap_setpixel(screen, x, screen->height - 1, (color_t) { .red = x >> 2, .green = x >> 4, .blue = x });
	}

	for (int y = 0; y < screen->height; y++) {
		bitmap_setpixel(screen, 0, y, (color_t) { .red = y, .green = y >> 4, .blue = y >> 2 });
		bitmap_setpixel(screen, screen->width - 1, y, (color_t) { .red = y, .green = y >> 4, .blue = y >> 2 });
	}*/

	//for (int pos = 0; pos < screen->height * screen->width; pos++)
	//		*(uint32_t *)&(screen->data[pos]) = 0xFFFFFFFF;

	

	//debug(0x62, 0);
	graphics_draw(screen);

	if (!load_bootimg(2, &rootDir, &bmp2)) {
		bitmap_paint(screen, bmp2, NULL, NULL);
		graphics_draw(screen);
	}


	//char *ch = (char *)(0xFFFFFF00000A0000);
	//uint8_t i = 1;
	//while (i++)
	//	*(ch++) = i;


	debug(0x63, 0);
//	pic_enable();

	thread_t t;
	thread_init(&t, hello_thread, NULL, (uintptr_t)tStack);
	thread_resume(&t);

	for (;;) {
		//thread_suspend();

		//bios_call(0x00, NULL);
		//realmode_context_t c;
		//c.eax = 0x0100;
		//uint32_t flags = bios_call(0x16, &c);
		//if (!(flags & (1 << 6))) {
		//	debug(0x15, 0);
		//	c.eax = 0;
		//	bios_call(0x16, &c);
		//	char ch = c.eax & 0xFF;
		//	//teletype_print_string(&ch, 1, 0x07);
		//}
	}
}

