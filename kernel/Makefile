#*******************************************************
#
# Kernel Makefile
#
# Builds the kernel for x86 PCs (todo: extend to ARM)
#
# created: 24.02.15
#
#*******************************************************


# application name
OUTPUT = FlightController
APPLICATION_NAME = OEM_Flight_Controller

# select hardware
PLATFORM := IBM_PC
PLATFORM_NAME := BARE_PC
ARCH := avr


# select required features
FEATURES = 			\
	THREADING


# include application specific source files
SRC = main.c


# let the framework control the actual build process
FRAMEWORK = ./../shared-c/
include $(FRAMEWORK)/Makefile



CC = x86_64-none-elf-gcc
OBJCOPY = x86_64-none-elf-objcopy
OBJDUMP = x86_64-none-elf-objdump

# c flags:
#    arch: x86_64 (implies availability of SSE and SSE2)
#    device: IBM-PC
#    memory model: negative 2GB
#    no standard library
CFLAGS = -std=c99 -O2 -ffreestanding -mcmodel=kernel -fno-pic -D__IBM_PC__=1 -Tlink.ld -I. -I$(SRCDIR) -I$(SRCDIR)/stdlib -nostdlib -lgcc -fno-common -pipe -g -fshort-wchar -m64 -masm=intel -mno-mmx -mno-avx -mno-3dnow -msse -msse2 -mfpmath=sse -msseregparm -mno-fp-ret-in-387 -Wall -Wpedantic -Werror-implicit-function-declaration -Werror=implicit-int
OFLAGS = -O binary

SRCDIR = ../shared
OUTDIR = ./bin
ARCH = x86

# list all source files here, each C source file must have a header file with the same name
KERNEL_SOURCE = kernel.c heap.c memory.c ntfs.c threading.c
SYSTEM_SOURCE = bitmap.c debug.c drivers.c filesystem.c log.c unicode.c
STDLIB_SOURCE = execinfo.c
ARCH_SOURCE = apic.c interrupts.c mmu.c realmode.c
HARDWARE_SOURCE = IBM-PC/bootloader.S IBM-PC/biosdisk.c IBM-PC/graphics.c IBM-PC/teletype.c

COMMON_SRC = $(addprefix $(SRCDIR)/kernel/, $(KERNEL_SOURCE)) $(addprefix $(SRCDIR)/system/, $(SYSTEM_SOURCE)) $(addprefix $(SRCDIR)/stdlib/, $(STDLIB_SOURCE)) $(addprefix $(SRCDIR)/arch/$(ARCH)/, $(ARCH_SOURCE)) $(addprefix $(SRCDIR)/hardware/, $(HARDWARE_SOURCE))
COMMON_HDR_C = $(COMMON_SRC:.c=.h)
COMMON_HDR = $(COMMON_HDR_C:.S=.h)

SOURCES = main.c $(COMMON_SRC)
HEADERS = config.h $(SRCDIR)/system.h $(SRCDIR)/arch/io.h $(SRCDIR)/arch/$(ARCH)/io.h $(SRCDIR)/stdlib/stdlib.h $(SRCDIR)/hardware/IBM-PC/device.h $(COMMON_HDR)
ASM_OBJS = $(SOURCES:.S=.S.o)
OBJECTS = $(ASM_OBJS:.c=.c.o)
DEPS = link.ld $(HEADERS)


BOOTLOADER_BIN = bootloader.bin
KERNEL_BIN = kernel.bin



CRT_SRC = $(SRCDIR)/arch/$(ARCH)/crt.c
CRT0_OBJ = $(SRCDIR)/arch/$(ARCH)/crt0.o
CRTI_OBJ = $(SRCDIR)/arch/$(ARCH)/crti.o
CRTBEGIN_OBJ := $(shell $(CC) $(CFLAGS) -print-file-name=crtbegin.o)
CRTEND_OBJ := $(shell $(CC) $(CFLAGS) -print-file-name=crtend.o)
CRTN_OBJ = $(SRCDIR)/arch/$(ARCH)/crtn.o
CRT_OBJS = $(CRT0_OBJ) $(CRTI_OBJ) $(CRTN_OBJ)


IMAGE = ntfs.bin
BOCHS_CONFIG = bochsrc.bxrc
BOCHS = "/c/Program Files (x86)/Bochs-2.6.7/bochsdbg.exe"


# builds all binaries
all: $(KERNEL_BIN) $(BOOTLOADER_BIN)


# removes all object files, list files and binaries
clean: $(OBJECTS:.o=.o.clean) $(OBJECTS:.o=.lss.clean) $(CRT_OBJS:.o=.o.clean) $(CRT_OBJS:.o=.lss.clean) $(OUTDIR)/$(BOOTLOADER_BIN:.bin=.elf.clean) $(OUTDIR)/$(BOOTLOADER_BIN:.bin=.bin.clean) $(OUTDIR)/$(BOOTLOADER_BIN:.bin=.lss.clean) $(OUTDIR)/$(KERNEL_BIN:.bin=.elf.clean) $(OUTDIR)/$(KERNEL_BIN:.bin=.bin.clean) $(OUTDIR)/$(KERNEL_BIN:.bin=.lss.clean)



# build rules for architecture specific runtime wrappers
%/crti.o: %/crt.c $(DEPS)
	$(CC) -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss) -DMAKE_CRTI=1
%/crtn.o: %/crt.c $(DEPS)
	$(CC) -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss) -DMAKE_CRTN=1
%/crt0.o: %/crt.c $(DEPS)
	$(CC) -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss)


# build rules to make object files from C or ASM source
%.c.o: %.c $(DEPS)
	$(CC) -c $(CFLAGS) $< -o $@ -Wa,-adhlns=$(<:.c=.c.lss)
%.S.o: %.S $(DEPS)
	$(CC) -c $(CFLAGS) $< -o $@ -Wa,-adhlns=$(<:.S=.S.lss)


%.clean:
	rm -f $(@:.clean=)



# kernel.elf contains both bootsector and kernel code
$(KERNEL_BIN:.bin=.elf): $(OUTDIR) $(OBJECTS) $(CRT_OBJS) $(DEPS)
	$(CC) $(CFLAGS) $(CRT0_OBJ) $(CRTI_OBJ) $(CRTBEGIN_OBJ) $(OBJECTS) $(CRTEND_OBJ) $(CRTN_OBJ) -o $(OUTDIR)/$(KERNEL_BIN:.bin=.elf)
	$(OBJDUMP) -D $(OUTDIR)/$(KERNEL_BIN:.bin=.elf) > $(OUTDIR)/$(KERNEL_BIN:.bin=.lss)

$(KERNEL_BIN): $(KERNEL_BIN:.bin=.elf)
	$(OBJCOPY) --remove-section .bootsect $(OUTDIR)/$(KERNEL_BIN:.bin=.elf) $(OUTDIR)/$(KERNEL_BIN) $(OFLAGS)

$(BOOTLOADER_BIN): $(KERNEL_BIN:.bin=.elf)
	$(OBJCOPY) --set-section-flags .bootsect=alloc --only-section .bootsect $(OUTDIR)/$(KERNEL_BIN:.bin=.elf) $(OUTDIR)/$(BOOTLOADER_BIN) $(OFLAGS)
	$(OBJDUMP) --section .bootsect --adjust-vma=32256 -mi386 -Maddr16,data16 -D $(OUTDIR)/$(KERNEL_BIN:.bin=.elf) > $(OUTDIR)/$(BOOTLOADER_BIN:.bin=.lss)




# installs the bootloader and kernel in the test system (current kernel size limit: 128kB)

install: all
	dd if=$(OUTDIR)/$(BOOTLOADER_BIN) of=C:/Developer/system.vhd bs=1 skip=32 seek=1519648 conv=notrunc
	dd if=$(OUTDIR)/$(KERNEL_BIN) of=C:/Developer/system.vhd bs=1 seek=1536000 conv=notrunc



# attaches the test system VHD for manual file operations
attach:
	diskpart -s vhdAttach.txt

# detaches the test system VHD for direct access
detach:
	diskpart -s vhdDetach.txt





# compiles and installs the bootloader and kernel and launches the test system in bochs in debugging mode

debug: install
	$(BOCHS) -q -f $(BOCHS_CONFIG)


