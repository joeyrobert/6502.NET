; bouncing_ball.asm - a pixel bounces around the screen, changing colour on every bounce.
; $00/$01 = x/y, $02/$03 = dx/dy (1 or $FF = -1), $04 = colour counter.
        .org $0600
        lda #5
        sta $00
        lda #3
        sta $01
        lda #1
        sta $02
        sta $03
        lda #0
        sta $04
loop:   lda #0
        jsr plot        ; erase
        lda $00
        clc
        adc $02
        sta $00
        beq flipx
        cmp #31
        bne movey
flipx:  lda #0
        sec
        sbc $02
        sta $02
        inc $04
movey:  lda $01
        clc
        adc $03
        sta $01
        beq flipy
        cmp #31
        bne draw
flipy:  lda #0
        sec
        sbc $03
        sta $03
        inc $04
draw:   lda $04
        and #7
        clc
        adc #2          ; colours 2-9
        jsr plot
        jsr delay
        jmp loop

; plot: draw colour A at (x, y).  address = $0200 + y*32 + x
plot:   pha
        lda $01
        lsr a
        lsr a
        lsr a
        clc
        adc #2
        sta $11         ; high byte
        lda $01
        asl a
        asl a
        asl a
        asl a
        asl a
        clc
        adc $00
        sta $10         ; low byte
        lda $11
        adc #0
        sta $11
        ldy #0
        pla
        sta ($10),y
        rts

delay:  ldx #200
d1:     ldy #20
d2:     dey
        bne d2
        dex
        bne d1
        rts
