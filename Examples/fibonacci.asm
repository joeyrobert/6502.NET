; fibonacci.asm - stores the Fibonacci numbers while the next sum still fits in a byte,
; at $0010.. and halts. Result: 0 1 1 2 3 5 8 13 21 34 55 89 144 (13 values).
        .org $0600
        ldx #0
        lda #0          ; $00 = F(n), $01 = F(n+1)
        sta $00
        lda #1
        sta $01
loop:   lda $00
        sta $10,x       ; store F(n)
        inx
        clc
        adc $01         ; F(n) + F(n+1)
        bcs done        ; overflowed a byte: stop
        tay
        lda $01
        sta $00
        sty $01
        jmp loop
done:   brk
