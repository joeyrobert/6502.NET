; primes.asm - sieve of Eratosthenes for 0..127. 
; $1000+n is 1 if n is prime, 0 otherwise; the primes are also listed at $1100, count in $00.
        .org $0600
SIEVE = $1000
LIST  = $1100
        ; mark everything prime
        ldx #127
        lda #1
init:   sta SIEVE,x
        dex
        bpl init
        lda #0
        sta SIEVE
        sta SIEVE+1
        ldx #2
outer:  lda SIEVE,x
        beq next
        txa
        tay
inner:  tya
        clc
        stx $01
        adc $01         ; next multiple
        bcs collect
        tay
        bmi collect     ; >= 128
        lda #0
        sta SIEVE,y
        jmp inner
next:
collect:
        inx
        cpx #12         ; sqrt(128) is about 11.3
        bcc outer
        ; collect the primes into LIST
        ldx #0
        ldy #0
scan:   lda SIEVE,x
        beq skip
        txa
        sta LIST,y
        iny
skip:   inx
        cpx #128
        bne scan
        sty $00
        brk
