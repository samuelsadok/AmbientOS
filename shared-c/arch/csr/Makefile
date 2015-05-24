#*******************************************************
#
# Makefile for the XAP2+ architecture on CSR1010 chips
#
# The CSR uEnergy SDK must be installed in
# C:\Developer\CSR_uEnergy
# (todo: make this customizable using an environment variable)
#
# A few adjustments need to be made to the SDK build process:
#
# changes in tools/Makefile.common.rules (compiler flags):
#  - switch to c99 standard
#  - pass the macros in $(MACROS) to the compiler
#  - remove warning flags related to prototypes and declarations
#  - remove no-inline compiler flags
#
# changes in tools/Makefile.common.rules.CSR101x_A05:
#  - add bootloader section:
#      --change-section-address .bootloader+$(DATA_RAM_START)
#
# changes in tools/lib/CSR101x_A05/xap_ram_rom.ld:
#  - add bootloader section:
#      . = 0x6F00;
#      BOOTLOADER_START = .;
#      .bootloader : AT (BOOTLOADER_START + $CODE$) {
#          *(.bootloader)
#      }
#
# created: 21.02.15
#
#*******************************************************



# add architecture specific source files
SRC += $(addprefix $(FRAMEWORK)/arch/$(ARCH)/,			\
	ble.c							\
	bootloader.c						\
	csr1010.c						\
	gatt.c							\
	i2c.c							\
	nvm_raw.c						\
	temperature.c						\
	timer.c							\
	voltage.c						)

# define list of gatt database files
DBS = $(addprefix $(FRAMEWORK)/arch/$(ARCH)/database/,		\
	app_gatt_db.db						\
	dev_info_service_db.db					\
	gap_service_db.db					\
	gatt_service_db.db					\
	battery_service_db.db					\
	heart_rate_service_db.db				\
	i2c_service_db.db)


# we have to use the make that comes with the SDK
SDK = C:/Developer/CSR_uEnergy/tools
SDK_MAKE = $(SDK)/bin/make SDK=$(SDK) INPUTS="$(SRC) $(DBS)" DBS="$(DBS)" KEYR="$(FRAMEWORK)/arch/$(ARCH)/default.keyr" OUTPUT="$(OUTPUT)" MACRO_ARGS="$(subst \,\\\,$(MACRO_ARGS))" INCPATHS=$(FRAMEWORK) -f $(FRAMEWORK)/arch/$(ARCH)/CoreMakefile


all:
	$(SDK_MAKE) build

clean:
	$(SDK_MAKE) clean


# todo: add build rules for install
