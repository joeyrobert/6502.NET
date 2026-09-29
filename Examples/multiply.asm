; multiply.asm - 8x8 -> 16 bit shift-and-add multiply using a subroutine.
; $00 * $01 -> result in $02 (low) / $03 (high). Here 200 * 150 = 30000 ($7530).
        .org $0600
        lda #200
        sta $00
        lda #150
        sta $01
        lda $00
        sta $04
        lda #0
        sta $05
        jsr multiply
        brk

multiply:
        lda #0
        sta $02
        sta $03
        ldx #8
mloop:  lsr $01         ; shift multiplier, low bit -> carry
        bcc noadd
        clc
        lda $02
        adc $04         ; add multiplicand (kept shifted at $04/$05)
        sta $02
        lda $03
        adc $05
        sta $03
noadd:  asl $04
        rol $05
        dex
        bne mloop
        rts
