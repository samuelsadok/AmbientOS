

; author: James Molloy - james<at>jamesmolloy.co.uk

[GLOBAL _lidt]    ; Allows the C code to call idt_flush().
[GLOBAL _sidt]    ; Allows the C code to call idt_flush().

_lidt:
   lidt [idtr]        ; Load the IDT pointer.
   ret
   
_sidt:
   sidt [idtr]        ; Load the IDT pointer.
   mov eax, [idtr+2]
   ret
   

[GLOBAL _fancy_delay]
_fancy_delay:
	mov eax, [esp+4]

.loop:
	dec eax
	jnz .loop
	
	ret



[EXTERN _cpu_isr_common_stub]
[EXTERN _apic_isr_common_stub]
[EXTERN _scheduler_isr]



; Handler for a CPU interrupt that pushes no error code
%macro ISR_NOERRCODE 1  ; define a macro, taking one parameter (%1 accesses the first parameter)
  dd next_isr%1
  dd %1
    push byte 0
    push byte %1
    jmp 0x08:_cpu_isr_common_stub
  next_isr%1
%endmacro

; Handler for a CPU exception that pushes an error code
%macro ISR_ERRCODE 1
  dd next_isr%1
  dd %1
    push byte %1
    jmp 0x08:_cpu_isr_common_stub
  next_isr%1
%endmacro

; Handler for a local APIC interrupt
%macro ISR_APIC 1
  dd next_isr%1
  dd %1
    push byte 0
    push %1
    jmp 0x08:_apic_isr_common_stub
  next_isr%1
%endmacro

; Handler for an APIC interrupt that should be ignored without an EOI message 
%macro ISR_DEAD 1
  dd next_isr%1
  dd %1
    iret
  next_isr%1
%endmacro

; Handler for a timer interrupt
%macro ISR_SCHEDULER 1
  dd next_isr%1
  dd %1
    jmp 0x08:_scheduler_isr
  next_isr%1
%endmacro



; the linked list allows the compiler to automatically load all ISRs
[GLOBAL _isr_linked_list]
_isr_linked_list:


; CPU EXCEPTIONS / INTERRUPTS
ISR_NOERRCODE 0x00
ISR_NOERRCODE 0x01
ISR_NOERRCODE 0x02
ISR_NOERRCODE 0x03
ISR_NOERRCODE 0x04
ISR_NOERRCODE 0x05
ISR_NOERRCODE 0x06
ISR_NOERRCODE 0x07
ISR_ERRCODE   0x08
ISR_NOERRCODE 0x09
ISR_ERRCODE   0x0A
ISR_ERRCODE   0x0B
ISR_ERRCODE   0x0C
ISR_ERRCODE   0x0D
ISR_ERRCODE   0x0E
ISR_NOERRCODE 0x0F
ISR_NOERRCODE 0x10
ISR_NOERRCODE 0x11
ISR_NOERRCODE 0x12
ISR_NOERRCODE 0x13
ISR_NOERRCODE 0x14
ISR_NOERRCODE 0x15
ISR_NOERRCODE 0x16
ISR_NOERRCODE 0x17
ISR_NOERRCODE 0x18
ISR_NOERRCODE 0x19
ISR_NOERRCODE 0x1A
ISR_NOERRCODE 0x1B
ISR_NOERRCODE 0x1C
ISR_NOERRCODE 0x1D
ISR_NOERRCODE 0x1E
ISR_NOERRCODE 0x1F


; LOCAL APIC INTERRUPTS
ISR_APIC 0xF0
ISR_SCHEDULER 0xF1
ISR_APIC 0xF2
ISR_APIC 0xF3
ISR_APIC 0xF4
ISR_APIC 0xF5
ISR_APIC 0xF6
ISR_APIC 0xF7
ISR_APIC 0xF8
ISR_APIC 0xF9
ISR_APIC 0xFA
ISR_APIC 0xFB
ISR_APIC 0xFC
ISR_APIC 0xFD
ISR_APIC 0xFE ; APIC error interrupt
ISR_DEAD 0xFF ; spurious interrupt


dd 0 ; mark end of linked list



; the actual IDT is built by C code
[EXTERN _interrupt_descriptor_table]

idtr:
	dw 0x7FF
	dd _interrupt_descriptor_table



