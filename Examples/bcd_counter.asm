; bcd_counter.asm - counts to 99 in decimal mode (SED) and stores each step at $0200.
; Ends with $99 at $0200+99 and the accumulator wrapping to $00 with carry set.
        .org $0600
        sed
        lda #0
        ldx #0
loop:   clc
        adc #1
        inx
        sta $0200,x     ; shows up as pixels
        cpx #99
        bne loop
        clc
        adc #1          ; 99 + 1 = 00, carry set
        cld
        brk
