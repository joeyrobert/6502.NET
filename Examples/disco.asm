; disco.asm - flashes the whole 32x32 screen through the 16 colours.
; The screen lives at $0200-$05FF; each byte's low nibble is its colour.
        .org $0600
loop:   inx
        txa
        sta $0200,y     ; fill each of the four screen pages
        sta $0300,y
        sta $0400,y
        sta $0500,y
        iny
        jmp loop
