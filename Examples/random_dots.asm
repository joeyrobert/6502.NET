; random_dots.asm - paints random pixels in random colours.
; $FE returns a random byte every time it is read.
        .org $0600
loop:   lda $fe
        sta $10         ; low address byte
        lda $fe
        and #$03
        clc
        adc #$02        ; high byte $02-$05 -> screen page
        sta $11
        lda $fe
        ldy #0
        sta ($10),y     ; store colour through the zero page pointer
        jmp loop
