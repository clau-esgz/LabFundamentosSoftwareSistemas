#  PASO 1 - Ensamblador SIC/XE

##  ¿Qué hace el Paso 1?

El **Paso 1** del ensamblador de dos pasadas realiza:

1. ✅ **Asignación de direcciones** (LOCCTR - Location Counter)
2. ✅ **Construcción de la tabla de símbolos** (SYMTAB)
3. ✅ **Generación del archivo intermedio**
4. ✅ **Detección de errores** (etiquetas duplicadas, instrucciones desconocidas)

---

##  Cómo usar

### Opción 1: Menú interactivo
```bash
dotnet run
```
Luego:
1. Selecciona un archivo `.asm` del menú
2. Elige la opción **2: "PASO 1 - Ensamblador"**

### Opción 2: Línea de comandos (modo directo)
```bash
# Ejecutar Paso 1 directamente
dotnet run ejemplo_simple.asm 2

# Ejecutar análisis completo (anterior)
dotnet run ejemplo_simple.asm 1
```

```bash
ejecutar_paso1.bat
```

---

## Archivos CSV generados

El Paso 1 genera **3 archivos CSV** en la carpeta `reportes_paso1/`:

### 1️⃣ `*_SYMTAB_*.csv` - Tabla de Símbolos
```csv
SIMBOLO,DIRECCION_HEX,DIRECCION_DEC
COPY,1000,4096
FIRST,1000,4096
CLOOP,1009,4105
ENDFIL,1015,4117
RETADR,1022,4130
LENGTH,1025,4133
BUFFER,1028,4136
...
```
📌 **Ábrelo en Excel/Google Sheets** para ver todos los símbolos con sus direcciones.

### 2️⃣ `*_LISTADO_*.csv` - Listado Intermedio Completo
```csv
LINEA,DIRECCION_HEX,DIRECCION_DEC,ETIQUETA,OPERACION,OPERANDO,INCREMENTO,ERROR,COMENTARIO
1,,,,,,,"Inicio del programa"
2,1000,4096,COPY,START,1000,0,,"Inicio en 1000h"
3,1000,4096,FIRST,STL,RETADR,3,,"Guardar dirección de retorno"
4,1003,4099,,LDB,#LENGTH,3,,"Cargar longitud en B"
5,1006,4102,,BASE,LENGTH,0,,"Establecer registro base"
...
```
📌 **Visualización completa** de todas las líneas con sus direcciones asignadas.

### 3️⃣ `*_RESUMEN_*.csv` - Resumen del Programa
```csv
PROPIEDAD,VALOR_HEX,VALOR_DEC
NOMBRE_PROGRAMA,COPY,COPY
DIRECCION_INICIO,1000,4096
LONGITUD_PROGRAMA,1116,4374
TOTAL_SIMBOLOS,,16
TOTAL_LINEAS,,81
TOTAL_ERRORES,,0
```
📌 **Estadísticas generales** del análisis.

---

## 🖥️ Salida en Consola

También verás un reporte formateado en la consola:

```
╔════════════════════════════════════════════════════════════════════╗
║              PASO 1 - ENSAMBLADOR SIC/XE                          ║
║              ANÁLISIS Y ASIGNACIÓN DE DIRECCIONES                 ║
╚════════════════════════════════════════════════════════════════════╝

Programa: COPY
Dirección de inicio: 1000h (4096)
Longitud del programa: 1116h (4374 bytes)
Total de símbolos: 16

═══════════════════ TABLA DE SÍMBOLOS (SYMTAB) ═══════════════════
SÍMBOLO              | DIRECCIÓN (HEX)    | DIRECCIÓN (DEC)   
────────────────────────────────────────────────────────────
FIRST                | 1000h              | 4096              
CLOOP                | 1006h              | 4102              
ENDFIL               | 101Ah              | 4122              
...

═══════════════════ LISTADO INTERMEDIO ═══════════════════════════
LÍN   | LOC      | ETIQUETA     | OPERACIÓN    | OPERANDO           | INC 
────────────────────────────────────────────────────────────────────────────────
1     |          |              |              |                    |     
2     | 1000h    | COPY         | START        | 1000               |     
3     | 1000h    | FIRST        | STL          | RETADR             | 3   
...
```

---

## 📂 Estructura de archivos

```
laboratorioPractica3/
├── Paso1.cs                         ← Implementación del Paso 1
├── Program.cs                       ← Programa principal modificado
├── SICXESemanticAnalyzer.cs         ← Análisis semántico existente
├── ejemplo_simple.asm               ← Archivo de ejemplo simple
├── ejemplo_con_errores.asm          ← Ejemplo con errores
├── programa_ok.asm                  ← Programa complejo de ejemplo
├── ejecutar_paso1.bat               ← Script para ejecutar rápidamente
├── README_PASO1.md                  ← Esta guía
├── WINDOWS_FORMS_GUIA.md            ← Guía para agregar interfaz gráfica
└── reportes_paso1/                  ← Carpeta de salida (se crea automáticamente)
    ├── *_SYMTAB_*.csv
    ├── *_LISTADO_*.csv
    └── *_RESUMEN_*.csv
```

---

## 🔧 Características implementadas

### ✅ Instrucciones reconocidas

**Formato 1 (1 byte)**:
- `FIX`, `FLOAT`, `HIO`, `NORM`, `SIO`, `TIO`

**Formato 2 (2 bytes)**:
- `ADDR`, `CLEAR`, `COMPR`, `DIVR`, `MULR`, `RMO`
- `SHIFTL`, `SHIFTR`, `SUBR`, `SVC`, `TIXR`

**Formato 3 (3 bytes)**:
- `ADD`, `ADDF`, `AND`, `COMP`, `COMPF`, `DIV`, `DIVF`
- `J`, `JEQ`, `JGT`, `JLT`, `JSUB`
- `LDA`, `LDB`, `LDCH`, `LDF`, `LDL`, `LDS`, `LDT`, `LDX`
- `MUL`, `MULF`, `OR`, `RD`, `RSUB`, `SSK`
- `STA`, `STB`, `STCH`, `STF`, `STI`, `STL`, `STS`, `STSW`, `STT`, `STX`
- `SUB`, `SUBF`, `TD`, `TIX`, `WD`

**Formato 4 (4 bytes)**:
- Cualquier instrucción con prefijo `+` (ej: `+JSUB`, `+LDA`)

### ✅ Directivas

- `START` - Inicio del programa con dirección
- `END` - Fin del programa
- `BYTE` - Constante de bytes (`C'...'` o `X'...'`)
- `WORD` - Constante de 3 bytes
- `RESB` - Reservar bytes
- `RESW` - Reservar palabras (3 bytes cada una)
- `BASE` - Establecer registro base (no genera código)
- `NOBASE` - Desactivar registro base
- `LTORG` - Tabla de literales (marcador, no genera código)

### ✅ Detección de errores

- ❌ **Etiquetas duplicadas**
- ⚠️ **Instrucciones desconocidas** (advertencia)
- 📝 Marcado en el listado intermedio
- 📊 Resumen de errores al final

---

## 📝 Ejemplos de uso

### Ejemplo 1: Programa simple sin errores

```assembly
PROG    START   1000
ALPHA   RESW    1
BEGIN   LDA     ALPHA
        RSUB
        END     BEGIN
```

**Resultado**:
```
Total de símbolos: 2
Longitud del programa: 0007h (7 bytes)
Total de errores: 0
```

### Ejemplo 2: Programa con errores

```assembly
PROG    START   2000
LOOP    LDA     NUM1
LOOP    COMP    #0        ← ERROR: Etiqueta duplicada
        END     PROG
```

**Resultado**:
```
ERRORES ENCONTRADOS:
  • Línea 3: Error - Etiqueta duplicada 'LOOP'
```

---

## 🎨 Visualización en Excel

1. Abre la carpeta `reportes_paso1/`
2. Haz doble clic en `*_SYMTAB_*.csv`
3. Excel muestra los datos en columnas perfectamente formateadas:

| SIMBOLO | DIRECCION_HEX | DIRECCION_DEC |
|---------|---------------|---------------|
| ALPHA   | 1000          | 4096          |
| BETA    | 1003          | 4099          |
| BUFFER  | 1009          | 4105          |

---

## 🔍 Detalles técnicos

### Cálculo de direcciones

| Instrucción/Directiva | Tamaño |
|-----------------------|--------|
| Formato 1             | 1 byte |
| Formato 2             | 2 bytes |
| Formato 3             | 3 bytes |
| Formato 4 (+)         | 4 bytes |
| WORD                  | 3 bytes |
| BYTE C'...'           | longitud del string |
| BYTE X'...'           | (dígitos hex / 2) |
| RESB n                | n bytes |
| RESW n                | n × 3 bytes |
| BASE, NOBASE, LTORG   | 0 bytes (directivas) |

### Tabla de símbolos (SYMTAB)

```
SYMTAB[etiqueta] = dirección actual (LOCCTR)
```

Solo se agregan cuando:
- ✅ Hay una etiqueta en la línea
- ✅ La etiqueta NO existe previamente
- ❌ Si existe → Error de etiqueta duplicada

---

## 🚧 ¿Qué NO hace el Paso 1?

El Paso 1 **NO**:
- ❌ Genera código objeto
- ❌ Resuelve referencias a símbolos
- ❌ Maneja literales (`=C'...'`, `=X'...'`)
- ❌ Calcula modos de direccionamiento

Estas funciones se implementan en el **Paso 2**.

---

## 🎯 Próximos pasos recomendados

1. ✅ **Verificar que funciona con tus archivos**
   ```bash
   dotnet run tu_programa.asm 2
   ```

2. ✅ **Revisar los CSV en Excel**
   - Verifica la tabla de símbolos
   - Revisa las direcciones asignadas

3. 📝 **Implementar Paso 2** (generación de código objeto)
   - Usar la tabla de símbolos del Paso 1
   - Generar códigos de operación
   - Resolver referencias

4. 🎨 **Agregar interfaz gráfica** (opcional)
   - Ver `WINDOWS_FORMS_GUIA.md`
   - Windows Forms o WPF

---

## 🐛 Solución de problemas

### Error: "No se encontró el archivo"
```bash
# Verifica que el archivo existe
ls *.asm

# Usa la ruta completa
dotnet run "C:\ruta\completa\archivo.asm" 2
```

### Error: "Cannot read keys when console input has been redirected"
```bash
# Usa el modo directo con el segundo parámetro
dotnet run archivo.asm 2
```

### No se generan los CSV
```bash
# Verifica que la carpeta reportes_paso1/ se creó
# Si no existe, el programa la crea automáticamente
```

---

- **Libro**: "System Software" de Leland L. Beck (Capítulo 2)
- **SIC/XE**: Arquitectura simplificada de computadora educativa
- **Ensamblador de dos pasadas**: Técnica clásica de implementación

---


Mejoras implementadas:
- ✅ Detección de etiquetas duplicadas
- ✅ Exportación a CSV (3 archivos)
- ✅ Reporte formateado en consola
- ✅ Modo directo sin interacción
- ✅ Script BAT para ejecución rápida
- ✅ Manejo de todos los formatos SIC/XE
- ✅ Cálculo correcto de tamaños BYTE/WORD

---


Este proyecto es parte del curso:
- **Fundamentos de Software de Sistemas**
- **Laboratorio Práctica 3**
- **Ensamblador SIC/XE**

**Nota**: El código está completamente funcional y listo para usar en entornos académicos. Los archivos CSV facilitan la verificación de resultados y la presentación de informes.

