#*******************************************************
#
# Makefile for bare x86 architecture
#
# The gcc cross compiler "x86-none-elf" must be installed.
#
# created: 15.08.15
#
#*******************************************************

ifndef FRAMEWORK
  $(error this Makefile cannot be run directly, use the universal framework makefile instead)
endif


BIN_TOOLS:=/c/developer
CC:=$(BIN_TOOLS)/x86_64-none-elf-gcc
OBJCOPY:=$(BIN_TOOLS)/x86_64-none-elf-objcopy.exe
OBJDUMP:=$(BIN_TOOLS)/x86_64-none-elf-objdump.exe
SIZE:=$(BIN_TOOLS)/x86_64-none-elf-size.exe

INCLUDES:=. $(FRAMEWORK) $(FRAMEWORK)/stdlib
LINKER_SCRIPT:=$(FRAMEWORK)/arch/$(ARCH)/link.ld

# C flags:
#    optimize for size
#    arch: x86_64 (implies availability of SSE and SSE2)
#    memory model: negative 2GB
#    no standard library
CFLAGS:=-std=c99 -O2 \
 -ffreestanding -mcmodel=kernel -fno-pic \
 -fshort-enums -fshort-wchar \
 -Wall -Werror -Wpedantic -Werror-implicit-function-declaration -Werror=implicit-int \
 -fno-common -pipe -g -m64 -masm=intel \
 -mno-mmx -mno-avx -mno-3dnow -msse -msse2 -mfpmath=sse -msseregparm -mno-fp-ret-in-387 \
 -nostdlib -T$(QUOTE)$(LINKER_SCRIPT)$(QUOTE) -lgcc \
 $(MACRO_ARGS)



# add architecture specific source files
SRC+=$(addprefix $(FRAMEWORK)/arch/$(ARCH)/,			\
	apic.c							\
	interrupts.c						\
	mmu.c							\
	realmode.c						\
	vmx.c							\
	)



CFLAGS += $(addprefix -I$(QUOTE),$(addsuffix $(QUOTE),$(INCLUDES)))
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

# runtime files
CRT_SRC = $(FRAMEWORK)/arch/$(ARCH)/crt.c
CRT0_OBJ = $(FRAMEWORK)/arch/$(ARCH)/crt0.o
CRTI_OBJ = $(FRAMEWORK)/arch/$(ARCH)/crti.o
CRTBEGIN_OBJ := $(shell $(CC) $(CFLAGS) -print-file-name=crtbegin.o)
CRTEND_OBJ := $(shell $(CC) $(CFLAGS) -print-file-name=crtend.o)
CRTN_OBJ = $(FRAMEWORK)/arch/$(ARCH)/crtn.o
CRT_OBJS = $(CRT0_OBJ) $(CRTI_OBJ) $(CRTN_OBJ)

# build rules for architecture specific runtime wrappers
$(CRTI_OBJ): $(CRT_SRC)
	"$(CC)" -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss) -DMAKE_CRTI=1
$(CRTN_OBJ): $(CRT_SRC)
	"$(CC)" -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss) -DMAKE_CRTN=1
$(CRT0_OBJ): $(CRT_SRC)
	"$(CC)" -c $(CFLAGS) $(CRT_SRC) -o $@ -Wa,-adhlns=$(@:.o=.lss)


# Links all objects into a single binary and generates all output files
BINARY_EXPORT := -R .bootsect -R .eeprom -R .fuse -R .lock -R .signature --set-section-flags=.extended_text=alloc,load "$(OUTDIR)/$(OUTPUT).elf"
$(OUTDIR)/$(OUTPUT).elf: $(OBJ) $(CRT_OBJS) $(LINKER_SCRIPT)
	"$(CC)" $(CFLAGS) -o"$(OUTDIR)/$(OUTPUT).elf" $(CRT0_OBJ) $(CRTI_OBJ) $(CRTBEGIN_OBJ) $(OBJ) $(CRTEND_OBJ) $(CRTN_OBJ) $(LIBS) -Wl,-Map="$(OUTDIR)/$(OUTPUT).map"
	"$(OBJCOPY)" $(BINARY_EXPORT) -O ihex "$(OUTDIR)/$(OUTPUT).hex"
	"$(OBJCOPY)" $(BINARY_EXPORT) -O binary "$(OUTDIR)/$(OUTPUT).bin"
	"$(OBJCOPY)" -j .bootsect --set-section-flags=.eeprom=alloc,load -O binary "$(OUTDIR)/$(OUTPUT).elf" "$(OUTDIR)/bootloader.bin"
	"$(OBJDUMP)" -h -S "$(OUTDIR)/$(OUTPUT).elf" > "$(OUTDIR)/$(OUTPUT).lss"
	"$(OBJCOPY)" $(BINARY_EXPORT) -O srec "$(OUTDIR)/$(OUTPUT).srec"



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
