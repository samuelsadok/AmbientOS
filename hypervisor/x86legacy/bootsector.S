/*************************************************************************
* File:  HelloWorld.asm                     Version: 0.1 
* Autor: Samuel Sadok                       Date:    
* Description: 
*     Print the Hello World message, Wait for a key and reboot the system.
* History :
*     v0.1    2006.09.10 UTC    Inital version. Start of History
**************************************************************************/

// console size: 80x25 chars

#incude <machine/asm.h>


//#define DISABLE_OS
#define VERBOSE



.intel_syntax noprefix
.text

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


.globl _main
.globl __start


.code16			// 16bit realmode code
// .org 0x7C00		// BIOS loads us at this address

.org 0x00

_main:
__start:
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



Sector1Fail:

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
	jmp	after_win8_entry
windows8_entry:
.org 0x119
	jmp MAIN
after_win8_entry:
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
	jc	Sector1Fail
	test	ax, ax
	jz	Sector1Fail
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
	sub	ebx, [bytes_per_sector]
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
*   bx       	number of bytes to read
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
	mov	si, offset flat:disk_read_command	// address of "disk address packet"
	mov	ax, 0x4200				// disk read command
	mov	dl, [boot_drive]
	int	0x13
	jc	Sector1Fail

	// increment sector
	inc	dword ptr [disk_read_command+8]
	sub	bx, [bytes_per_sector]	// check if there's more to read
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


bootMsg1:	.asciz "\r\npreparing NTFS read..."
bootMsg2:	.asciz "\r\nloading root directory..."
bootMsg3:	.asciz "\r\nloading file..."
bootMsg4:	.asciz "\r\nloading file content..."
bootMsg5:	.asciz "\r\nloading complete."

errMsg0:	.asciz "\r\nhad small buffer,"
errMsg1:	.asciz "\r\nfile not found. press any key"
dbgMsg1:	.asciz "\r\npress any key to enter memory view"

directory_index_name: // $I30
	.byte 4, 0, '$', 0, 'I', 0, '3', 0, '0', 0
file_name:
	.byte 10, 0, 'k', 0, 'e', 0, 'r', 0, 'n', 0, 'e', 0, 'l', 0, '.', 0, 'b', 0, 'i', 0, 'n', 0






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
	mov	eax, [bytes_per_cluster]	// if positive, it specifies the number of clusters per MFT record
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
	mov	eax, 0x05		// root directory always has segment 0x05
	call	NTFSLoadMFTRecord

	// load our file
	print	bootMsg3
	mov	edi, offset flat: file_name
	call	NTFSLoadChild
	print	bootMsg4
	xor	eax, eax
	mov	edx, -1
	call	NTFSLoadFile
	print	bootMsg5

	// load the content of our file
	push	ds
	mov	ds, [file_buffer_segment]
	xor	esi, esi
	call	DisplayMessage
	pop	ds

	// jump to the beginning of the kernel
	pushw	word ptr [file_buffer_segment]
	pushw	0x00
	lret

	// jmp	DiagnosticsMode




AdvancedFail1:
	print	errMsg0
AdvancedFail2:
	print	errMsg1
AdvancedFail:


DiagnosticsMode:
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
*   cx       	number of bytes to read (rounded up to a multiple of sectors)
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
	push	cx

	// ebx = cluster position (in sectors)
	movzxb	ecx, byte ptr [sectors_per_cluster]
	mul	ecx
	xchg	eax, ebx

	// eax = offset (in sectors)
	movzxw	ecx, word ptr [bytes_per_sector]
	div	ecx

	// eax = sector to read from
	add	eax, ebx

	pop	bx
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
	mov	cx, [bytes_per_MFT_record]
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
	jz	AdvancedFail			// don't allow infinite loop due to offset of zero
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
	jnz	AdvancedFail			// because they aren't supported
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
	jle	AdvancedFail			// can't read from beyond the attribute
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
	pop	ecx
	push	ebx
	shr	cx, 4				// upper nibble: number of cluster bytes
	call	.load_compressed_integer	// retrieve (signed) cluster number
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

	// eax: bytes in this cluster
	// edx: cluster to start from

	// adjust offset and byte count for this set of clusters
	mov	ebx, [ebp + 0x1C]
	mov	dword ptr [ebp + 0x1C], 0	// offset for next cluster read will be 0
	sub	eax, ebx
	mov	ecx, [ebp + 0x14]
	cmp	ecx, eax
	jle	.fully_within_cluster
	mov	ecx, eax
.fully_within_cluster:
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
	// eax	pointer to the integer (incremented)
	// ebx	output
	// ecx	number of bytes to read (max. 4) (clobbered)
	add	eax, ecx		// move pointer
	mov	ebx, [eax - 4]		// load 4 bytes (the upper bytes are relevant)
	sub	cx, 4			// CAUTION: FAILS FOR NUMBERS > 4
	neg	cx			// ecx = 4 - ecx
	shl	cx, 3
	sar	ebx, cl			// arithmetic shift to remove bytes
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
	jz	AdvancedFail
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
	jz	AdvancedFail

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
	jz	AdvancedFail1				// the index buffer has been scanned
	
	sub	ecx, ebx				// keep track of remaining bytes
	jle	AdvancedFail2				// all index buffers have been scanned
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
	jz	AdvancedFail

	ret



//.org 1024 // 0x8000

.align	16

MFT_record:
// MFT records are buffered here
// the attribute content is then loaded
