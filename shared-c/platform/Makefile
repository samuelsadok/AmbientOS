#*******************************************************
#
# This makefile includes platform dependend source files
#
#
# created: 18.09.15
#
#*******************************************************

ifndef FRAMEWORK
  $(error this Makefile cannot be run directly, use the universal framework makefile instead)
endif

ifeq ($(PLATFORM),IBM_PC)
SRC+=$(addprefix $(FRAMEWORK)/platform/IBM-PC/,				\
	bootloader.S							\
	biosdisk.c							\
	graphics.c							\
	teletype.c							\
	)
endif

# windows platform requires its own makefile
ifeq (($PLATFORM),windows)
include $(FRAMEWORK)/platform/windows/Makefile
endif