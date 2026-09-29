; stack_demo.asm - subroutines, the stack and indirect jumps.
; Calls a subroutine three times to double a value, checks the result via JMP (indirect), then halts.
        .org $0600
        lda #3
        jsr double      ; 6
        jsr double      ; 12
        jsr double      ; 24
        sta $00
        php             ; save flags
        pha
        lda #0
        pla             ; restores 24
        plp
        jmp (vector)
        lda #$ff        ; skipped
        sta $01
target: lda #$aa
        sta $02
        brk
double: asl a
        rts
vector: .word target
