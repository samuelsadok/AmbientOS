
#ifndef __REALMODE_H__
#define __REALMODE_H__



#define KERNEL_OFFSET	(0xFFFFFFFF80000000UL)


typedef struct __attribute__((packed))
{
	uint16_t ds, es, fs, gs;
	uint32_t edi, esi, ebp, reserved, ebx, edx, ecx, eax;
} realmode_context_t; // size: 28 (hard coded in bootsector.S)


//void realmode_init(void *bootloaderBase);
uint32_t realmode_execute(uint64_t code, realmode_context_t *context);

// buffer to transfer data to and from real mode
extern char *realmodeBuffer; // length: ~500kB
extern char *realmodeBuffer2; // length: 16 bytes


static inline void realmode_buffer_ref_ex(uintptr_t address, uint16_t *segmentRegister, uint16_t *offsetRegister) {
	*offsetRegister = address & 0xF;
	*segmentRegister = (address >> 4) & 0xFFFF;
}

// applies the realmode buffer address to a pair of segment and offset registers
#define realmode_buffer_ref(segmentRegister, offsetRegister)	realmode_buffer_ref_ex((uintptr_t)realmodeBuffer, (segmentRegister), (offsetRegister))
#define realmode_buffer2_ref(segmentRegister, offsetRegister)	realmode_buffer_ref_ex((uintptr_t)realmodeBuffer2, (segmentRegister), (offsetRegister))

// invokes a BIOS interrupt
#define realmode_int(interrupt, context)			realmode_execute(((uint64_t)(uint8_t)(interrupt) << 48) | 0xFA00CDFB90909090UL, context) // code: sti; int interrupt; cli;
#define realmode_int_debug(interrupt, context)		realmode_execute(((uint64_t)(uint8_t)(interrupt) << 48) | 0xFA00CDFBDB879090UL, context) // code: sti; int interrupt; cli;

// calls an address in real mode
#define realmode_call(segment, address, context)	realmode_execute(((uint64_t)(uint16_t)(segment) << 40) | ((uint64_t)(uint16_t)(address) << 24) | 0xFA000000009AFB90UL, context) // code: sti; call segment:address; cli;

static inline void realmode_reset(realmode_context_t *context) {
	memset(context, 0, sizeof(realmode_context_t));
}


// some more variables set up by the bootloader
extern void **memoryMapPtr;
extern void *tssDescriptor;
extern uint8_t *bootdisk;
extern uint32_t *volumeStart;


#endif //  __REALMODE_H__
