COPY    START   1000
FIRST   STL     RETADR
CLOOP   JSUB    RDREC
        LDA     LENGTH
        RSUB
EOF     BYTE    C'EOF'
        END     FIRST
