.intel_syntax noprefix

/*
*
* x84_64 BOOTLOADER
*
*
*
* Promises by the bootloader:
*  - the CPU is in long mode, this implies:
*     - the CPUID instruction is supported
*     - SSE and SSE2 are supported
*  - the lower 1MB of physical memory is mapped to VA 0xFFFFFFFF80000000 - 0xFFFFFFFF800FFFFF
*  - the raw kernel binary followed by the cleared .bss section is mapped starting at VA 0xFFFFFFFF80100000
*  - all paging structures are mapped consecutively to VA 0xFFFFFF0000000000, starting with PML1T
*  - the page at VA 0xFFFFFF8040201000 (directly after PML4T) is mapped to a non-reserved physical page.
*  - the very last page in virtual address space is reserved for the stack and the stack pointer is 0.
*  - the reference count in all page table entries is set accordingly (see kernel source for description).
*  - the kernel arguments are passed as defined by the System V ABI, all arguments are 64-bit wide.
*  - the memory map that is passed as kernel argument contains an entry that reserves all bios and kernel memory.
*
*
* Promises by the kernel:
*  - the raw kernel binary is not larger than 500kB
*  - the raw kernel binary contains all sections required at runtime (i.e. .text and .data) except .bss
*  - the start and end addresses of the .bss section are stored as 64-bit pointers at addresses 0x8 and 0x10 in the raw kernel binary
*
*/








// console size: 80x25 chars

#incude <machine/asm.h>


//#define DISABLE_OS
#define VERBOSE


//.equ RESERVED_MEMORY, 0x104000


.section .bootsect

// THIS BOOTSECTOR WORKS ON NTFS, exFAT, FAT12/16/32 FILE SYSTEMS


.macro print string
#ifdef VERBOSE
	lea	si, \string	// set SourceIndex
	call	DisplayMessage		// Display the message
#endif
.endm

.macro dsp_newline
	dspchar	0x0D
	dspchar	0x0A
.endm

.macro dspchar ascii
	mov	al, \ascii
	call	DisplayChar
.endm

.macro failmsg16 message
	mov    esi, offset flat: .fail_message_\@
	jmp    EnterDebugMode16
.fail_message_\@:
	.asciz "\message"
.endm

.macro failmsg32 message
.code32
	mov    esi, offset flat: .fail_message_\@
	jmp    EnterDebugMode32
.fail_message_\@:
	.asciz "\message"
.code16
.endm




.code16			// 16bit realmode code
// .org 0x7C00		// BIOS loads us at this address

.org 0x00

bootsector_start:
	jmp windows7_entry	// entry point when loaded by the BIOS
	nop

.org 0x03

//	.ascii	"C0DE"




// int 12 can be used by the OS to probe how much memory is available.
// To protect this and other hooks, we need to hide some memory.
// 28 bytes
int12_hook_entry:
	push	bp
	lea	bp, [esp + 0x6]
	jmp	.int12_filter


.org 0x0B

bytes_per_sector:
	.word 0			// set in bootsector
sectors_per_cluster:
	.byte 0			// set in bootsector
.global boot_drive
boot_drive:
	.byte 0			// 0 or set by windows bootsector


.int12_filter:
	test	ax, ax		// only intercept ax = 0
	jnz	.int12_passthrough

	call	int12_passdown
	jc	.int12_return	// if there's an error, don't touch result
	// test	ax, ax
	// jz	.int12_return
	dec	ax		// hide one kilobyte
	jmp	.int12_return
	nop


.org 0x1C

.equ	MFT_first_cluster, volume_offset + 0x14	// set in the NTFS bootsector
.equ	MFT_record_size, volume_offset + 0x24	// set in the NTFS bootsector

.global volume_offset
volume_offset:
	.long 0x90909090	// sector where this volume starts (set in boot sector)


int12_passdown:
	pushd	0x12345678	// actual interrupt vector set during hooking
	call	int_passdown
	ret

.int12_passthrough:
	call	int12_passdown

.int12_return:
	pop	bp
	iret


// Int 13 may be used by the OS to reload the first sector of the volume.
// For the OS to work correctly, we need to repair the volume_offset value.
// 0x60 bytes
int13_hook_entry:
	push	bp
	lea	bp, [esp + 0x6]

	cmp	ah, 0x42		// only intercept ah = 0x42 (disk read)
	jne	.int13_passthrough
	cmp	dl, cs:[boot_drive]	// only intercept commands for the current drive
	jne	.int13_passthrough

.global disk_read_command
.equ	disk_read_command, .disk_read_command_cmp + 2	// we save an instruction by reusing the "0x10" from this instruction to mark the beginning of the data_read_command
.disk_read_command_cmp:	// must not fall into MFT_record_size field
	cmp	byte ptr [si], 0x10	// only intercept commands we understand
	jne	.int13_passthrough

	call	int13_passdown
	jc	.int13_return		// only intercept on success


	pushad
	push	es

	mov	eax, [si + 0x08]	// first sector
	mov	cx, [si + 0x02]		// number of sectors
	mov	di, [si + 0x04]		// destination address
	mov	es, [si + 0x06]		// destination segment
	mov	ebx, cs:[volume_offset]	// sector to be patched
	xor	edx, edx

.check_next_sector:
	cmp	eax, ebx		// only patch one particular sector
	jne	.dont_patch_sector
	mov	es:[edi + volume_offset], ebx
	mov	dword ptr es:[edi + 0x11], edx
	jmp	.dont_patch_sector

windows7_entry:
.org 0x7a
	jmp	MAIN

.dont_patch_sector:
	inc	eax
	add	di, cs:[bytes_per_sector]
	loop	.check_next_sector


	pop	es
	popad
	jmp	.int13_return

int13_passdown:
	pushd	0x12345678	// actual interrupt vector set during hooking
	call	int_passdown
	ret

.int13_passthrough:
	call	int13_passdown

.int13_return:
	pop	bp
	iret




// Call this routine to invoke the actual interrupt handler.
// The top element on the stack (below the return address)
// should contain the original interrupt vector (4 bytes).
// ebp should point to the flags register that will be returned using "reti".
// 0x27 bytes
int_passdown:
	pushf			// before calling, set up address and flags for reti
	lcall	[esp + 0x04]
	
	// after calling the interrupt handler, we need to copy the returned flags to the hook's return stack
	mov	[esp - 0x4], ax	// preserve ax and flags
	pushf

	// propagate the returned flags to be applied when the hook returns
	pop	ax
	test	word ptr [bp], 0x0200	// the flags register for the hook return resides here
	jz	.dont_restore_IF	// restore IF if it was set before interrupt call
	or	ax, 0x0200
.dont_restore_IF:
	mov	[bp], ax
	sub	esp, 2

	popf			// restore actual returned flags and ax
	mov	ax, [esp - 0x4]
	ret	0x4		// discard interrupt vector parameter from stack





EarlyFail:

#ifdef VERBOSE
	mov	ah, 0x00
	int	0x16                    // await keypress
#endif

ESCAPE:

#ifndef DISABLE_OS
	mov	ax, cs
	cmp	ax, 0x7c0		// if we're already the original VBR, jumping back makes no sense
	je	SHUTDOWN

	mov	dl, cs:[boot_drive]
	jmp	0x07c0:0000		// jump back to original VBR - this time it will execute as designed
#endif

SHUTDOWN:
	int	0x19                    // reboot computer



MAIN:

/*
	// SETUP
	// set all memory segments to 0x0000 and enables interrupts
	.byte	0xFA, 0x33, 0xC0
	.byte	0x8E, 0xC0, 0x8E, 0xD0, 0x8E, 0xE0, 0x8E, 0xE8
	.byte	0xBC, 0x00, 0x7C, 0xFB, 0x68, 0x00, 0x00, 0x1F, 0x1E, 0x68
	.word . + 3
	.byte 0xCB
*/

	// SETUP
	cli
	xor	ax, ax
	mov	ss, ax
	mov	esp, 0x7C00    // Set Stack Pointer to 0000:7C00

	// determine were we were loaded
	// we want to adjust the code & data segments
	// such that __start has an address of 0x0000
	call 	.load_ip	// this is position independent
.load_ip:
	pop	bx		// load instruction pointer
	sub	bx, offset flat:.load_ip
	shr	bx, 4
	mov	ax, cs		// load current code segment
	add	ax, bx

	push	ax
	pop	ds		// init data segment
	push	ax
	pushw	offset flat:.cs_initialized
	lret			// init code segment
.cs_initialized:

	// if we're loaded by the BIOS or MBR, dl contains the boot drive
	// if we're loaded by someone else, dl may be invalid but the
	// boot_drive field will be set on VBR sync
	mov	[boot_drive], dl

	// ax contains the segment we're in
	cmp	ax, 0x07c0			// check if we were loaded as a bootsector
	je	.is_genuine_bootloader		// if so, don't install hooks or sync VBR (in fact, the hooking code is not even available)

	jmp	after_win8_entry
windows8_entry:
.org 0x119
	jmp MAIN
after_win8_entry:

	pushw	0x07c0
	pop	es
	mov	eax, es:[bytes_per_sector]	// this includes the bootdrive field
	mov	[bytes_per_sector], eax		// populate vital fields required by the hooks and for loading new sectors

	mov	eax, [volume_offset]
	mov	ebx, es:[volume_offset]
	inc	ebx
	push	ebx
	mov	es:[volume_offset], eax		// repair original bootloader


	// see how much memory we have available
	xor	eax, eax
	mov	dword ptr es:[0x11], eax	// repair a field in the windows bootsector that must be zero
	int	0x12
	jc	EarlyFail
	test	ax, ax
	jz	EarlyFail
	dec	ax		// one kilobyte below the BIOS reserved space is where we'll store our hooks
	shl	ax, 6		// convert kB to segments
	mov	es, ax		// this is our hook-code segment


	// set up int 12h hook
	mov	eax, 0x12
	mov	si, offset flat: int12_passdown + 0x2
	mov	di, offset flat: int12_hook_entry
	call	InstallHook

	// set up int 13h hook
	mov	eax, 0x13
	mov	si, offset flat: int13_passdown + 0x2
	mov	di, offset flat: int13_hook_entry
	call	InstallHook

	// copy this sector (which now contains the prepared hooks) into reserved memory
	xor	si, si
	xor	di, di
	mov	cx, 512
	rep movsb

	pop	dword ptr [volume_offset]	// reference the sector where this bootloaders is stored

	sti



.is_genuine_bootloader:

	// calculate segments per sector
	mov	ax, [bytes_per_sector]
	shr	ax, 4
	mov	[segments_per_sector], ax


	// The hooks have been copied, so we can reclaim the memory they take up.	
	// clear 15 bytes for the 16 bytes disk_read_command
	// (this code takes only 12 bytes, yay we safe 3B)
	push	ds
	pop	es
	mov	di, offset flat: disk_read_command + 1
	mov	cx, 0xF
	xor	eax, eax
	rep stosb

	// the packet will have the following layout:
	// 1B	size of the transfer descriptor (0x10)
	// 1B	always 0
	// 2B	number of sectors to transfer - int 13 resets this to # of blocks actually read/written
	// 2B	memory buffer destination address
	// 2B	memory buffer destination segment
	// 4B	sector number

	// LOAD FILE SYSTEM SUPPORT CODE FROM (RELATIVE) SECTOR 2-16
	mov	eax, 1
	mov	ebx, 0x1000
	movzx	ecx, word ptr [bytes_per_sector]
	sub	ebx, ecx
	mov	di, ds
	addw	di, word ptr [segments_per_sector]
	call	LoadSectors


	// SUCCESS
	jmp ExtendedBootsequence










/********************************************************************
* PROCEDURE LoadSectors
* loads the specified sectors from hard drive via BIOS
*
* inputs:
*   eax		first sector to read relative to this volume
*   ebx       	number of bytes to read
*   di		destination segment (data will be written starting at di:0000)
*
* outputs:
*   ebx		zero or negative
*   di		first segment that was not written to
*
* clobbered:
*   eax, edx, esi
*
*********************************************************************/

LoadSectors:

	// add volume offset
	add	eax, [volume_offset]

	// configure read operation
	movw	[disk_read_command+2], 1
	mov	[disk_read_command+8], eax

.load_next_sector:
	// load destination segment and increment
	mov	[disk_read_command+6], di
	add	di, [segments_per_sector]

	// send command to BIOS
	push	ebx					// may be clobbered
	mov	si, offset flat:disk_read_command	// address of "disk address packet"
	mov	ax, 0x4200				// disk read command
	mov	dl, [boot_drive]
	int	0x13
	pop	ebx
	jc	EarlyFail

	// increment sector
	inc	dword ptr [disk_read_command+8]
	movzx	eax, word ptr [bytes_per_sector]
	sub	ebx, eax				// check if there's more to read
	ja	.load_next_sector

	ret






.org 512 - 2

segments_per_sector:		// calculated at runtime, recycles the boot sector signature
boot_sector_signature:
	.word	0xAA55


.org 512


bytes_per_cluster:
	.long 0			// calculated at runtime
bytes_per_MFT_record:
	.long 0			// calculated at runtime
sectors_per_MFT_record:
	.long 0			// calculated at runtime
MFT_record_segment:
	.word 0			// calculated at runtime
file_buffer_segment:
	.word 0			// calculated at runtime

.global memory_map_ptr
memory_map_ptr:
	.quad 0xFFFFFFFF80000000 // calculated at runtime


bootMsg1:	.asciz "\r\npreparing NTFS read..."
bootMsg2:	.asciz "\r\nloading root directory..."
bootMsg3:	.asciz "\r\nloading file..."
bootMsg4:	.asciz "\r\nloading file content..."
bootMsg5:	.asciz "\r\nloading complete."

errMsg:		.asciz "\r\n\r\nerror: "
dbgMsg1:	.asciz "\r\npress any key to enter memory view"

directory_index_name: // $I30
	.byte 4, 0, '$', 0, 'I', 0, '3', 0, '0', 0
file_name:
	.byte 10, 0, 'k', 0, 'e', 0, 'r', 0, 'n', 0, 'e', 0, 'l', 0, '.', 0, 'b', 0, 'i', 0, 'n', 0





/********************************************************************
* PROCEDURE InstallHook
* Installs a IVT hook by patching the IVT and the hook code.
* Note that the hook code should afterwards be placed somewhere safe
* to avoid being overwritten by some OS bootloader.
*
* inputs:
*   eax		interrupt number to hook
*   ds:si	address of the interrupt vector field in the hook code
*   es:di	entry point of the hook
*
* clobbered:
*   ebx
*
*********************************************************************/
InstallHook:
	push	es
	push	di

	push	eax
	sidt	[esp - 0x2]	// load IVT location
	pop	es
	pop	di
	and	edi, 0xFFFF
	mov	ebx, es:[edi + 4 * eax]
	mov	[si], ebx			// patch hook with interrupt vector
	mov	ebx, [esp]
	mov	es:[edi + 4 * eax], ebx		// patch IVT with hook address

	pop	di
	pop	es

	ret



ExtendedBootsequence:

	//jmp	DiagnosticsMode

	print	bootMsg1


	// DETERMINE SOME FILE SYSTEM METRICS
	// (mostly taken from win8 bootsector)

	// copy NTFS parameters from original bootloader
	pushw	0x07c0
	pop	es
	mov	eax, es:[volume_offset]
	mov	[volume_offset], eax
	mov	eax, es:[MFT_first_cluster]
	mov	[MFT_first_cluster], eax
	mov	ax, es:[MFT_record_size]
	mov	[MFT_record_size], ax
	

	// calculate bytes per cluster
	movzxw	eax, word ptr [bytes_per_sector]
	movzxb	ebx, byte ptr [sectors_per_cluster]
	mul	ebx
	mov	[bytes_per_cluster], eax

	// calculate bytes per MFT record
	mov	ecx, [MFT_record_size]
	cmp	cl, 0x00
	jg	.record_size_positive
	neg	cl				// if negative, 2^[-0x40] specifies the number of bytes per MFT record
	mov	eax, 1
	shl	eax, cl
	jmp	.divide_by_sector_size
.record_size_positive:
	movzx	eax, byte ptr [bytes_per_cluster]	// if positive, it specifies the number of clusters per MFT record
	mul	ecx

	// calculate sectors per MFT record
.divide_by_sector_size:
	mov	[bytes_per_MFT_record], eax
	movzxw	ebx, word ptr [bytes_per_sector]
	xor	edx, edx
	div	ebx				// this rounds down (remainder in edx)
	mov	[sectors_per_MFT_record], eax

	// calculate the MFT record buffer segment
	mov	bx, ds
	mov	eax, offset flat: MFT_record
	shr	ax, 4
	add	ax, bx
	mov	[MFT_record_segment], ax

	// calculate the file buffer segment
	mov	bx, [bytes_per_MFT_record]
	shr	bx, 4
	add	ax, bx
	mov	[file_buffer_segment], ax


	// load root directory
	print	bootMsg2
	mov	eax, 0x05		// root directory always has segment number 0x05
	call	NTFSLoadMFTRecord

	// load our file
	print	bootMsg3
	mov	edi, offset flat: file_name
	call	NTFSLoadChild

	// load the content of our file
	print	bootMsg4
	xor	eax, eax
	mov	edx, -1
	call	NTFSLoadFile
	print	bootMsg5
	mov	ebp, ecx

/*	// display the content of our file
	push	ds
	mov	ds, [file_buffer_segment]
	xor	esi, esi
	call	DisplayMessage
	pop	ds */


	// retrieve memory map from bios
	push	ss				// the entries will be put on the stack
	pop	es
	mov	edx, 0x534D4150			// edx = "SMAP"
	xor	ebx, ebx
	pushw	0x0000				// mark end of list

.int15_get_next_entry:
	mov	eax, 0x0000E820
	mov	ecx, 0x18
	sub	sp, 0x18
	mov	di, sp
	int	0x15
	jc	.int15_last_entry_invalid
	test	cx, cx
	jz	.int15_last_entry_invalid
	push	cx


	cmp	dword ptr [esp + 2 + 0x10], 0x1	// we only know type 1 (free) or 2 (reserved)
	je	.known_type
	mov	dword ptr [esp + 2 + 0x10], 0x2
.known_type:
	cmp	cx, 0x14			// extended attributes?
	jle	.no_extended_attributes
	test	word ptr [esp + 2 + 0x14], 0x1	// if so, there's an additional "don't ignore" flag
	jnz	.dont_ignore
	mov	dword ptr [esp + 2 + 0x08], 0x0
.dont_ignore:
.no_extended_attributes:

	cmp	dword ptr [esp + 2 + 0x08], 0x0
	jz	.int15_check_next_entry		// ignore empty entries
	cmp	dword ptr [esp + 2 + 0x10], 0x2
	je	.int15_check_next_entry		// ignore reserved areas


	// todo: check to see if the memory just above 1M is available


.int15_check_next_entry:
	test	ebx, ebx
	jnz	.int15_get_next_entry
	jmp	.int15_last_entry_valid

.int15_last_entry_invalid:
	add	sp, 0x18			// discard last entry
.int15_last_entry_valid:

	add	[memory_map_ptr], esp
	sub	dword ptr [memory_map_ptr], 0x1A	// another entry is inserted later on

	// hide cursor
	mov	ah, 0x01
	mov	cx, 0x2607
	int	0x10

	push	ebp				// store kernel file length
	jmp	SetupKernelEnvironment


	// jmp	DebugMode





AttributeOffsetZero:		failmsg16 "attribute list is corrupt"
AttributeNotSupported:		failmsg16 "attribute is compressed, sparse or encrypted"
OffsetOutOfRange:		failmsg16 "offset was out of range when loading attribute"
IndexRootNotFound:		failmsg16 "directory has no index root"
IndexAllocationNotFound:	failmsg16 "directory has no index allocation"
EntryNotFound1:			failmsg16 "directory entry not found in single index block"
EntryNotFound2:			failmsg16 "directory entry not found in multiple index blocks"
DataAttributeNotFound:		failmsg16 "the file has no data attribute"
A20Error:			failmsg16 "A20 could not be enabled"
CPUNotSupported0:		failmsg32 "the CPUID instruction is not supported"
CPUNotSupported1:		failmsg32 "some basic CPUID extensions are not available"
CPUNotSupported2:		failmsg32 "some extended CPUID functions are not available"
CPUNotSupported3:		failmsg32 "some of the required CPU features are not available"
CPUNotSupported4:		failmsg32 "some of the required CPU instructions are not available"
CPUNotSupported5:		failmsg32 "long mode or no-execute not supported by CPU"
KernelTooLarge:			failmsg32 "kernel larger than than 1GB"
KernelPanic:			failmsg16 "KERNEL PANIC!"
//Test32:				failmsg32 "entered protected mode"
//MappingComplete:		failmsg32 "page mapping complete"
//CPUChecked:			failmsg32 "CPU is compatible"
//ReadyFor64:			failmsg32 "ready to enter compatibility mode"
//MSRWritten:			failmsg32 "written to IA32_EFER"
//CR4Read:			failmsg32 "read from CR4"
//CR4Written:			failmsg32 "written to CR4"



// provides a debug mode entry that can be jumped to from anywhere in 32-bit protected mode
EnterDebugMode32:
.code32
	cli
	mov	bx, cs:[.boot_code_base1]
	shr	bx, 4
	push	bx
	pushw	offset flat: .real_mode_entry
	jmp	0x08:.protected_mode_entry_16
.code16
.protected_mode_entry_16:
	mov	eax, cr0
	and	al, 0xFE	// clear PE (Protection Enable) bit
	mov	cr0, eax

	mov	ds, bx
	xor	bx, bx
	mov	ss, bx
	lret
.real_mode_entry:
	

// provides a debug mode entry that can be jumped to from anywhere in 16-bit real mode
EnterDebugMode16:
	push	esi
	print	errMsg
	pop	esi
	call	DisplayMessage


DebugMode:

	print	dbgMsg1
	mov	ah, 0x00
	int	0x16                    // await keypress

	mov	ax, cs
	mov	ds, ax
	mov	es, ax
	xor	si, si

.display_loop:
	
	call	DisplayHexPage
	
.await_keypress:
	mov	ah, 0x00
	int	0x16                    // await keypress

	cmp	ah, 0x48		// up
	jne	.not_key1
	sub	si, 0x10
	jmp	.display_loop
	.not_key1:

	cmp	ah, 0x50		// down
	jne	.not_key2
	add	si, 0x10
	jmp	.display_loop
	.not_key2:

	cmp	ah, 0x49		// page up
	jne	.not_key3
	sub	si, 0x180
	jmp	.display_loop
	.not_key3:

	cmp	ah, 0x51		// page down
	jne	.not_key4
	add	si, 0x180
	jmp	.display_loop
	.not_key4:

	cmp	ah, 0x4B		// left
	jne	.not_key5
	mov	ax, es
	dec	ax
	mov	es, ax
	jmp	.display_loop
	.not_key5:

	cmp	ah, 0x4D		// right
	jne	.not_key6
	mov	ax, es
	inc	ax
	mov	es, ax
	jmp	.display_loop
	.not_key6:

	cmp	ah, 0x47		// home
	jne	.not_key7
	mov	ax, es
	sub	ax, 0x10
	mov	es, ax
	jmp	.display_loop
	.not_key7:

	cmp	ah, 0x4F		// end
	jne	.not_key8
	mov	ax, es
	add	ax, 0x10
	mov	es, ax
	jmp	.display_loop
	.not_key8:

	cmp	ah, 0x01		// esc
	jne	.not_key9
	jmp	ESCAPE
	.not_key9:

	jmp	.await_keypress

	



console_color:	.word 0x0007
DisplayChar:
	// displays the char in al
	// clobbered: bx
	mov	ah, 0x0E
	mov	bx, [console_color]
	int 0x10
	ret

hex_chars:	.ascii "0123456789ABCDEF"
DisplayHexByte:
	// displays the byte in ah in hex
	// clobbered: bx
	push	eax
	shr	ax, 12
	and	eax, 0x0000000F
	mov	al, [eax + hex_chars]
	call	DisplayChar
	pop	eax
	push	eax
	shr	ax, 8
	and	eax, 0x0000000F
	mov	al, [eax + hex_chars]
	call	DisplayChar
	pop	eax
	ret
	

DisplayHexLine:
	// displays the hex line at es:si
	// clobbered: ax, bx, si (incremented by 0x10)
	push	cx

	// print address
	pushw	word ptr [console_color]
	mov	word ptr [console_color], 0x03
	mov	ax, es
	call	DisplayHexByte
	shl	ax, 8
	call	DisplayHexByte
	dspchar	0x3A
	mov	ax, si
	call	DisplayHexByte
	shl	ax, 8
	call	DisplayHexByte
	dspchar	0x20
	dspchar	0x20
	popw	word ptr [console_color]

	mov	cx, 0x10
	.do_print_byte_hex:
	  mov	ah, es:[si]
	  call	DisplayHexByte
	  inc	si
	  dspchar 0x20
	loop .do_print_byte_hex
	sub	si, 0x10
	dspchar 0x20

	mov	cx, 0x10
	.do_print_byte_ascii:
	  mov	al, es:[si]
	  test  al, 0x80
	  jnz	.bound_val
	  cmp	al, 0x20
	  jge	.dont_bound_val
.bound_val:
	  mov	al, 0x2e
.dont_bound_val:
	  call	DisplayChar
	  inc	si
	loop .do_print_byte_ascii

	pop	cx
	ret


DisplayHexPage:
	// displays the page at es:si
	pusha
	dsp_newline
	mov	cx, 0x18
	.do_print_line:
	  call	DisplayHexLine
	  dsp_newline
	loop .do_print_line
	popa
	ret




/********************************************************************
* PROCEDURE DisplayMessage
* display ASCII string at ds:si via BIOS INT 10h
*
* input ds:si   segment:offset message text
*
*********************************************************************/
#ifdef VERBOSE
DisplayMessage:
	pusha                           // save all registers to stack       

	mov ah, 0x0E                    // BIOS teletype                     
	mov bx, 0x0007                  // display text at page   0x00       
                                   	// text attribute         0x07
.DisplayLoop: lodsb                     // load next character
	test al, al                     // test for NULL character
	jz .DONE                        // if NULL exit printing message
	int 0x10                        // invoke BIOS
	jmp .DisplayLoop                // restart loop
 .DONE:
	popa                            // load all saved registers from stack
	ret                             // exit function
#endif


/*
DisplayHex:		// displays the byte in AL in hex
	pusha
	
	mov si, .hexBuffer
	xor ebx, ebx
	
	mov bl, al
	shr bl, 4
	mov bl, [ebx + .hexChars]
	mov [si + 2], bl
	
	mov bl, al
	and bl, 0x0F
	mov bl, [ebx + .hexChars]
	mov [si + 3], bl
	
	call DisplayMessage
	
	popa
	ret

.hexBuffer:	.asciz "0x00 "
.hexChars:	.ascii "0123456789ABCDEF"
	
	
DisplayHex32:	// displays the 32-bit value in EAX in hex
	pusha
	
	call	DisplayHex
	shr	eax, 8
	call	DisplayHex
	shr	eax, 8
	call	DisplayHex
	shr	eax, 8
	call	DisplayHex
	
	popa
	ret

*/


/********************************************************************
* PROCEDURE CompareUnicode
* compares two unicode strings
* the first byte is interpreted as length in characters
* the second byte is ignored
*
* inputs:
*   es:esi	points to the first string to compare
*   ds:edi	points to the second string to compare
*
* outputs:
*   z-flag	set iif both strings are equal
*
*********************************************************************/

CompareUnicode:
	push	ax
	push	ecx

	// load and compare length
	movzxb	ecx, byte ptr [edi]
	cmp	cl, es:[esi]
	jne	.return
	test	cx, cx
	jz	.return

	// loop over every wchar (backwards) as long as they are equal
.compare_char:
	mov	ax, es:[esi + 2*ecx]
	cmp	ax, [edi + 2*ecx]
	loope	.compare_char

.return:
	// at this point the Z-flag is already correctly set
	pop	ecx
	pop	ax
	ret



/********************************************************************
* PROCEDURE LoadFromCluster
* loads the specified cluster
*
* inputs:
*   eax		relative to this cluster the loading will start
*   ebx		offset (in bytes) from the cluster in eax (should be a multiple of sector size)
*   ecx       	number of bytes to read (rounded up to a multiple of sectors)
*   di		destination segment (data will be written starting at di:0000)
*
* outputs:
*   di		first segment that was not written to
*
* clobbered:
*   eax, ebx, edx, esi
*
*********************************************************************/

LoadFromCluster:
	push	ecx

	// ebx = cluster position (in sectors)
	movzxb	ecx, byte ptr [sectors_per_cluster]
	mul	ecx
	xchg	eax, ebx

	// eax = offset (in sectors)
	movzxw	ecx, word ptr [bytes_per_sector]
	div	ecx

	// eax = sector to read from
	add	eax, ebx

	pop	ebx
	jmp	LoadSectors



/********************************************************************
* PROCEDURE NTFSLoadMFTRecord
* loads the specified MFT record from the MFT
*
* inputs:
*   eax		file reference (segment number to load)
*
* clobbered:
*   eax, ebx, ecx, edx, esi, edi
*
*********************************************************************/

NTFSLoadMFTRecord:

	// load the very first MFT record, which describes the MFT itself
	push	eax
	mov	eax, [MFT_first_cluster]
	xor	ebx, ebx
	movzx	ecx, word ptr [bytes_per_MFT_record]
	mov	di, [MFT_record_segment]
	call	LoadFromCluster
	pop	eax

	// load a specific part of the current "file" (that is, the MFT)
	// the part we want to load is the MFT record for the requested file
	mov	ebx, [bytes_per_MFT_record]
	mul	ebx
	mov	edx, ebx
	push	ebx
	call	NTFSLoadFile
	pop	ecx				// copy as many bytes as we loaded

	mov	ax, [file_buffer_segment]
	mov	es, ax
	xor	edi, edi
	.do_copy_record:
	  mov	al, es:[edi]
	  mov	[edi + MFT_record], al
	  inc	edi
	loop .do_copy_record

	ret





/********************************************************************
* PROCEDURE NTFSLoadAttibute
* loads an attribute from the currently loaded MFT record
* the attribute is stored in the file buffer
*
* inputs:
*   eax		offset into the attribute
*   ebx		attribute type
*   edx		maximum number of bytes to load (-1 to load entire attribute)
*   es:esi	pointer to the unicode attribute name
*		(null means don't care - otherwise the length should be non-zero)
*
* outputs:
*   ecx		the length of the the attribute that was read (-1 if not found)
*   Z-flag	set if the attribute was not found
*
* clobbered:
*   es, eax, edx
*
*********************************************************************/

NTFSLoadAttribute:
	mov	ecx, -1
	pushad
	mov	ebp, esp
	mov	eax, offset flat: MFT_record
	movzxb	edx, byte ptr [MFT_record + 0x14]

.check_next_attribute:
	test	edx, edx
	jz	AttributeOffsetZero		// don't allow infinite loop due to offset of zero
	add	eax, edx			// point eax to the beginning of the current attribute

	cmp	dword ptr [eax], 0xFFFFFFFF	// end of attribute list?
	je	.not_found
	cmp	[eax], ebx			// correct attribute type?
	mov	edx, [eax + 0x4]
	jne	.check_next_attribute

	test	esi, esi			// do we care about the name?
	jz	.found

	mov	cl, [eax + 0x09]
	movzxw	edi, word ptr [eax + 0x0A]
	test	edi, edi			// attribute name present?
	jz	.check_next_attribute
	lea	edi, [eax + edi - 2]
	mov	[edi], cl
	call	CompareUnicode			// correct attribute name?
	jne	.check_next_attribute

.found:
	// at this point, eax points to the attribute header
	mov	di, [file_buffer_segment]	// load destination segment for attribute value
	test	word ptr [eax + 0xC], 0xFFFF	// check compressed/encrypted/sparse flags
	jnz	AttributeNotSupported		// because they aren't supported
	test	word ptr [eax + 0x8], 1		// resident or non-resident attribute?
	jnz	.non_resident


.resident:
	// resident attributes reside inside the attribute header
	mov	ecx, [eax + 0x10]		// CAUTION: FAILS FOR FILES > 0xFFFF BYTES
	mov	[ebp + 0x18], ecx		// remember file length

	movzxw	ebx, word ptr [eax + 0x14]
	add	eax, ebx			// advance eax to point to the value

	add	eax, [ebp + 0x1C]		// add offset
	sub	ecx, [ebp + 0x1C]
	jle	OffsetOutOfRange		// can't read from beyond the attribute
	cmp	dword ptr [ebp + 0x14], -1
	je	.dont_bound_length
	cmp	ecx, [ebp + 0x14]
	jle	.dont_bound_length
	mov	ecx, [ebp + 0x14]		// use smaller length
.dont_bound_length:
	
	mov	es, di
	xor	ebx, ebx
	.do_copy_attr:
	  mov	dl, ds:[eax + ebx]
	  mov	es:[ebx], dl
	  inc	ebx
	loop .do_copy_attr

	jmp	.copy_complete
	

.non_resident:
	// non-resident attributes have dataruns that indicate
	// which clusters constitute the attribute content

	mov	ecx, [eax + 0x30]
	mov	[ebp + 0x18], ecx	// remember file length
	cmp	dword ptr [ebp + 0x14], -1
	jne	.length_specified
	mov	[ebp + 0x14], ecx	// read entire attribute
.length_specified:
	mov	edx, [eax + 0x10]	// this is the initial VCN for the data runs
	add	eax, [eax + 0x20]

.load_next_datarun:

	// abort if there are no more bytes to read
	cmp	dword ptr [ebp + 0x14], 0
	je	.runlist_complete

	// load the cluster number and count for this data run
	xor	ecx, ecx
	mov	cl, [eax]
	inc	eax
	test	cx, cx
	jz	.runlist_complete		// 0 marks the end of the dataruns
	push	ecx
	and	cx, 0x000F			// lower nibble: number of length bytes
	call	.load_compressed_integer	// retrieve cluster count
	shr	ebx, cl				// length is unsigned => logic shift
	pop	ecx
	push	ebx
	shr	cx, 4				// upper nibble: number of cluster bytes
	call	.load_compressed_integer	// retrieve (signed) cluster number
	sar	ebx, cl				// offset is signed => arithmetic shift
	add	edx, ebx
	pop	ebx
	push	eax				// push next datarun address
	mov	eax, ebx
	

	// eax: number of clusters
	// edx: cluster to start from

	// see if these clusters require loading
	push	edx
	mul	dword ptr [bytes_per_cluster]
	pop	edx
	cmp	eax, [ebp + 0x1C]		// see if requested data is beyond this datarun
	jg	.within_cluster
	sub	[ebp + 0x1C], eax		// if so, skip this data run
	pop	eax				// pop next datarun address
	jmp	.load_next_datarun
.within_cluster:

	// eax: bytes in this datarun
	// edx: cluster to start from

	// adjust offset and byte count for this set of clusters
	mov	ebx, [ebp + 0x1C]
	mov	dword ptr [ebp + 0x1C], 0	// offset for next cluster read will be 0
	sub	eax, ebx
	mov	ecx, [ebp + 0x14]
	cmp	ecx, eax
	jle	.fully_within_datarun
	mov	ecx, eax
.fully_within_datarun:
	sub	[ebp + 0x14], ecx	// subtract the number of bytes that will effectively be read from the remaining bytes to read

	// load ecx bytes from cluster edx plus ebx bytes offset
	push	edx
	mov	eax, edx
	call	LoadFromCluster
	pop	edx
	pop	eax				// pop next datarun address
	jmp	.load_next_datarun

.load_compressed_integer:
	// reads an integer of length ecx from [eax] into ebx
	// input:
	//  eax	 pointer to the integer (incremented)
	//  ecx	 number of bytes to read (min 0, max. 4)
	// output:
	//  ebx  left-aligned integer that was read
	//   cl  number of bits to shift ebx by to obtain the result
	add	eax, ecx		// move pointer
	mov	ebx, [eax - 4]		// load 4 bytes (the upper bytes are relevant)
	sub	cx, 4			// CAUTION: FAILS FOR NUMBERS > 4
	neg	cx			// ecx = 4 - ecx
	shl	cx, 3
	ret


.not_found:
.copy_complete:
.runlist_complete:
	popad
	cmp	ecx, -1
	ret






/********************************************************************
* PROCEDURE NTFSLoadChild
* loads the MFT record of a child of the directory, whos MFT record
* is currently loaded
*
* inputs:
*   ds:edi	pointer to the unicode child name
*
* clobbered:
*   most registers
*
*********************************************************************/

NTFSLoadChild:
	mov	esi, offset flat: directory_index_name	// directory index is named "$I30"

	push	ds
	pop	es			// "$I30" string is in data segment
	xor	eax, eax		// load from offset 0
	mov	edx, -1			// load full length
	mov	ebx, 0x90
	call	NTFSLoadAttribute	// load index root attribute
	jz	IndexRootNotFound
	mov	eax, 0x10		// in an index root attribute, the offset is at 0x10

	mov	es, [file_buffer_segment]
	test	word ptr es:[0x1C], 1	// check if this is a large index
	jz	.scan_small_index

	push	ds
	pop	es			// "$I30" string is in data segment
	xor	eax, eax		// load from offset 0
	mov	edx, -1			// load full length
	mov	ebx, 0xA0
	call	NTFSLoadAttribute	// load index allocation attribute
	jz	IndexAllocationNotFound

	mov	es, [file_buffer_segment]
	mov	bx, es:[0x20]
	add	bx, 0x18
	push	bx			// store length of one index buffer (in segments)
	mov	eax, 0x18		// in an index allocation attribute, the offset is at 0x18
	jmp	.scan_large_index

.scan_small_index:
	pushw	0

.scan_large_index:
	mov	edx, es:[eax]

.scan_next_entry:
	add	eax, edx				// edx holds the offset to the next index
	mov	ebx, es:[eax + 0x0]
	movzxw	edx, word ptr es:[eax + 0x8]
	lea	esi, [eax + 0x50]
	call	CompareUnicode
	je	.entry_found
	// test	word ptr es:[eax + 0x0C], 0x0001
	// jnz	AdvancedFail1
	test	word ptr es:[eax + 0x0C], 0x0002	// flag 0x02 marks the last entry of an index buffer
	jz	.scan_next_entry

	pop	bx
	and	ebx, 0xFFFF
	jz	EntryNotFound1				// the index buffer has been scanned
	
	sub	ecx, ebx				// keep track of remaining bytes
	jle	EntryNotFound2				// all index buffers have been scanned
	push	bx
	shr	bx, 4
	mov	ax, es
	add	ax, bx					// advance to the next index buffer
	mov	es, ax
	mov	eax, 0x18

	jmp	.scan_large_index


.entry_found:
	// ebx holds a reference to the MFT record of the current entry
	mov	eax, ebx
	call	NTFSLoadMFTRecord
	pop	bx
	ret
	




/********************************************************************
* PROCEDURE NTFSLoadFile
* loads the contents of the file of which the MFT record
* is currently loaded into the file buffer
*
* input:
*   eax		offset into the file
*   edx		maximum number of bytes to load (-1 to load entire file)
*
* clobbered:
*   esi, eax, ebx, ecx
*
*********************************************************************/

NTFSLoadFile:
	xor	esi, esi		// data attribute has no name
	mov	ebx, 0x80		// attribute 0x80 is the file content
	call	NTFSLoadAttribute
	jz	DataAttributeNotFound

	ret





/********************************************************************
* TestA20
* tests if the A20 line is enabled
*
* output:
*   Z-flag	clear if A20 is enabled
*
* clobbered:
*   ax
*
*********************************************************************/

TestA20:
	pushw	0x0000
	pushw	0xFFFF
	pop	gs
	pop	fs
	mov	ax, fs:[0x7c00]
	cmp	ax, gs:[0x7c10]
	jne	.test_a20_done
	not	word ptr fs:[0x7c00]
	mov	ax, fs:[0x7c00]
	cmp	ax, gs:[0x7c10]
	not	word ptr fs:[0x7c00]
.test_a20_done:
	ret






/********************************************************************
* SetupKernelEnvironment
* initializes the GDT, switches to protected mode, sets up paging,
* copies kernel to start address, enables paging and long mode
* and finally jumps to kernel
*
* stack at entry:
*   - length of kernel file (4 bytes)
*
*********************************************************************/


SetupKernelEnvironment:

	// try to enable A20

	// check if A20 is already enabled
	call	TestA20
	jnz	.a20_enabled

	// method 1: BIOS
	mov	ax, 0x2401
	int	0x15
	call	TestA20
	jnz	.a20_enabled

	// method 2: keyboard controller
	// (not implemented)

	// method 3: Fast A20
	in	al, 0xEE
	in	al, 0x92
	test	al, 2
	jnz	.no_fast_a20
	or	al, 2
	and	al, 0xFE
	out	0x92, al

	// check in a loop this time (controller may require some time)
	xor	ecx, ecx
.a20_wait_1:
	call	TestA20
	jnz	.a20_enabled
	loop	.a20_wait_1
.no_fast_a20:

	jmp	A20Error

.a20_enabled:

	// disable interrupts - they will not be enabled until much later on in the kernel
	cli
	sidt	[idtr_bios]
	
	// adjust some fields that must be relative to the code location
	push	ds
	pop	ax
	and	eax, 0xffff
	shl	eax, 0x4
	mov	[.boot_code_base1], ax
	mov	[.boot_data_base1], ax
	mov	[.boot_code_base2], ax
	mov	[.boot_data_base2], ax
	add	[.long_mode_address], ax
	add	[.long_mode_address2], ax
	add	[gdtr + 2], eax

	// load GDT
	lgdt	[gdtr]

	// enable 32-bit protected mode
	mov	eax, cr0
	or	al, 0x1
	mov	cr0, eax
	jmp	0x18:[protected_mode_entry] // far jump to 32-bit code

.code32

protected_mode_entry:
	mov	ax, 0x20 // selector data segment adjusted for code location
	// (not used)
	mov	ax, 0x30 // selector data segment starting at 0
	mov	ds, ax
	mov	ss, ax
	mov	es, ax
	mov	fs, ax
	mov	gs, ax

//	jmp	Test32

	// copy kernel from file buffer to the memory after the BIOS
	movzx	esi, word ptr cs:[file_buffer_segment]
	shl	esi, 0x4
	mov	edi, 0x100000	// destination address
	pop	ecx		// pop file length
	shr	ecx, 2		// the kernel sections must be 4-byte aligned
	rep movsd		// copy sections from flat binary
	mov	ecx, [0x100010]	// load "__bss_end" field
	sub	ecx, [0x100008]	// determine length of .bss section
	shr	ecx, 2
	xor	eax, eax
	rep stosd		// clear .bss section

	// move edi after the last used physical page
	test	edi, 0xFFF
	jz	.no_padding
	add	edi, 0x1000
	and	edi, ~0xFFF
.no_padding:

	// clear three pages for PML4T, stack and temp page
	mov	esi, edi
	mov	ecx, 0xC00
	rep stosd		// eax is still 0

//	jmp	ReadyForMapping

	// map PML4T, temp page and stack
	mov	eax, 0xF8040200
	mov	ebx, esi
	call	MapPage
	inc	eax
	add	ebx, 0x1000
	call	MapPage			// map the the temp page to a well known virtual address
	mov	eax, 0xFFFFFFFF		// the very last virtual page becomes the stack
	add	ebx, 0x1000
	call	MapPage

	// map physical pages starting at 0x00000000 to virtual pages starting at 0xFFFFFFFF80000000
	mov	ecx, esi	// the physical memory to be mapped stops where the PML4T starts
	shr	ecx, 12
	mov	eax, 0xFFF80000
	xor	ebx, ebx
.map_next_page:
	call	MapPage
	inc	eax
	add	ebx, 0x1000
	loop .map_next_page

	// set up 1:1 mapping for the lower 1GB of the 2GB kernel space
	mov	eax, [esi + 0xFF8]
	and	eax, 0xFFFFF000
	mov	eax, [eax + 0xFF0]	// copy the PML3TE from the actual PML3T that controls the kernel space
	mov	[esi + 0x1000], eax	// use temp page as PML3T for the least 512GB
	lea	eax, [esi + 0x1000]
	or	eax, 0x00000003
	mov	[esi], eax		// map 1st PML4T entry to the temp page

	// load PML4T base address
	mov	eax, esi
	mov	cr3, eax

//	jmp	MappingComplete

	// add an entry to the memory map that specifically reserves the memory that was allocated up to this point
	sub	sp, 0x18
	mov	dword ptr [esp + 0x00], 0x0		// base address
	mov	dword ptr [esp + 0x04], 0x0		// fill
	mov	dword ptr [esp + 0x08], edi		// length
	mov	dword ptr [esp + 0x0C], 0x0		// fill
	mov	dword ptr [esp + 0x10], 0x2		// type: reserved
	pushw	0x14					// small entry


	// determine if the CPUID instruction is supported
	pushf
	pop	eax
	mov	ecx, eax
	xor	eax, 0x200000	// flip ID bit in EFLAGS
	push	eax
	popf
	pushf
	pop	eax
	xor	eax, ecx	// see if flipping worked
	je	CPUNotSupported0

	// check the available CPUID functions
	xor	eax, eax
	cpuid
	cmp	eax, 0x1
	jl	CPUNotSupported1
	mov	eax, 0x80000000
	cpuid  
	cmp	eax, 0x80000008
	jl	CPUNotSupported2

	// CPUID.01H:
	// request MSRs, physical address extension
	mov	eax, 0x1
	cpuid
	mov	eax, 0x00060
	and	edx, eax
	cmp	edx, eax
	jne	CPUNotSupported3
	mov	eax, 0x0000000
	and	ecx, eax
	cmp	ecx, eax
	jne	CPUNotSupported4

	// CPUID.80000001H:
	// request long mode and no-execute
	mov	eax, 0x80000001
	cpuid
	mov	eax, 0x20000000
	and	edx, eax
	cmp	edx, eax
	jne	CPUNotSupported5

	// enable physical address extension
	mov	ecx, 0xc0000080	// IA32_EFER MSR
	rdmsr
	or	eax, 0x901
	wrmsr

	// enable physical address extension
	mov	eax, cr4
	or	eax, 0x20
	mov	cr4, eax

	// load kernel arg
	movzx	edi, word ptr cs:[.boot_code_base1]

	// enable paging and enter long mode
	mov	eax, cr0
	or	eax, 0x80010000
	mov	cr0, eax

	.byte	0xEA		// far jump to 64-bit long mode
.long_mode_address:
	.long	long_mode_entry
	.word	0x0028		// 64-bit code segment

long_mode_entry:

.code64
	mov	rax, 0xFFFFFFFF80000000	// actual address of bios code and kernel

	add	[gdtr + edi + 2], rax	// not that 64-bit code doesn't support references like cs:[gdtr]
	lgdt	[gdtr + edi]

	// initial kernel stack is at the top of the memory space
	xor	rsp, rsp

	// jump out of 1:1 mapping
	jmp	.high_jump_call
.high_jump:
	add	[rsp], rax
	ret
.high_jump_call:
	call	.high_jump

	// unmap PML3T for temporary 1:1 mapping
	mov	r10, 0xFFFFFF8040200000
	mov	dword ptr [r10], 0

	// jump to kernel
	add	rax, 0x100000
	jmp	rax








#define REALMODE_CONTEXT_SIZE	(0x28)

.global RealModeCall
RealModeCall:
	push	rbp	// save registers as specified in System V ABI
	push	rbx
	push	r12
	push	r13
	push	r14
	push	r15

	// store code to be executed
	mov	rbx, rdi
	mov	rdx, rdi
	shr	rdx, 32

	// store context address
	push	rsi

	mov	rax, rsp
	mov	rsp, 0x7c00	// switch to bootloader stack
	push	rax		// store kernel stack address
	
	// copy real mode context to bootloader stack
	mov	rcx, REALMODE_CONTEXT_SIZE
	sub	rsp, rcx
	mov	rdi, rsp
	rep movsb

	// exit long mode
	mov	rax, (0x18 << 32)
	add	rax, offset flat: .switch_64_to_32
	push	rax
	lret
.switch_64_to_32:
.code32

	// set up real mode address
	mov	cx, cs:[.boot_code_base1]
	shr	cx, 4
	push	cx
	pushw	offset flat: .switch_protected_to_real
	
	// adjust GDT address
	movzx	edi, word ptr cs:[.boot_code_base1]
	and	dword ptr [gdtr + edi + 2], 0x000FFFFF
	mov	dword ptr [gdtr + edi + 6], 0
	lgdt	[gdtr + edi]

	// disable paging and exit long mode
	mov	eax, cr0
	and	eax, ~0x80000000
	mov	cr0, eax

	// disable protected mode
	jmp	0x08:.switch_32_to_16
.code16
.switch_32_to_16:
	mov	eax, cr0
	and	al, 0xFE	// clear PE (Protection Enable) bit
	mov	cr0, eax

	xor	cx, cx
	mov	ss, cx
	lret
.switch_protected_to_real:
	
	// swap interrupt table
	lidt	cs:[idtr_bios]

	// setup real mode code
	mov	cs:[.realmode_code], ebx
	mov	cs:[.realmode_code+4], edx

	// load real mode context	
	pop	ds
	pop	es
	pop	fs
	pop	gs
	popad

	// execute realmode code
.realmode_code:
	.quad	0	// real mode code is inserted here at runtime

	// store real mode context
	pushfd
	pushad
	push	gs
	push	fs
	push	es
	push	ds

	// enter 32-bit protected mode
	mov	eax, cr0
	or	al, 0x1
	mov	cr0, eax
	jmp	0x18:[.switch_16_to_32]
.code32
.switch_16_to_32:
	

	// restore GDT address
	movzx	edi, word ptr cs:[.boot_code_base1]
	add	dword ptr [gdtr + edi + 2], 0x80000000
	mov	dword ptr [gdtr + edi + 6], 0xFFFFFFFF

	// enter 32-bit compatibility mode
	mov	eax, cr0
	or	eax, 0x80000000
	mov	cr0, eax

	// enter 64-bit long mode
	.byte	0xEA
.long_mode_address2:
	.long	.switch_32_to_64
	.word	0x0028
.switch_32_to_64:
.code64

	// restore GDT
	lgdt	[gdtr + edi]

	// restore kernel memory segments
	mov	ax, 0x30
	mov	ds, ax
	mov	ss, ax
	mov	es, ax
	mov	fs, ax
	mov	gs, ax

	// load return value and switch stack
	mov	eax, [rsp + REALMODE_CONTEXT_SIZE]
	mov	rsi, [rsp + 4 + REALMODE_CONTEXT_SIZE]
	xchg	rsi, rsp

	// copy real mode context
	pop	rdi
	mov	rcx, REALMODE_CONTEXT_SIZE
	rep movsb


	pop	r15
	pop	r14
	pop	r13
	pop	r12
	pop	rbx
	pop	rbp
	ret		// return to kernel mode code















.code32




/********************************************************************
* PROCEDURE MapPage
* Maps a 4kB physical page to a virtual page.
* If new paging structures must be generated in the process, these
* are also mapped to their appropriate virtual addresses.
*
* input:
*   eax		virtual page to be mapped (see below for format description)
*   ebx		physical address to be mapped (should be 4kB page aligned)
*   esi		physical address of the PML4T
*   edi		the first unused physical memory location
*
* output:
*   edi		the first unused physical memory location after
*		all required tables have been allocated
*
* Format of eax:
*  VA 0xFFFFF00000000000 <=> eax 0x00000000 (first VA that can be mapped)
*  VA 0xFFFFF00000001000 <=> eax 0x00000001 (second VA that can be mapped)
*  VA 0xFFFFFFFFFFFFF000 <=> eax 0xFFFFFFFF (last VA that can be mapped)
*
* Virtual addresses of the mapped tables:

*  adjusted: accounting for the lower 510/512 that can't be mapped
*  PML4: base FFFFFF7FBFDFF000, adjusted FFFFFF7FBFDFFF00, eax F7FBFDFF
*  PML3: base FFFFFF7FBFE00000, adjusted FFFFFF7FBFFE0000, eax F7FBFFE0
*  PML2: base FFFFFF7FC0000000, adjusted FFFFFF7FFC000000, eax F7FFC000
*  PML1: base FFFFFF8000000000, adjusted FFFFFFF800000000, eax FF800000
* NEW:
*  PML4T: base FFFFFF8040200000, adjusted FFFFFF8040200F00, eax F8040200
*  PML3T: base FFFFFF8040000000, adjusted FFFFFF80401E0000, eax F80401E0
*  PML2T: base FFFFFF8000000000, adjusted FFFFFF803C000000, eax F803C000
*  PML1T: base FFFFFF0000000000, adjusted FFFFFF7800000000, eax F7800000
*
*********************************************************************/

MapPage:
	pusha

	// set up local variables
	push	ebx		// store physical address of mapped page
	mov	ebx, eax
	and	ebx, 0x1FF
	push	ebx		// store PML1TE index
	shr	eax, 9
	mov	ebx, eax
	add	ebx, 0xF7800000
	push	ebx		// store virtual page for PML1T
	mov	ecx, edi
	push	ecx		// store physical address for PML1T
	mov	ebx, eax
	and	ebx, 0x1FF
	push	ebx		// store PML2TE index
	shr	eax, 9
	mov	ebx, eax
	add	ebx, 0xF803C000
	push	ebx		// store virtual page for PML2T
	add	ecx, 0x1000
	push	ecx		// store physical address for PML2T
	mov	ebx, eax
	and	ebx, 0x1FF
	push	ebx		// store PML3TE index
	shr	eax, 9
	mov	ebx, eax
	add	ebx, 0xF80401E0
	push	ebx		// store virtual page for PML3T
	add	ecx, 0x1000
	push	ecx		// store physical address for PML3T (this is the highest address, since if PML3T must be allocated, this implies that PML2T and PML1T must also be allocated)
	or	eax, 0x1E0	// 4 upper bits are well defined
	push	eax		// store PML4TE index
	
	xor	ecx, ecx		// ecx counts the number of new tables created

	// look at the PML4TE to find PML3T
	mov	eax, [esp + 0x00]
	lea	esi, [esi + 8 * eax]
	mov	ebx, [esi]		// load PML4TE
	test	ebx, 1			// check if PML3T is present
	jnz	.pml4te_present
	mov	ebx, [esp + 0x04]	// entry not present - use the reserved page for it
	call	ClearPage
	or	ebx, 0x3		// mark writable and present
	mov	[esi], ebx		// write new entry
	inc	ecx			// remember the number of tables pages to map
.pml4te_present:
	mov	edx, esi		// store address of PML4TE (may be used later)
	and	ebx, 0xFFFFF000
	mov	esi, ebx		// set esi to point to the physical address of the PML3T

	// look at the PML3TE to find PML2T
	mov	eax, [esp + 0x0C]
	lea	esi, [esi + 8 * eax]
	mov	ebx, [esi]
	test	ebx, 1
	jnz	.pml3te_present
	mov	ebx, [esp + 0x10]
	call	ClearPage
	or	ebx, 0x3
	mov	[esi], ebx
	inc	ecx
	add	dword ptr [edx + 4], (1 << (53 - 32))	// increment reference count in PML4TE
.pml3te_present:
	mov	edx, esi
	and	ebx, 0xFFFFF000
	mov	esi, ebx

	// look at the PML2TE to find PML1T
	mov	eax, [esp + 0x18]
	lea	esi, [esi + 8 * eax]
	mov	ebx, [esi]
	test	ebx, 1
	jnz	.pml2te_present
	mov	ebx, [esp + 0x1C]
	call	ClearPage
	or	ebx, 0x3
	mov	[esi], ebx
	inc	ecx
	add	dword ptr [edx + 4], (1 << (53 - 32))
.pml2te_present:
	mov	edx, esi
	and	ebx, 0xFFFFF000
	mov	esi, ebx

	// update PML1TE to reference the page to be mapped
	mov	eax, [esp + 0x24]
	lea	esi, [esi + 8 * eax]
	mov	ebx, [esp + 0x28]
	or	ebx, 0x3
	mov	[esi], ebx
	add	dword ptr [edx + 4], (1 << (53 - 32))
	add	dword ptr [esi + 4], (1 << (53 - 32))
	or	dword ptr [esi + 4], (1 << (52 - 32))	// mark as fully allocated


	test	ecx, ecx
	jz	.map_no_table
	mov	eax, 0x1000
	mul	ecx
	add	edi, eax
	mov	esi, [esp + 0x30]
	mov	edx, 0x1C
.map_next_table:
	mov	eax, [esp + edx + 4]
	mov	ebx, [esp + edx]
	call	MapPage
	sub	edx, 0xC
	loop	.map_next_table
.map_no_table:
	
	add	esp, 0x2C
	mov	[esp], edi	// set edi to be returned
	popa
	ret





/********************************************************************
* PROCEDURE ClearPage
* Clears the page at the specified physical address
*
* input:
*   ebx		physical address of the page to be cleared
*
*********************************************************************/

ClearPage:
	push	eax
	push	ecx
	push	edi
	xor	eax, eax
	mov	ecx, 0x400
	mov	edi, ebx
	rep stosd
	pop	edi
	pop	ecx
	pop	eax
	ret




.align 4
idtr_bios:
	.word	0
	.quad	0
.align 4
idtr_null:
	.word	0
	.quad	0

.align 4

gdtr:	// global descriptor table register
	.word	gdt_end - gdt - 1	// GDT limit
	.quad	gdt			// GDT start

gdt:	// global descriptor table
	// selector 0x00: null entry
	.quad	0x0000000000000000
	
	// selector 0x08: 16-bit code segment (base set at runtime, limit 0xFFFFFFFF)
	.word	0xFFFF		// lower 16bit of limit (in pages)
.boot_code_base1:
	.byte	0x00, 0x00, 0x00	// lower 24bit of base address
	.byte	0x9E			// access type
	.byte	0x0F			// upper 8bit of limit (in pages) plus 2 flags (32-bit and page granularity)
	.byte	0x00			// upper 8bit of base address
	
	// selector 0x10: data segment (base set at runtime, limit 0xFFFFFFFF)
	.word	0xFFFF
.boot_data_base1:
	.byte	0x00, 0x00, 0x00
	.byte	0x92
	.byte	0x0F
	.byte	0x00
	
	// selector 0x18: 32-bit code segment (base set at runtime, limit 0xFFFFFFFF)
	.word	0xFFFF
.boot_code_base2:
	.byte	0x00, 0x00, 0x00
	.byte	0x9E
	.byte	0xCF
	.byte	0x00
	
	// selector 0x20: data segment (base set at runtime, limit 0xFFFFFFFF)
	.word	0xFFFF
.boot_data_base2:
	.byte	0x00, 0x00, 0x00
	.byte	0x92
	.byte	0xCF
	.byte	0x00
	
	// selector 0x28: 64-bit code segment (base 0x00000000, limit 0xFFFFFFFF)
	.word	0xFFFF
	.byte	0x00, 0x00, 0x00
	.byte	0x98
	.byte	0x20
	.byte	0x00
	
	// selector 0x30: data segment (base 0x00000000, limit 0xFFFFFFFF)
	.word	0xFFFF
	.byte	0x00, 0x00, 0x00
	.byte	0x92
	.byte	0xAF
	.byte	0x00
	
	// selector 0x38: tss (setup and used by the kernel)
.global tss_descriptor
tss_descriptor:
	.quad	0
	.quad	0
	
	
gdt_end:



//.org 1024 // 0x8000

.align	16

MFT_record:
// MFT records are buffered here
// the attribute content is then loaded

.global RealModeBuffer
RealModeBuffer:
// this space is used by the kernel for real mode code execution
