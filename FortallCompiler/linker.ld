ENTRY(__start)

SECTIONS
{
    .text 0x00400000: {
        *(.text)
    }

    .data 0x10010000: {
        *(.data)
    }

    . = ALIGN(4K);
    _end = .;
}
