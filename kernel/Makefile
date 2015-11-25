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
OUTPUT = kernel
APPLICATION_NAME = OEM_Kernel

# select hardware
PLATFORM := IBM_PC
PLATFORM_NAME := BARE_PC
ARCH := x86


# select required features
FEATURES = 			\
	UNICODE			\
	THREADING		\
	VIRTUAL_MEMORY		\
	DRIVERS			\
	GRAPHICS		\
	FILESYSTEM

# include application specific source files
SRC = main.c



# let the framework control the actual build process
FRAMEWORK = ./../shared-c/
include $(FRAMEWORK)/Makefile




.PHONY: install debug attach detach

BOCHS=C:\Developer\bochs-2.6.8
BOCHS_VHD=C:\Developer\system.vhd
BOCHS_CONFIG=bochsrc.bxrc

# escapes a string for use in a sed expression (not complete)
sedEscape=$(subst /,\\/,$(subst \,\\,$(1)))

# escapes a string for use in an echo expression (not correct)
echoEscape=$(subst \\,\\\\,$(1))

# installs the bootloader and kernel in the test system (current kernel size limit: 128kB)
install: all
	dd if="$(OUTDIR)/bootloader.bin" of="$(BOCHS_VHD)" bs=1 skip=32 seek=1519648 conv=notrunc
	dd if="$(OUTDIR)/kernel.bin" of="$(BOCHS_VHD)" bs=1 seek=1536000 conv=notrunc
	@echo -e "$(CCGREEN)installed in $(call echoEscape,$(BOCHS_VHD))$(CCRST)"

# compiles and installs the bootloader and kernel and launches the test system in bochs in debugging mode
debug: install
	sed -e 's/__VHD_PATH__/$(call sedEscape,$(BOCHS_VHD))/' -e 's/__BOCHS_PATH__/$(call sedEscape,$(BOCHS))/' "$(BOCHS_CONFIG)" > "$(BOCHS_CONFIG).auto.bxrc"
	@echo -e "$(CCGREEN)launching bochs...$(CCRST)"
	"$(BOCHS)\bochsdbg.exe" -q -f "$(BOCHS_CONFIG).auto.bxrc"
	rm -rf "$(BOCHS_CONFIG).auto.bxrc"

# attaches the test system VHD for manual file operations (works only on windows)
attach:
	echo -e "select vdisk file=\"$(call echoEscape,$(BOCHS_VHD))\"\r\nattach vdisk\r\nselect partition 1\r\nassign letter=v\r\n" > diskpart.auto.txt
	(diskpart -s diskpart.auto.txt && rm -rf diskpart.auto.txt && echo -e "$(CCGREEN)$(call echoEscape,$(BOCHS_VHD)) attached as V:\\ $(CCRST)") || echo -e "$(CCRED)failed to attach $(call echoEscape,$(BOCHS_VHD)) V:\\ $(CCRST)"

# detaches the test system VHD for direct access
detach:
	echo -e "select vdisk file=\"$(call echoEscape,$(BOCHS_VHD))\"\r\ndetach vdisk\r\n" > diskpart.auto.txt
	diskpart -s diskpart.auto.txt
	rm -rf diskpart.auto.txt
	@echo -e "$(CCGREEN)$(call echoEscape,$(BOCHS_VHD)) detached$(CCRST)"


