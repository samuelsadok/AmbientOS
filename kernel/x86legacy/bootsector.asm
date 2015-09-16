;*************************************************************************
; File:  HelloWorld.asm                     Version: 0.1 
; Autor: Michael Graf                       Date:    10.09.2006
; Description: 
;     Print the Hello World message, Wait for a key and reboot the system.
; History :
;     v0.1    2006.09.10 UTC    Inital version. Start of History
;*************************************************************************

; THIS BOOTSECTOR IS MADE FOR A FAT32 FILE SYSTEM



%define VESAInfo_Signature		(temp_buffer+0x00)
%define VESAInfo_Version		(temp_buffer+0x04)
%define ModeInfo_PhysBasePtr	(temp_buffer+0x28)



global __start

[BITS 16]			; 16bit realmode code
;[ORG 0x7C00]		; were loaded at this address (the linker will take care of this)
[SEGMENT .bootrec]	; tell the linker this is a bootrecord



__start:
	jmp MAIN
	db 0x90	; this still belongs to the jump field
	
	TIMES 90-($-$$) db 0x00	; placeholder for the FAT32 header

MAIN:
	mov     si, ldMsg1			; set SourceIndex
	call    DisplayMessage		; Display the message
	
	
	
	; LOAD BOOTLOADER FROM DISK
	
	mov si, DAPACK	; address of "disk address packet"
	mov ah, 0x42		; AL is unused
;	mov dl, [boot_drive]		; the drive number is preloaded from BIOS/MBR
	int 0x13
	jc ERROR_HANDLER
	
	
	
	mov     si, ldMsg2
	call    DisplayMessage
	
	
	
	; INVOKE BOOTLOADER
	
	mov     bx, 0x0000		; segmentlocation 0x0000
	mov     ds, bx			; set DataSegment to 0x0000
	mov     es, bx			; set ExtraSegment to 0x0000
	mov     ss, bx			; set StackSegment to 0x0000
	mov     sp, 0xFFFF		; set StackPointer to 0xFFFF
	jmp  WORD 0000h:8000h	; set CodeSegment to 0x0000 and jump to 0x8000
   
   
   


ERROR_HANDLER:
   mov     si, errMsg              ; set SourceIndex
   call    DisplayMessage          ; Display the message

   mov     ah, 0x00                ; 
   int     0x16                    ; await keypress
   int     0x19                    ; reboot computer






;********************************************************************
; PROCEDURE DisplayMessage
; display ASCII string at ds:si via BIOS INT 10h
;
; input ds:si   segment:offset message text
;
;********************************************************************
DisplayMessage:
	pusha                           ; save all registers to stack       

	mov ah, 0x0E                    ; BIOS teletype                     
	mov bx, 0x0007                  ; display text at page   0x00       
                                   ; text attribute         0x07
.DisplayLoop lodsb                           ; load next character
	test al, al                     ; test for NULL character
	jz .DONE                        ; if NULL exit printing message
	int 0x10                        ; invoke BIOS
	jmp .DisplayLoop                ; restart loop
 .DONE:
	popa                            ; load all saved registers from stack         
	ret                             ; exit function


DisplayHex:		; displays the byte in AL in hex
	pusha
	
	mov si, hexBuffer
	xor ebx, ebx
	
	mov bl, al
	shr bl, 4
	mov bl, [ebx + hexChars]
	mov [si + 2], bl
	
	mov bl, al
	and bl, 0x0F
	mov bl, [ebx + hexChars]
	mov [si + 3], bl
	
	call DisplayMessage
	
	popa
	ret
	
	
DisplayHex32:	; displays the 32-bit value in EAX in hex
	pusha
	
	call	DisplayHex
	shr		eax, 8
	call	DisplayHex
	shr		eax, 8
	call	DisplayHex
	shr		eax, 8
	call	DisplayHex
	
	popa
	ret




; Data area
boot_drive	db 0x00,

hexBuffer db "0x", 0x00, 0x00, " ", 0x00
hexChars   db "0123456789ABCDEF"

ldMsg1	db 0x0D, 0x0A, "loading bootloader...", 0x00
ldMsg2	db 0x0D, 0x0A, "launching bootloader...", 0x00
errMsg	db 0x0D, 0x0A, 0x0D, 0x0A, "Hoppa, Something went wrong", 0x0D, 0x0A, "Press any key to reboot", 0x00





		TIMES (512-48)-($-$$) db 0x00

; this pack will be the second last line of the sector in a hex editor
DAPACK:	db	0x10		; size of the transfer descriptor (16 B)
		db	0		; always 0
		dw	32		; number of sectors to transfer - (127 -> 64kB) - int 13 resets this to # of blocks actually read/written
		dw	0x8000	; memory buffer destination address (0:8000)
		dw	0		; in memory page zero
		dq	0x0000000000000002	; put the lba to read in this spot


		TIMES (16+14) db 0x00

		dw 0xAA55                          ; Bootsector Magic Number


; a 512-byte block will be put here during video init
temp_buffer:



