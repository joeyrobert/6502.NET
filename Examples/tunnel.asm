; tunnel.asm - animated colour tunnel: concentric square rings whose colours cycle every frame.
; ring = min(x, y, 31-x, 31-y); colour = ring + frame.
        .org $0600
frame = $06
        lda #0
        sta frame
next:   lda #$00
        sta $02         ; running screen pointer
        lda #$02
        sta $03
        ldy #0
row:    ldx #0
col:    stx $04
        sty $05
        stx $07         ; m = x
        cpy $07
        bcs s1
        sty $07         ; y < m
s1:     lda #31
        sec
        sbc $04         ; 31 - x
        cmp $07
        bcs s2
        sta $07
s2:     lda #31
        sec
        sbc $05         ; 31 - y
        cmp $07
        bcs s3
        sta $07
s3:     lda $07
        clc
        adc frame
        ldy #0
        sta ($02),y
        ldy $05
        inc $02
        bne nocarry
        inc $03
nocarry:
        inx
        cpx #32
        bne col
        iny
        cpy #32
        bne row
        inc frame
        jmp next
