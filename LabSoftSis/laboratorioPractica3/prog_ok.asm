COPY 	START 	1000	
FIRST 	+JSUB 	RDREC 	; Formato extendido
 	LDA 	#3	; Inmediato
 	COMP 	LENGTH 	; Simple/directo
 	JEQ 	@ENDFIL	; Indirecto
 	STA 	BUFFER,X	; Indexado
 	RSUB		
ENDFIL 	LDA 	EOF	
EOF 	BYTE 	C'EOF'	
LENGTH 	WORD 	3	
BUFFER 	RESW 	10	
 	END 	FIRST