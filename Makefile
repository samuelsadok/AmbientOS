#*******************************************************
#
# Universal Framework Makefile
#
# Controls the framework build process
#
# This file is to be included by the application makefile.
# The application makefile shall define the following variables:
#   OUTPUT: name of the application (output will be named after this)
#   FRAMEWORK: path to this file
#   SRC: application specific source files
#   ARCH: the target architecture
#   PLATFORM: the target platform
#     __[PLATFORM]__ will be passed as a macro to the compiler
#   FEATURES: a list of features used by the application
#     USING_[FEATURE] will be passed as a macro for every feature
#
# created: 21.02.15
#
#*******************************************************


.DEFAULT_GOAL := all

QUOTE:="


# add system sources
SRC += $(addprefix $(FRAMEWORK)/kernel/,			\
	kernel.c)

SRC += $(addprefix $(FRAMEWORK)/system/,			\
	build.c							\
	dfu.c							\
	time.c							\
	log.c							\
	math.c							\
	)

# add hardware sources
SRC += $(addprefix $(FRAMEWORK)/hardware/,			\
	mpu/invensense_mpu.c					\
	nvm.c							\
	power.c							\
	pwmmotor.c						\
	soft_i2c_master.c					\
	i2c_eeprom.c						\
	)

# add services
SRC += $(addprefix $(FRAMEWORK)/services/,			\
	gatt_service.c						\
	gap_service.c						\
	i2c_service.c						\
	dfu_service.c						\
	)


# add simulation code
SRC += $(call forFeature,SIMULATION,$(addprefix $(FRAMEWORK)/simulation/,	\
	sim_endpoint.c						\
	virtual_motor.c						\
	virtual_mpu.c						\
	))


# add special sources
SRC += $(addprefix $(FRAMEWORK)/uav/,				\
	local_uav.c						\
	)



# a list of all file types that are to be compiled
SRC_EXTENSIONS=.c .cpp .s .S


# configure output directory
ifndef OUTDIR
OUTDIR:=./bin
endif

$(OUTDIR):
	mkdir $(QUOTE)$(OUTDIR)$(QUOTE)


# converts one or multiple source paths into object paths
#   e.g. ./../../shared/system/log.c => .bin/_system#log.c.o
toObjPath = $(addprefix $(OUTDIR)/,$(subst \#\#,\#,$(subst /,\#,$(subst ./,,$(subst ../,§,$(patsubst $(FRAMEWORK)/%,_%,$(1:%=%.o)))))))

# converts one or multiple object paths into source paths
#   e.g. .bin/_system#log.c.o => ./../../shared/system/log.c
toSrcPath = $(addprefix ./,$(patsubst _%,$(FRAMEWORK)/%,$(subst §,../,$(subst \#,/,$(patsubst $(patsubst ./%,%,$(OUTDIR))/%,%,$(patsubst ./%,%,$(1:%.o=%)))))))

# returns a language specifying compiler flag for the specified object file if necessary
getLang = $(if $(filter %.s.o,$(1)),-x assembler-with-cpp)

# returns the second argument if the according feature (1st argument) is enabled
forFeature = $(if $(filter $(1),$(FEATURES)),$(2))

# escapes the #-character in the specified dependency file
repairDep = sed -b -i 's/\#/\\\\\#/g' $(QUOTE)$(1)$(QUOTE)


BUILD_TIME:=$(shell /bin/date "+%Y-%m-%d_%H:%M:%S" -u)

MACROS=$(addprefix USING_, $(FEATURES))
ifdef PLATFORM
MACROS+=__$(PLATFORM)__
endif
MACRO_ARGS=$(addprefix -D, $(MACROS)) -DPLATFORM_NAME=\"$(PLATFORM_NAME)\" -DAPPLICATION_NAME=\"$(APPLICATION_NAME)\" -DBUILD_TIME=\"$(BUILD_TIME)\"


# include architecture or platform specific build process
# this controls the actual compilation
ifdef ARCH
include $(FRAMEWORK)/arch/$(ARCH)/Makefile
else
#todo: make platform to adapt to the PC where it's being compiled
PLATFORM:=windows
include $(FRAMEWORK)/platform/$(PLATFORM)/Makefile
endif