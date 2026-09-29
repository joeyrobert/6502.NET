; sierpinski.asm - Sierpinski triangle: a pixel is lit where (x AND y) == 0.
; Lit pixels are coloured in vertical bands. Draws once, then halts.
        .org $0600
        lda #$00
        sta $02         ; $02/$03 = running screen pointer, starts at $0200
        lda #$02
        sta $03
        ldy #0          ; y
row:    ldx #0          ; x
col:    stx $04
        tya
        and $04
        beq lit
        lda #0          ; unlit: black
        jmp put
lit:    tya
        lsr a
        lsr a
        clc
        adc #2          ; colours 2-9 by rows of 4
put:    sty $05
        ldy #0
        sta ($02),y
        ldy $05
        inc $02         ; advance pointer, carry into the high byte
        bne nocarry
        inc $03
nocarry:
        inx
        cpx #32
        bne col
        iny
        cpy #32
        bne row
        brk
