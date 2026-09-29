; bubble_sort.asm - sorts the 10 bytes at $0080 ascending, then halts.
        .org $0600
        .org $0080
data:   .byte 9, 3, 200, 7, 1, 250, 42, 0, 17, 5
        .org $0600
LEN = 10
again:  ldx #0
        ldy #0          ; y counts swaps this pass
pass:   lda data,x
        cmp data+1,x
        bcc ordered     ; data[x] < data[x+1]
        beq ordered
        pha             ; swap using the stack
        lda data+1,x
        sta data,x
        pla
        sta data+1,x
        iny
ordered:
        inx
        cpx #LEN-1
        bne pass
        cpy #0
        bne again
        brk
