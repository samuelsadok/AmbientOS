#*******************************************************
#
# Makefile for the AVR microcontrollers
#
# Atmel Studio 6 must be installed.
#
# created: 21.02.15
#
#*******************************************************

ifndef FRAMEWORK
  $(error this Makefile cannot be run directly, use the universal framework makefile instead)
endif


BIN_TOOLS:=C:\Program Files (x86)\Atmel\Atmel Toolchain\AVR8 GCC\Native\3.4.2.1002\avr8-gnu-toolchain\bin
CC:=$(BIN_TOOLS)\avr-gcc.exe
OBJCOPY:=$(BIN_TOOLS)\avr-objcopy.exe
OBJDUMP:=$(BIN_TOOLS)\avr-objdump.exe
SIZE:=$(BIN_TOOLS)\avr-size.exe

INCLUDES:=. $(FRAMEWORK)
LINKER_SCRIPT:=$(FRAMEWORK)/arch/$(ARCH)/link.ld

CFLAGS:=-Os -funsigned-char -funsigned-bitfields \
 -ffunction-sections -fdata-sections -fpack-struct \
 -fshort-enums -g2 -Wall -Werror -std=gnu99 \
 -Wa,-g -Wa,-gdwarf2 \
 -Wl,--start-group -Wl,-lm -Wl,--end-group -Wl,--gc-sections \
 -mmcu=$(shell echo $(MCU) | tr A-Z a-z) \
 $(MACRO_ARGS)



# add architecture specific source files
SRC+=$(addprefix $(FRAMEWORK)/arch/$(ARCH)/,			\
	avr_io.c						\
	bootloader.c						\
	i2c_master.c						\
	i2c_slave.c						\
	interrupt.c						\
	math.S							\
	timer.c							\
	)



# INCLUDE MAKEFILE GENERATED FROM PROJECT
#avrprojdump="../../../../Code/C\#/avrprojdump/bin/debug/avrprojdump.exe"
#PROJ_FILE = ./$(OUTPUT).cproj
#PROJ_MAKEFILE = ./bin/Makefile
#PROJ_MAKEFILE_SCRIPT = $(FRAMEWORK)/arch/$(ARCH)/avrproj2makefile.xml
#$(PROJ_MAKEFILE):
#	$(avrprojdump) -i "$(PROJ_FILE)" -s "$(PROJ_MAKEFILE_SCRIPT)" -o "$(PROJ_MAKEFILE)" Configuration=Release
#include $(PROJ_MAKEFILE)



ASF=$(FRAMEWORK)/../ASF/

# add source files from Atmel Software Framework
ASF_SRC:= \
		common/services/ioport/xmega/ioport_compat.c 			\
		common/services/clock/xmega/sysclk.c 				\
		common/services/sleepmgr/xmega/sleepmgr.c 			\
		xmega/drivers/cpu/ccp.s 					\
		xmega/drivers/crc/crc.c 					\
		xmega/drivers/rtc/rtc.c 					\
		xmega/drivers/wdt/wdt.c 					\
		$(call forFeature,NVM,xmega/drivers/nvm/nvm.c) 			\
		$(call forFeature,NVM,xmega/drivers/nvm/nvm_asm.s)		\
		$(call forFeature,BUILTIN_TIMERS,xmega/drivers/tc45/tc45.c)	\
		$(call forFeature,I2C_MASTER,xmega/drivers/twi/twim.c) 		\
		)

SRC+=$(addprefix $(ASF),$(ASF_SRC))

INCLUDES+=$(ASF) $(addprefix $(ASF), \
		config				\
		common/services/ioport 		\
                common/boards 			\
                common/utils 			\
                common/services/cpu 		\
                common/services/clock 		\
                common/services/sleepmgr 	\
                xmega/utils/preprocessor 	\
                xmega/utils 			\
                xmega/drivers/cpu 		\
                xmega/drivers/crc 		\
                xmega/drivers/pmic 		\
                xmega/drivers/rtc 		\
                xmega/drivers/sleep 		\
                xmega/drivers/wdt 		\
                $(call forFeature,NVM,xmega/drivers/nvm)		\
                $(call forFeature,BUILTIN_TIMERS,xmega/drivers/tc45)	\
                xmega/drivers/twi 		\
		)

CFLAGS += -DBOARD=99



CFLAGS += $(addprefix -I$(QUOTE),$(addsuffix $(QUOTE),$(INCLUDES)))
ifdef BOOTLOADER_OFFSET
  CFLAGS += -Wl,--section-start=.text=$(BOOTLOADER_SECTION) -Wl,--section-start=.extended_text=$(BOOTLOADER_OFFSET)
  CFLAGS += -DBOOT=text
  CFLAGS += -DEXTENDED_BOOTSECTION_SIZE=$(BOOTLOADER_SECTION)-$(BOOTLOADER_OFFSET)
endif
OBJ := $(call toObjPath,$(filter $(addprefix %,$(SRC_EXTENSIONS)),$(SRC)))


# Creates an object file and a dependency file from any compatible source code file
%.o: $(call toSrcPath,$(@))
	@echo object: $(@) source: $(call toSrcPath,$(@))
	"$(CC)" -c $(CFLAGS) $(call getLang,$(@)) -MD -MP -MF "$(@:%.o=%.d)" -MT"$(@:%.o=%.d)" -MT"$(@:%.o=%.o)" -o "$@" "$(call toSrcPath,$(@))"
	$(call repairDep,$(@:%.o=%.d))

# Creates a preprocessed version from any compatible source code file
%.pre.c: $(call toSrcPath,$(@))
	@echo object: $(@) source: $(call toSrcPath,$(@:%.pre.c=%.o))
	"$(CC)" -E $(CFLAGS) $(call getLang,$(@:%.pre.c=%.o)) -MD -MP -MF "$(@:%.pre.c=%.d)" -MT"$(@:%.pre.c=%.d)" -MT"$(@:%.pre.c=%.o)" -o "$@" "$(call toSrcPath,$(@:%.pre.c=%.o))"
	$(call repairDep,$(@:%.pre.c=%.d))


# Links all objects into a single binary and generates all output files
FLASH_EXPORT := -R .eeprom -R .fuse -R .lock -R .signature --set-section-flags=.extended_text=alloc,load "$(OUTDIR)/$(OUTPUT).elf"
$(OUTDIR)/$(OUTPUT).elf: $(OBJ) $(LINKER_SCRIPT)
	"$(CC)" $(CFLAGS) -o"$(OUTDIR)/$(OUTPUT).elf" $(OBJ) $(LIBS) -Wl,-Map="$(OUTDIR)/$(OUTPUT).map"
	"$(OBJCOPY)" $(FLASH_EXPORT) -O ihex "$(OUTDIR)/$(OUTPUT).hex"
	"$(OBJCOPY)" -j .eeprom --set-section-flags=.eeprom=alloc,load --change-section-lma .eeprom=0 --no-change-warnings -O ihex "$(OUTDIR)/$(OUTPUT).elf" "$(OUTDIR)/$(OUTPUT).eep"
	"$(OBJDUMP)" -h -S "$(OUTDIR)/$(OUTPUT).elf" > "$(OUTDIR)/$(OUTPUT).lss"
	"$(OBJCOPY)" $(FLASH_EXPORT) -O srec "$(OUTDIR)/$(OUTPUT).srec"



.PHONY: all clean %.clean preprocess FORCE

# creates a preprocessed version of each source code file
preprocess: $(OBJ:%.o=%.pre.c)
	@echo preprocessing complete

# creates a firmware binary from the source code
all: $(OUTDIR) $(OUTDIR)/$(OUTPUT).elf
	@echo
	@"$(SIZE)" "$(OUTDIR)/$(OUTPUT).elf"
	@echo compilation complete!

info:
	@echo objects: $(OBJ)
	@echo src: $(call toSrcPath,$(OBJ))
	@echo dependencies: $(wildcard $(OBJ:%.o=%.d))

clean: #$(OBJ:.o=.o.clean)
	rm -rf $(addprefix $(QUOTE)$(OUTDIR)/$(OUTPUT),$(addsuffix $(QUOTE),.elf .hex .srec .eep .map .lss))
	rm -rf $(OBJ)
	rm -rf $(OBJ:%.o=%.d)
	rm -rf $(OBJ:%.o=%.pre.c)

%.clean:
	rm -rf "$(@:%.clean=%)"

FORCE:


$(wildcard $(OBJ:%.o=%.d)):
	@echo dep $(@)
include $(wildcard $(OBJ:%.o=%.d))
