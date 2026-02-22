. Programa de prueba SIC/XE - Con errores intencionales
. Este archivo contiene múltiples errores para probar el analizador

. ERROR 1: Etiqueta más de 6 caracteres
ETIQUETAMUYLARGAS   START   1000    . Etiqueta excede 6 caracteres

. ERROR 2: Instrucción no válida
        LOADA   BUFFER      . LOADA no es instrucción válida

. ERROR 3: Directiva mal escrita
        STAR    2000        . Debería ser START

. ERROR 4: Falta operando requerido
LOOP    LDA                 . LDA requiere operando

. ERROR 5: Etiqueta duplicada
BUFFER  RESW    100         . Primera definición de BUFFER
        LDA     BUFFER
BUFFER  RESB    50          . ERROR: Etiqueta duplicada

. ERROR 6: Número incorrecto de operandos para CLEAR
        CLEAR   A,X         . CLEAR solo requiere 1 registro

. ERROR 7: RSUB con operando (no debe tener)
        RSUB    MAIN        . RSUB no debe tener operando

. ERROR 8: Instrucción de 2 registros con solo 1
        ADDR    A           . ADDR requiere 2 registros

. ERROR 9: BYTE sin constante válida
DATA    BYTE    123         . BYTE requiere X'xx' o C'xx'

. ERROR 10: Formato extendido en instrucción formato 1
        +FIX                . FIX es formato 1, no puede ser +FIX

. ERROR 11: Otra etiqueta larga
LABELEXTRA   WORD   5       . Etiqueta con más de 6 caracteres

. ERROR 12: RMO con número en lugar de registro
        RMO     A,5         . Segundo operando debe ser registro

. ERROR 13: END sin etiqueta de inicio
        END                 . END debería tener etiqueta

. ERROR 14: Caracteres inválidos
VAR$1   RESW    1           . $ no es válido en identificador

. ERROR 15: COMPR con solo un operando
        COMPR   A           . COMPR requiere 2 registros

. Algunas líneas válidas para comparación
VALID   LDA     #100
        STA     VALID
        +JSUB   VALID
        LTORG

. ERROR 16: TIX sin operando
        TIX                 . TIX requiere dirección de memoria

. ERROR 17: Segunda etiqueta duplicada
VALID   RESW    1           . VALID ya fue definida arriba

. Fin con errores
ENDERR  END     LOOP
