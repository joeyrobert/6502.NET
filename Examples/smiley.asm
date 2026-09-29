; smiley.asm - draws an 8x8 bitmap scaled up 4x to fill the 32x32 screen.
; Each bit of the sprite data becomes a 4x4 block: set = yellow, clear = blue.
        .org $0600
        lda #$00
        sta $02         ; running screen pointer
        lda #$02
        sta $03
        ldy #0
row:    ldx #0
col:    stx $04
        tya
        lsr a
        lsr a
        sta $05         ; sprite row = y / 4
        txa
        lsr a
        lsr a
        sta $06         ; sprite column = x / 4
        ldx $05
        lda sprite,x
        ldx $06
        beq test
shift:  asl a           ; move the wanted bit to bit 7
        dex
        bne shift
test:   and #$80
        beq blue
        lda #7          ; yellow
        jmp put
blue:   lda #6
put:    sty $07
        ldy #0
        sta ($02),y
        ldy $07
        inc $02
        bne nocarry
        inc $03
nocarry:
        ldx $04
        inx
        cpx #32
        bne col
        iny
        cpy #32
        bne row
        brk

sprite: .byte %00111100
        .byte %01111110
        .byte %11011011
        .byte %11011011
        .byte %11111111
        .byte %10111101
        .byte %11000011
        .byte %01111110
