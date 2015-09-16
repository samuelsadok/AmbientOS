ENTRY(__start)
SECTIONS
{
    . = 0;

    .bootsect :
    {
        bootsect = .;
        _bootsect = .;
        __bootsect = .;
        *(.bootsect)		/* bootloader */
        . = ALIGN(4096);
    }

    . = 0xFFFFFFFF80100000;

    .text :
    {
        code = .;
        _code = .;
        __code = .;

        *(.start)		/* initialization code */
	*(.init)
	*(.fini)

        *(.text)		/* program code */
        *(.rodata)		/* read-only data */

        . = ALIGN(4096);
    }

    .data :
    {
        data = .;
        _data = .;
        __data = .;
        *(.data) 		/* read-write data */

	*(.ctor*)		/* various lists used by initialization code */
	*(.dtor*)
	*(.init0)
	*(.init1)
	*(.init2)
	*(.init3)
	*(.init4)
	*(.init5)
	*(.bootref)

        . = ALIGN(4096);
    }

    .bss :
    {
        bss = .;
        _bss = .;
        __bss = .;
        *(.bss)			/* zero-initialized read-write data */
        . = ALIGN(4);
	__bss_end = .;
        . = ALIGN(4096);
    }

    end = .;
    _end = .;
    __end = .;
}