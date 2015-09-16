
CFLAGS = -Os -std=c99 -march=i686 -ffreestanding -masm=intel -m32 -I.
AFLAGS = -a x86 -f coff -g null -m x86
LFLAGS = -Tlink.ld -LC:\Developers\lib
OFLAGS = -O binary
DFLAGS = -S -D -b binary -mi386:intel -Maddr32,data32,intel

OUTDIR = .\bin
ODIR = .\o
DEPS = asciiart.h hardware\x86\io.h hardware\x86\interrupt.h hardware\teletype.h hardware\x86\apic.h system\threading.h

BOOTSECTOR_OBJ = bootsector.o
BOOTLOADER_OBJ = bootloader.o bootloader_start.o hardware\x86\isr.o hardware\x86\interrupt.o hardware\teletype.o hardware\x86\apic.o system\threading.o

BOOTSECTOR_BIN = bootsector
BOOTLOADER_BIN = bootloader




makeall: $(BOOTSECTOR_BIN).bin $(BOOTLOADER_BIN).bin $(BOOTSECTOR_BIN).dump $(BOOTLOADER_BIN).dump program

program:
	BootCopy



%.o: %.asm $(DEPS)
	yasm -o $@ $< $(AFLAGS)

%.o: %.c $(DEPS)
	gcc -c -o $@ $< $(CFLAGS)

%.dump: %.bin
	objdump --disassemble "$(OUTDIR)\$<" > "$(OUTDIR)\$@" $(DFLAGS)

%.bin: %.out
	objcopy $(OUTDIR)\$< $(OUTDIR)\$@ $(OFLAGS)




$(BOOTSECTOR_BIN).out: $(BOOTSECTOR_OBJ)
	ld -o $(OUTDIR)\$@ $^ $(LFLAGS)

$(BOOTLOADER_BIN).out: $(BOOTLOADER_OBJ)
	ld -o $(OUTDIR)\$@ $^ $(LFLAGS)


