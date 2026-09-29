; gradient.asm - draws a diagonal colour gradient then halts.
        .org $0600
        ldx #0          ; row
row:    ldy #0          ; column
col:    tya
        clc
        stx $00
        adc $00         ; colour = row + column
        sta $01
        ; address = $0200 + row*32 + column
        txa
        lsr a
        lsr a
        lsr a
        clc
        adc #$02
        sta $03         ; high byte
        txa
        asl a
        asl a
        asl a
        asl a
        asl a
        sta $02         ; low byte = (row << 5) & $FF
        tya
        clc
        adc $02
        sta $02
        lda $03
        adc #0
        sta $03
        lda $01
        sty $04
        ldy #0
        sta ($02),y
        ldy $04
        iny
        cpy #32
        bne col
        inx
        cpx #32
        bne row
        brk
