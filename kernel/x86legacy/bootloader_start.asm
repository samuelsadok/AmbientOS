;*************************************************************************
; File:  HelloWorld.asm                     Version: 0.1 
; Autor: Michael Graf                       Date:    10.09.2006
; Description: 
;     Print the Hello World message, Wait for a key and reboot the system.
; History :
;     v0.1    2006.09.10 UTC    Inital version. Start of History
;*************************************************************************


global __start
global ___main

extern _main
extern _vga_buffer

[BITS 16]                             ; 16bit realmode code

[SECTION .start]

__start:
	
	mov si, hiMsg1
	call DisplayMessage
	
	
	
	; SET UP ENHANCED VIDEO
	
;	mov   dword [VESAInfo_Signature],'VBE2'
;	mov   ax,4f00h			; Is Vesa installed ?
;	mov   di,temp_buffer	; This is the address of our info block.
;	int   10h
;	cmp   ax,004Fh			; successful?
;	jne   near ERROR_HANDLER
;
;	cmp   dword [VESAInfo_Signature], 'VESA'	; check signature
;	jne   near ERROR_HANDLER
;
;	cmp   byte [VESAInfo_Version+1], 2	; VESA version 2.0 or higher?
;	jb    ERROR_HANDLER
;
;	mov   ax,4f01h			; Get Vesa Mode information.
;	mov   di,temp_buffer	; This is the address of our info block.
;	mov   cx,0x4101			; 4112h = 32/24bit ; 0x4101 = 8bit ;4111h = 15bit (640*480)
;	and   cx,0xfff
;	int   10h
;	cmp   ax,004Fh			; successful?
;	jne   near ERROR_HANDLER
;	
;	mov   ax,4f02h			; Set video mode
;	mov   bx,0x4101			; 640x480x256
;	int   10h
;	cmp   ax,004Fh			; successful?
;	jne   near ERROR_HANDLER
;
;	mov	  eax, [ModeInfo_PhysBasePtr]	; remember video buffer location
;
;	; make video buffer pointer relative to our segment
;	xor ebx, ebx
;	mov	bx, ds
;	shl	bx, 4
;	sub	eax, ebx
;	mov [_vga_buffer], eax	; remember video location
;	
;	call DisplayHex32





	mov si, hiMsg2
	call DisplayMessage
	
	mov	ah, 01h		; hide cursor
	mov	cx, 2607h
	int	10h
	
	;mov     ah, 0x00
	;int     0x16                    ; await keypress
	
	
	
	; SWITCH TO PROTECTED MODE
	
	cli          ; disable interrupts
	lgdt [gdtr]  ; load GDT register with start address of Global Descriptor Table
	mov eax, cr0
	or al, 1     ; set PE (Protection Enable) bit in CR0 (Control Register 0)
	mov cr0, eax

	jmp 08h:ENTER_PROTECTED_MODE	; 0x08 is our new code selector
	
[BITS 32]

ENTER_PROTECTED_MODE:
	mov   AX, 0x10		; 0x10 is our new data selector
	mov   DS, AX
	mov   ES, AX
	mov   FS, AX
	mov   GS, AX
	mov   SS, AX

	; LAUNCH C CODE
	jmp	08h:_main		; 0x08 is our new code selector










___main: ; this called by the C function main() for some reason
	ret
	
	
	








[BITS 16]



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


hexBuffer db "0x", 0x00, 0x00, " ", 0x00
hexChars   db "0123456789ABCDEF"

hiMsg1	db 0x0D, 0x0A, "Hi. This is bootloader. Wow.", 0x00
hiMsg2	db 0x0D, 0x0A, "Do key for make protected and empty.", 0x0D, 0x0A, "Then much amazing C.", 0x00


	
	
	
	
	
	
gdtr:	; global descriptor table register
	dw (gdt_end-gdt)	; GDT lenght
	dd gdt				; GDT start

gdt:	; global descriptor table
	; selector 0x00: null entry
	dq 0x0000000000000000
	
	; selector 0x08: code segment (base 0x00000000, limit 0xFFFFFFFF)
	dw 0xFFFF ; lower 16bit of limit (in pages)
	db 0x00, 0x00, 0x00 ; lower 24bit of base address
	db 0x9E ; access type: code
	db 0xCF ; upper 8bit of limit (in pages) plus 2 flags (32-bit and page granularity)
	db 0x00 ; upper 8bit of base address
	
	; selector 0x10: data segment (base 0x00000000, limit 0xFFFFFFFF)
	dw 0xFFFF
	db 0x00, 0x00, 0x00
	db 0x92 ; access type: data
	db 0xCF
	db 0x00
	
	; selector 0x18: tss (not used)
	dw 0xFFFF
	db 0x00, 0x00, 0x00
	db 0x89
	db 0xCF
	db 0x00
	
	
gdt_end:




	nop
	nop
	nop
	nop
	nop
	nop
	nop
	