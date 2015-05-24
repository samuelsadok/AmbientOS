#*******************************************************
#
# Makefile for Windows
#
# MinGW must be installed.
#
# created: 31.03.15
#
#*******************************************************

ifndef FRAMEWORK
  $(error this Makefile cannot be run directly, use the universal framework makefile instead)
endif


CC:=gcc.exe
OBJCOPY:=objcopy.exe
OBJDUMP:=objdump.exe
SIZE:=size.exe

INCLUDES:=. $(FRAMEWORK)

CFLAGS:=-Os -funsigned-char -funsigned-bitfields \
 -ffunction-sections -fdata-sections -fpack-struct \
 -fshort-enums -g2 -Wall -Werror -std=gnu99 \
 -Wa,-g -Wa,-gdwarf2 \
 -Wl,--start-group -Wl,-lm -Wl,--end-group -Wl,--gc-sections \
 $(MACRO_ARGS)



# add architecture specific source files
SRC+=$(addprefix $(FRAMEWORK)/platform/$(PLATFORM)/,		\
	math.c							\
	nvm.c							\
	stdio.c							\
	timer.c							\
	windows.c						\
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


# Links all objects into a single binary and generates all output files
$(OUTDIR)/$(OUTPUT).exe: $(OBJ) $(LINKER_SCRIPT)
	"$(CC)" $(CFLAGS) -o"$(OUTDIR)/$(OUTPUT).exe" $(OBJ) $(LIBS) -Wl,-Map="$(OUTDIR)/$(OUTPUT).map"
	"$(OBJDUMP)" -h -S "$(OUTDIR)/$(OUTPUT).exe" > "$(OUTDIR)/$(OUTPUT).lss"



.PHONY: all clean %.clean preprocess FORCE

# creates a preprocessed version of each source code file
preprocess: $(OBJ:%.o=%.pre.c)
	@echo preprocessing complete

# creates a firmware binary from the source code
all: $(OUTDIR) $(OUTDIR)/$(OUTPUT).exe
	@echo
	@"$(SIZE)" "$(OUTDIR)/$(OUTPUT).exe"
	@echo compilation complete!

info:
	@echo objects: $(OBJ)
	@echo src: $(call toSrcPath,$(OBJ))
	@echo dependencies: $(wildcard $(OBJ:%.o=%.d))

clean:
	rm -rf $(addprefix $(QUOTE)$(OUTDIR)/$(OUTPUT),$(addsuffix $(QUOTE),.exe .map .lss))
	rm -rf $(OBJ)
	rm -rf $(OBJ:%.o=%.d)
	rm -rf $(OBJ:%.o=%.pre.c)

%.clean:
	rm -rf "$(@:%.clean=%)"

FORCE:


$(wildcard $(OBJ:%.o=%.d)):
	@echo dep $(@)
include $(wildcard $(OBJ:%.o=%.d))
