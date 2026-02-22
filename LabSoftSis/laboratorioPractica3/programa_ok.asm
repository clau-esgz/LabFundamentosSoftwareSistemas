. Programa de prueba SIC/XE - Válido
. Este programa demuestra el uso correcto de instrucciones SIC/XE
. y directivas del ensamblador

COPY    START   1000        . Inicio del programa en dirección 1000

. Sección de datos
FIRST   STL     RETADR      . Guardar dirección de retorno
        LDB     #LENGTH     . Cargar longitud en registro B
        BASE    LENGTH      . Establecer registro base
        
. Lectura del primer registro
CLOOP   +JSUB   RDREC       . Leer registro de entrada
        LDA     LENGTH      . Cargar longitud leída
        COMP    #0          . Comparar con cero
        JEQ     ENDFIL      . Si es cero, fin de archivo
        
. Escritura del registro
        +JSUB   WRREC       . Escribir registro de salida
        J       CLOOP       . Repetir ciclo

. Fin del archivo
ENDFIL  LDA     =C'EOF'     . Cargar literal EOF
        STA     BUFFER      . Guardar en buffer
        LDA     #3          . Longitud del mensaje
        STA     LENGTH      . Guardar longitud
        +JSUB   WRREC       . Escribir mensaje EOF
        
. Retorno del programa
        LDL     RETADR      . Cargar dirección de retorno
        RSUB                . Retornar al sistema

. Definición de variables
RETADR  RESW    1           . Reservar una palabra
LENGTH  RESW    1           . Variable de longitud
BUFFER  RESB    4096        . Buffer de 4096 bytes

. Subrutina de lectura de registro
RDREC   CLEAR   X           . Limpiar registro X
        CLEAR   A           . Limpiar acumulador
        CLEAR   S           . Limpiar registro S
        +LDT    #4096       . Longitud máxima del registro
        
RLOOP   TD      INPUT       . Probar dispositivo de entrada
        JEQ     RLOOP       . Esperar si ocupado
        RD      INPUT       . Leer carácter
        COMPR   A,S         . Comparar con fin de registro
        JEQ     EXIT        . Si es igual, terminar
        STCH    BUFFER,X    . Guardar carácter en buffer
        TIXR    T           . Incrementar índice
        JLT     RLOOP       . Continuar si no llegamos al límite
        
EXIT    STX     LENGTH      . Guardar longitud leída
        RSUB                . Retornar

. Subrutina de escritura de registro
WRREC   CLEAR   X           . Limpiar registro X
        LDT     LENGTH      . Cargar longitud a escribir
        
WLOOP   TD      OUTPUT      . Probar dispositivo de salida
        JEQ     WLOOP       . Esperar si ocupado
        LDCH    BUFFER,X    . Cargar carácter del buffer
        WD      OUTPUT      . Escribir carácter
        TIXR    T           . Incrementar índice
        JLT     WLOOP       . Continuar si hay más
        RSUB                . Retornar

. Definición de dispositivos
INPUT   BYTE    X'F1'       . Dispositivo de entrada
OUTPUT  BYTE    X'05'       . Dispositivo de salida

. Constantes
MAXLEN  WORD    4096        . Longitud máxima
THREE   WORD    3           . Constante 3
ZERO    WORD    0           . Constante cero

. Tabla de literales (generada automáticamente)
        LTORG

. Fin del programa
        END     FIRST       . Punto de entrada: FIRST
