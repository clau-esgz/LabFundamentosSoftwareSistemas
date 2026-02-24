using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
// using ClosedXML.Excel;  // Deshabilitado: no se usa exportación a Excel

namespace laboratorioPractica3
{
    /// <summary>
    /// Implementa el Paso 1 del ensamblador SIC/XE de dos pasadas:
    /// 
    /// OBJETIVO DEL PASO 1:
    /// - Asignar direcciones a todas las instrucciones mediante el CONTLOC (Contador de Localidades)
    /// - Construir la tabla de símbolos (TABSIM) asociando etiquetas con sus direcciones
    /// - Generar el archivo intermedio con formato y modo de direccionamiento para el Paso 2
    /// - Detectar errores semánticos: etiquetas duplicadas, símbolos no definidos, operandos inválidos
    /// 
    /// CONCEPTOS CLAVE:
    /// 
    /// 1. CONTLOC (Contador de Localidades):
    ///    - Rastrea la dirección de memoria de cada instrucción
    ///    - Se inicializa con el valor de la directiva START (o 0 si no existe)
    ///    - Se incrementa según la longitud de cada instrucción
    ///    - Formato 1: +1 byte, Formato 2: +2 bytes, Formato 3: +3 bytes, Formato 4: +4 bytes
    /// 
    /// 2. TABSIM (Tabla de Símbolos):
    ///    - Estructura: Dictionary<string, int> donde la clave es el símbolo y el valor es su dirección
    ///    - Se inserta cuando una etiqueta aparece en el campo de etiqueta de una línea
    ///    - El valor semántico es el CONTLOC actual (dirección donde está definido el símbolo)
    /// 
    /// 3. Valores Semánticos de Directivas:
    ///    - BYTE C'texto': longitud = número de caracteres (ej: C'EOF' = 3 bytes)
    ///    - BYTE X'hex': longitud = (número de dígitos hex + 1) / 2 (ej: X'F1' = 1 byte, X'C1C2' = 2 bytes)
    ///    - WORD: longitud = 3 bytes (1 palabra en SIC/XE)
    ///    - RESB n: reserva n bytes, longitud = n
    ///    - RESW n: reserva n palabras, longitud = n * 3
    /// 
    /// 4. Longitud del Programa:
    ///    - Se calcula al finalizar el programa (directiva END)
    ///    - Fórmula: Longitud = CONTLOC_final - Operando_de_START
    /// 
    /// 5. Directiva BASE:
    ///    - Su valor de operando se almacena para usarlo en el Paso 2
    ///    - Se utiliza para calcular desplazamientos relativos a la base en formato 3
    /// 
    /// 6. Archivo Intermedio:
    ///    - Contiene: Número de línea, CONTLOC, Etiqueta, Operación, Operando, Formato, Modo de direccionamiento
    ///    - Sirve como entrada para el Paso 2 del ensamblador
    /// </summary>
    public class Paso1 : SICXEBaseListener
    {
        // ═══════════════════ VARIABLES DEL PASO 1 ═══════════════════
        
        private int CONTLOC = 0;  // Contador de localidades - rastrea la dirección actual
        private int START_ADDRESS = 0;  // Dirección de inicio del programa (operando de START)
        private string PROGRAM_NAME = "";  // Nombre del programa (etiqueta de START)
        private int PROGRAM_LENGTH = 0;  // Longitud total del programa = CONTLOC_final - START_ADDRESS
        private int? BASE_VALUE = null;  // Valor de la directiva BASE para usar en Paso 2
        
        private Dictionary<string, int> TABSIM = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);  // Tabla de símbolos: símbolo -> dirección
        private List<IntermediateLine> IntermediateLines = new List<IntermediateLine>();  // Archivo intermedio línea por línea
        private List<SICXEError> Errors = new List<SICXEError>();  // Lista de errores detectados
        private HashSet<string> ReferencedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  // Símbolos usados en operandos
        
        private int CurrentLine = 0;  // Número de línea actual del archivo fuente
        private bool ProgramStarted = false;  // Flag para saber si ya se procesó START
        
        
        // ═══════════════════ TABLA DE CÓDIGOS DE OPERACIÓN (OPTAB) ═══════════════════
        // Contiene todas las instrucciones válidas de SIC/XE con su código de operación y formato
        // Formato 1: 1 byte (sin operandos)
        // Formato 2: 2 bytes (operan con registros)
        // Formato 3: 3 bytes (direccionamiento de memoria)
        // Formato 4: 4 bytes (direccionamiento extendido, prefijo +)
        
        private static readonly Dictionary<string, OpCodeInfo> OPTAB = new Dictionary<string, OpCodeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // Formato 1 (1 byte)
            { "FIX", new OpCodeInfo { Opcode = 0xC4, Format = 1 } },
            { "FLOAT", new OpCodeInfo { Opcode = 0xC0, Format = 1 } },
            { "HIO", new OpCodeInfo { Opcode = 0xF4, Format = 1 } },
            { "NORM", new OpCodeInfo { Opcode = 0xC8, Format = 1 } },
            { "SIO", new OpCodeInfo { Opcode = 0xF0, Format = 1 } },
            { "TIO", new OpCodeInfo { Opcode = 0xF8, Format = 1 } },
            
            // Formato 2 (2 bytes)
            { "ADDR", new OpCodeInfo { Opcode = 0x90, Format = 2 } },
            { "CLEAR", new OpCodeInfo { Opcode = 0xB4, Format = 2 } },
            { "COMPR", new OpCodeInfo { Opcode = 0xA0, Format = 2 } },
            { "DIVR", new OpCodeInfo { Opcode = 0x9C, Format = 2 } },
            { "MULR", new OpCodeInfo { Opcode = 0x98, Format = 2 } },
            { "RMO", new OpCodeInfo { Opcode = 0xAC, Format = 2 } },
            { "SHIFTL", new OpCodeInfo { Opcode = 0xA4, Format = 2 } },
            { "SHIFTR", new OpCodeInfo { Opcode = 0xA8, Format = 2 } },
            { "SUBR", new OpCodeInfo { Opcode = 0x94, Format = 2 } },
            { "SVC", new OpCodeInfo { Opcode = 0xB0, Format = 2 } },
            { "TIXR", new OpCodeInfo { Opcode = 0xB8, Format = 2 } },
            
            // Formato 3/4 (3 o 4 bytes)
            { "ADD", new OpCodeInfo { Opcode = 0x18, Format = 3 } },
            { "ADDF", new OpCodeInfo { Opcode = 0x58, Format = 3 } },
            { "AND", new OpCodeInfo { Opcode = 0x40, Format = 3 } },
            { "COMP", new OpCodeInfo { Opcode = 0x28, Format = 3 } },
            { "COMPF", new OpCodeInfo { Opcode = 0x88, Format = 3 } },
            { "DIV", new OpCodeInfo { Opcode = 0x24, Format = 3 } },
            { "DIVF", new OpCodeInfo { Opcode = 0x64, Format = 3 } },
            { "J", new OpCodeInfo { Opcode = 0x3C, Format = 3 } },
            { "JEQ", new OpCodeInfo { Opcode = 0x30, Format = 3 } },
            { "JGT", new OpCodeInfo { Opcode = 0x34, Format = 3 } },
            { "JLT", new OpCodeInfo { Opcode = 0x38, Format = 3 } },
            { "JSUB", new OpCodeInfo { Opcode = 0x48, Format = 3 } },
            { "LDA", new OpCodeInfo { Opcode = 0x00, Format = 3 } },
            { "LDB", new OpCodeInfo { Opcode = 0x68, Format = 3 } },
            { "LDCH", new OpCodeInfo { Opcode = 0x50, Format = 3 } },
            { "LDF", new OpCodeInfo { Opcode = 0x70, Format = 3 } },
            { "LDL", new OpCodeInfo { Opcode = 0x08, Format = 3 } },
            { "LDS", new OpCodeInfo { Opcode = 0x6C, Format = 3 } },
            { "LDT", new OpCodeInfo { Opcode = 0x74, Format = 3 } },
            { "LDX", new OpCodeInfo { Opcode = 0x04, Format = 3 } },
            { "LPS", new OpCodeInfo { Opcode = 0xD0, Format = 3 } },
            { "MUL", new OpCodeInfo { Opcode = 0x20, Format = 3 } },
            { "MULF", new OpCodeInfo { Opcode = 0x60, Format = 3 } },
            { "OR", new OpCodeInfo { Opcode = 0x44, Format = 3 } },
            { "RD", new OpCodeInfo { Opcode = 0xD8, Format = 3 } },
            { "RSUB", new OpCodeInfo { Opcode = 0x4C, Format = 3 } },
            { "SSK", new OpCodeInfo { Opcode = 0xEC, Format = 3 } },
            { "STA", new OpCodeInfo { Opcode = 0x0C, Format = 3 } },
            { "STB", new OpCodeInfo { Opcode = 0x78, Format = 3 } },
            { "STCH", new OpCodeInfo { Opcode = 0x54, Format = 3 } },
            { "STF", new OpCodeInfo { Opcode = 0x80, Format = 3 } },
            { "STI", new OpCodeInfo { Opcode = 0xD4, Format = 3 } },
            { "STL", new OpCodeInfo { Opcode = 0x14, Format = 3 } },
            { "STS", new OpCodeInfo { Opcode = 0x7C, Format = 3 } },
            { "STSW", new OpCodeInfo { Opcode = 0xE8, Format = 3 } },
            { "STT", new OpCodeInfo { Opcode = 0x84, Format = 3 } },
            { "STX", new OpCodeInfo { Opcode = 0x10, Format = 3 } },
            { "SUB", new OpCodeInfo { Opcode = 0x1C, Format = 3 } },
            { "SUBF", new OpCodeInfo { Opcode = 0x5C, Format = 3 } },
            { "TD", new OpCodeInfo { Opcode = 0xE0, Format = 3 } },
            { "TIX", new OpCodeInfo { Opcode = 0x2C, Format = 3 } },
            { "WD", new OpCodeInfo { Opcode = 0xDC, Format = 3 } }
        };
        
        // ═══════════════════ TABLA DE REGISTROS SIC/XE ═══════════════════
        // Registros válidos del sistema SIC/XE con sus códigos numéricos
        // Se usan en instrucciones de formato 2
        // Cada registro tiene un nemónico (nombre), número y uso especial:
        // A(0)=Acumulador, X(1)=Índice, L(2)=Enlace, B(3)=Base, S(4)=General,
        // T(5)=General, F(6)=Punto flotante 48 bits, PC/CP(8)=Contador de programa, SW(9)=Palabra de estado
        
        private static readonly Dictionary<string, int> REGISTERS = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "A", 0 },   // Acumulador para operaciones aritméticas y lógicas
            { "X", 1 },   // Registro índice para direccionar
            { "L", 2 },   // Registro de enlace, para regreso de subrutinas
            { "B", 3 },   // Registro base, para direccionamiento
            { "S", 4 },   // Registro de aplicación general
            { "T", 5 },   // Registro de aplicación general
            { "F", 6 },   // Acumulador de punto flotante (48 bits)
            { "PC", 8 },  // Contador de programa - dirección de siguiente instrucción
            { "CP", 8 },  // Alias de PC (Contador de Programa)
            { "SW", 9 }   // Palabra de estado, información de banderas
        };


        public IReadOnlyDictionary<string, int> SymbolTable => TABSIM;
        public IReadOnlyList<IntermediateLine> Lines => IntermediateLines;
        public IReadOnlyList<SICXEError> ErrorList => Errors;  // Retornar SICXEError
        public int ProgramStartAddress => START_ADDRESS;
        public int ProgramSize => PROGRAM_LENGTH;
        public string ProgramName => PROGRAM_NAME;
        public int? BaseValue => BASE_VALUE;

        /// <summary>
        /// MÉTODO PRINCIPAL DEL PASO 1: Procesa cada línea del programa ensamblador
        /// 
        /// FLUJO DE PROCESAMIENTO:
        /// 1. Extraer componentes de la línea (etiqueta, operación, operando, comentario)
        /// 2. Si hay etiqueta, insertarla en TABSIM con el valor del CONTLOC actual
        /// 3. Determinar el formato de la instrucción (1, 2, 3, o 4 bytes)
        /// 4. Determinar el modo de direccionamiento (inmediato, indirecto, indexado, simple)
        /// 5. Calcular el incremento del CONTLOC según el tipo de instrucción/directiva
        /// 6. Agregar línea al archivo intermedio
        /// 7. Actualizar CONTLOC += incremento
        /// 
        /// CASOS ESPECIALES:
        /// - START: Inicializa CONTLOC con el operando de START
        /// - END: Calcula la longitud del programa y verifica símbolos no definidos
        /// - BASE: Almacena su valor para el Paso 2
        /// - Etiquetas duplicadas: Se detectan y reportan como error
        /// </summary>
        public override void EnterLine([NotNull] SICXEParser.LineContext context)
        {
            CurrentLine++;
            
            var statement = context.statement();
            
            // Ignorar líneas completamente vacías (sin statement ni comment)
            if (statement == null && context.comment() == null)
            {
                return;
            }
            
            // Ignorar líneas que solo tienen comentario vacío
            if (statement == null)
            {
                var commentText = context.comment()?.GetText() ?? "";
                if (string.IsNullOrWhiteSpace(commentText))
                {
                    return;
                }
                // Si hay un comentario real, agregarlo al archivo intermedio
                var intermediateLine = new IntermediateLine
                {
                    LineNumber = IntermediateLines.Count + 1,
                    Address = -1,
                    Label = "",
                    Operation = "",
                    Operand = "",
                    Comment = commentText,
                    Increment = 0
                };
                IntermediateLines.Add(intermediateLine);
                return;
            }

            // Extraer componentes de la línea
            var label = statement.label()?.GetText();
            var operation = statement.operation()?.GetText();
            var operand = statement.operand()?.GetText();
            var comment = statement.comment()?.GetText();
            
            // Detectar formato 4: instrucciones con prefijo + (ej: +JSUB, +LDA)
            bool isFormat4 = operation?.StartsWith("+") == true;
            string baseOperation = isFormat4 ? operation.Substring(1) : operation;
            
            // Determinar formato (1, 2, 3, 4) y modo de direccionamiento
            int format = GetInstructionFormat(baseOperation, isFormat4);
            string addressingMode = DetermineAddressingMode(operand, format);

            // Crear línea del archivo intermedio con toda la información
            var intermediateLine2 = new IntermediateLine
            {
                LineNumber = IntermediateLines.Count + 1,
                Address = CONTLOC,  // Asignar dirección actual
                Label = label ?? "",
                Operation = operation ?? "",
                Operand = operand ?? "",
                Comment = comment ?? "",
                Format = format,
                AddressingMode = addressingMode
            };
            
            // Registrar símbolos referenciados en el operando para verificar después
            if (!string.IsNullOrEmpty(operand))
            {
                RegisterReferencedSymbols(operand);
            }

            // CASO ESPECIAL: Directiva START
            // - Marca el inicio del programa
            // - Define el nombre del programa (etiqueta de START)
            // - Inicializa el CONTLOC con el operando de START
            if (operation?.Equals("START", StringComparison.OrdinalIgnoreCase) == true)
            {
                PROGRAM_NAME = label ?? "NONAME";
                START_ADDRESS = ParseOperand(operand);
                CONTLOC = START_ADDRESS;  // Inicializar CONTLOC con la dirección de inicio
                intermediateLine2.Address = CONTLOC;
                ProgramStarted = true;
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Si no hay START, inicializar en 0
            if (!ProgramStarted)
            {
                CONTLOC = 0;
                START_ADDRESS = 0;
                ProgramStarted = true;
            }
            
            // CASO ESPECIAL: Directiva BASE
            // - Almacena el valor de BASE para usarlo en el Paso 2
            // - Se usa para calcular desplazamientos relativos a la base en formato 3
            if (operation?.Equals("BASE", StringComparison.OrdinalIgnoreCase) == true)
            {
                if (!string.IsNullOrEmpty(operand))
                {
                    if (TABSIM.ContainsKey(operand))
                    {
                        BASE_VALUE = TABSIM[operand];
                    }
                    else
                    {
                        BASE_VALUE = null;
                    }
                }
            }

            // INSERCIÓN EN TABSIM: Si la línea tiene etiqueta, insertarla en la tabla de símbolos
            // - La clave es el nombre de la etiqueta
            // - El valor semántico es el CONTLOC actual (dirección donde está definida)
            // - Se verifica que no exista duplicada
            if (!string.IsNullOrEmpty(label))
            {
                if (TABSIM.ContainsKey(label))
                {
                    Errors.Add(new SICXEError(
                        intermediateLine2.LineNumber,
                        0,
                        $"Etiqueta duplicada '{label}'",
                        SICXEErrorType.Semantico
                    ));
                    intermediateLine2.Error = "Etiqueta duplicada";
                }
                else
                {
                    TABSIM[label] = CONTLOC;  // Insertar símbolo con su dirección
                }
            }

            // CÁLCULO DEL INCREMENTO: Determinar cuántos bytes ocupa esta instrucción/directiva
            // El incremento depende del formato de la instrucción o del tipo de directiva
            int increment = CalculateIncrement(baseOperation, operand);
            intermediateLine2.Increment = increment;
            IntermediateLines.Add(intermediateLine2);

            // CASO ESPECIAL: Directiva END
            // - Marca el fin del programa
            // - Calcula la longitud total: Longitud = CONTLOC_final - START_ADDRESS
            // - Verifica que todos los símbolos referenciados estén definidos
            if (operation?.Equals("END", StringComparison.OrdinalIgnoreCase) == true)
            {
                PROGRAM_LENGTH = CONTLOC - START_ADDRESS;  // Fórmula de longitud del programa
                CheckUndefinedSymbols();  // Verificar símbolos no definidos
                return;
            }

            // ACTUALIZACIÓN DEL CONTLOC: Incrementar según la longitud de la instrucción
            // Este es el paso crucial del Paso 1: mantener el contador de localidades actualizado
            CONTLOC += increment;
        }
        
        /// <summary>
        /// Registra los símbolos referenciados en un operando
        /// </summary>
        private void RegisterReferencedSymbols(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return;
                
            // Remover prefijos y sufijos
            string cleanOperand = operand.TrimStart('#', '@', '=').Split(',')[0].Trim();
            
            // Si no es un número ni un literal, es una referencia a símbolo
            if (!string.IsNullOrEmpty(cleanOperand) && 
                !char.IsDigit(cleanOperand[0]) && 
                !cleanOperand.StartsWith("C'") &&
                !cleanOperand.StartsWith("X'"))
            {
                ReferencedSymbols.Add(cleanOperand);
            }
        }
        
        /// <summary>
        /// Verifica símbolos referenciados pero no definidos
        /// </summary>
        private void CheckUndefinedSymbols()
        {
            foreach (var symbol in ReferencedSymbols)
            {
                if (!TABSIM.ContainsKey(symbol) && !REGISTERS.ContainsKey(symbol))
                {
                    Errors.Add(new SICXEError(
                        0,
                        0,
                        $"Símbolo no definido: '{symbol}'",
                        SICXEErrorType.Semantico
                    ));
                }
            }
        }
        
        /// <summary>
        /// Obtiene el formato de una instrucción
        /// </summary>
        private int GetInstructionFormat(string operation, bool isFormat4)
        {
            if (string.IsNullOrEmpty(operation))
                return 0;
                
            // Directivas no tienen formato
            if (IsDirective(operation))
                return 0;
            
            // Si tiene prefijo +, es formato 4
            if (isFormat4)
                return 4;
                
            // Buscar en OPTAB
            if (OPTAB.TryGetValue(operation, out var opInfo))
            {
                return opInfo.Format;
            }
            
            return 0;
        }
        
        /// <summary>
        /// DETERMINA EL MODO DE DIRECCIONAMIENTO para instrucciones de formato 3/4
        /// 
        /// MODOS DE DIRECCIONAMIENTO EN SIC/XE:
        /// - Inmediato (#): El operando es el valor a usar (ej: LDA #3)
        /// - Indirecto (@): El operando es la dirección de la dirección del valor (ej: LDA @POINTER)
        /// - Indexado (,X): Usa el registro X como índice (ej: LDA BUFFER,X)
        /// - Simple: Direccionamiento directo estándar (ej: LDA VALUE)
        /// </summary>
        private string DetermineAddressingMode(string operand, int format)
        {
            if (string.IsNullOrEmpty(operand) || format == 0)
                return "-";
            
            // Formato 1: sin modo de direccionamiento
            if (format == 1)
                return "-";
            
            // Formato 2: registros, sin modo de direccionamiento
            if (format == 2)
                return "-";
            
            // Formato 3/4: detectar modo según prefijos y sufijos
            if (operand.StartsWith("#"))
                return "Inmediato";  // #3, #LENGTH
            else if (operand.StartsWith("@"))
                return "Indirecto";  // @POINTER
            else if (operand.Contains(",X") || operand.Contains(", X"))
                return "Indexado";   // BUFFER,X
            else
                return "Simple";     // VALUE
        }

        /// <summary>
        /// CÁLCULO DEL INCREMENTO DEL CONTLOC: Determina cuántos bytes ocupa una instrucción/directiva
        /// 
        /// REGLAS DE CÁLCULO:
        /// 
        /// INSTRUCCIONES:
        /// - Formato 1: 1 byte (FIX, FLOAT, HIO, NORM, SIO, TIO)
        /// - Formato 2: 2 bytes (ADDR, CLEAR, COMPR, RMO, etc.)
        /// - Formato 3: 3 bytes (LDA, STA, COMP, JSUB, etc.)
        /// - Formato 4: 4 bytes (instrucciones con prefijo +, ej: +JSUB, +LDA)
        /// 
        /// DIRECTIVAS:
        /// - BYTE C'texto': longitud = número de caracteres (ej: C'EOF' = 3 bytes)
        /// - BYTE X'hexadecimal': longitud = (dígitos hex + 1) / 2 (ej: X'F1' = 1 byte, X'05' = 1 byte)
        /// - WORD: 3 bytes (1 palabra = 3 bytes en SIC/XE)
        /// - RESB n: n bytes (reserva n bytes)
        /// - RESW n: n * 3 bytes (reserva n palabras)
        /// - BASE, NOBASE, LTORG, END: 0 bytes (no ocupan espacio)
        /// </summary>
        private int CalculateIncrement(string operation, string operand)
        {
            if (string.IsNullOrEmpty(operation))
                return 0;

            if (operation.Equals("END", StringComparison.OrdinalIgnoreCase))
                return 0;
            
            if (operation.Equals("BYTE", StringComparison.OrdinalIgnoreCase))
                return CalculateByteSize(operand);
            
            if (operation.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                return 3;
            
            if (operation.Equals("RESB", StringComparison.OrdinalIgnoreCase))
                return ParseOperand(operand);
            
            if (operation.Equals("RESW", StringComparison.OrdinalIgnoreCase))
                return ParseOperand(operand) * 3;
            
            if (operation.Equals("BASE", StringComparison.OrdinalIgnoreCase) ||
                operation.Equals("NOBASE", StringComparison.OrdinalIgnoreCase) ||
                operation.Equals("LTORG", StringComparison.OrdinalIgnoreCase))
                return 0;

            if (operation.StartsWith("+"))
                return 4;
            
            if (IsFormat1Instruction(operation))
                return 1;
            
            if (IsFormat2Instruction(operation))
                return 2;
            
            if (IsFormat3Instruction(operation))
                return 3;

            Errors.Add(new SICXEError(
                CurrentLine,
                0,
                $"Instrucción desconocida '{operation}'",
                SICXEErrorType.Semantico
            ));
            return 3;
        }

        /// <summary>
        /// CÁLCULO DEL TAMAÑO DE LA DIRECTIVA BYTE
        /// 
        /// La directiva BYTE puede almacenar constantes de dos formas:
        /// 1. BYTE C'texto': Almacena caracteres en código ASCII
        ///    - Longitud = número de caracteres
        ///    - Ejemplo: BYTE C'EOF' = 3 bytes (E=45h, O=4Fh, F=46h)
        /// 
        /// 2. BYTE X'hexadecimal': Almacena valores hexadecimales directos
        ///    - Longitud = (número de dígitos hex + 1) / 2
        ///    - Cada 2 dígitos hex = 1 byte
        ///    - Ejemplo: BYTE X'F1' = 1 byte, BYTE X'C1C2C3' = 2 bytes (se redondea hacia arriba)
        /// </summary>
        private int CalculateByteSize(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return 1;

            // BYTE C'texto': contar caracteres
            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase))
            {
                var content = operand.Substring(2, operand.Length - 3);  // Extraer contenido entre C' y '
                return content.Length;  // Cada carácter = 1 byte
            }
            // BYTE X'hexadecimal': cada 2 dígitos hex = 1 byte
            else if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase))
            {
                var content = operand.Substring(2, operand.Length - 3);  // Extraer contenido entre X' y '
                return (content.Length + 1) / 2;  // Dividir entre 2 y redondear hacia arriba
            }

            return 1;
        }

        /// <summary>
        /// PARSEO DE OPERANDOS NUMÉRICOS
        /// 
        /// Convierte operandos a valores enteros:
        /// - Soporta hexadecimal con prefijo 0x: 0x1000
        /// - Soporta hexadecimal sin prefijo: 1000 (si tiene más de 3 dígitos hex)
        /// - Soporta decimal: 4096
        /// - Remueve prefijos de direccionamiento: #, @, =
        /// </summary>
        private int ParseOperand(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return 0;

            // Remover prefijos de direccionamiento
            operand = operand.TrimStart('#', '@', '=');

            try
            {
                // Hexadecimal con prefijo 0x
                if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(operand, 16);
                // Hexadecimal sin prefijo (si tiene >3 dígitos hexadecimales)
                else if (operand.All(c => "0123456789ABCDEFabcdef".Contains(c)) && operand.Length > 3)
                    return Convert.ToInt32(operand, 16);
                // Decimal
                else
                    return int.Parse(operand);
            }
            catch
            {
                return 0;
            }
        }

        private bool IsFormat1Instruction(string op)
        {
            return OPTAB.TryGetValue(op, out var info) && info.Format == 1;
        }

        private bool IsFormat2Instruction(string op)
        {
            return OPTAB.TryGetValue(op, out var info) && info.Format == 2;
        }

        private bool IsFormat3Instruction(string op)
        {
            return OPTAB.TryGetValue(op, out var info) && info.Format == 3;
        }
        
        private bool IsDirective(string op)
        {
            if (string.IsNullOrEmpty(op)) return false;
            var directives = new[] { "START", "END", "BYTE", "WORD", "RESB", "RESW", 
                                      "BASE", "NOBASE", "EQU", "ORG", "LTORG" };
            return directives.Contains(op, StringComparer.OrdinalIgnoreCase);
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PASO 1 - ENSAMBLADOR SIC/XE                          ║");
            sb.AppendLine("║              ANÁLISIS Y ASIGNACIÓN DE DIRECCIONES                 ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            
            sb.AppendLine($"Programa: {PROGRAM_NAME}");
            sb.AppendLine($"Dirección de inicio: {START_ADDRESS:X4}h ({START_ADDRESS})");
            sb.AppendLine($"Longitud del programa: {PROGRAM_LENGTH:X4}h ({PROGRAM_LENGTH} bytes)");
            sb.AppendLine($"Total de símbolos: {TABSIM.Count}");
            if (BASE_VALUE.HasValue)
                sb.AppendLine($"Valor de BASE: {BASE_VALUE.Value:X4}h ({BASE_VALUE.Value})");
            sb.AppendLine();

            sb.AppendLine("═══════════════════ TABLA DE SÍMBOLOS (TABSIM) ═══════════════════");
            sb.AppendLine($"{"SÍMBOLO",-20} | {"DIRECCIÓN (HEX)",-18} | {"DIRECCIÓN (DEC)",-18}");
            sb.AppendLine(new string('─', 60));
            
            foreach (var symbol in TABSIM.OrderBy(s => s.Value))
            {
                sb.AppendLine($"{symbol.Key,-20} | {symbol.Value:X4}h{"",-13} | {symbol.Value,-18}");
            }
            
            if (TABSIM.Count == 0)
            {
                sb.AppendLine("  (No hay símbolos definidos)");
            }
            sb.AppendLine();

            sb.AppendLine("═══════════════════ ARCHIVO INTERMEDIO ═══════════════════════════");
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"FMT",-4} | {"MOD",-12} | {"ERR"}");
            sb.AppendLine(new string('─', 90));
            
            foreach (var line in IntermediateLines)
            {
                string loc = (line.Address >= 0) ? $"{line.Address:X4}h" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "-";
                
                sb.AppendLine($"{line.LineNumber,-4} | {loc,-8} | {line.Label,-10} | {line.Operation,-10} | {line.Operand,-15} | {fmt,-4} | {line.AddressingMode,-12} | {line.Error}");
            }
            sb.AppendLine();

            if (Errors.Count > 0)
            {
                sb.AppendLine("═══════════════════ ERRORES DETECTADOS ══════════════════════════");
                
                var semanticErrors = Errors.Where(e => e.Type == SICXEErrorType.Semantico).OrderBy(e => e.Line);
                
                foreach (var error in semanticErrors)
                {
                    sb.AppendLine($"  • {error}");
                }
                
                sb.AppendLine();
                sb.AppendLine($"  Total errores: {Errors.Count}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Exporta la tabla de símbolos a un archivo CSV
        /// </summary>
        public void ExportSymbolTableToCSV(string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("SIMBOLO,DIRECCION_HEX,DIRECCION_DEC");
            
            foreach (var symbol in TABSIM.OrderBy(s => s.Value))
            {
                sb.AppendLine($"{symbol.Key},{symbol.Value:X4},{symbol.Value}");
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exporta el listado intermedio completo a un archivo CSV
        /// </summary>
        public void ExportIntermediateListingToCSV(string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,ERR,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                string addressHex = (line.Address >= 0) ? $"{line.Address:X4}" : "";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "";
                
                // Buscar si hay errores para esta línea
                string errorMsg = "";
                var lineErrors = Errors.Where(e => e.Line == line.LineNumber);
                if (lineErrors.Any())
                {
                    errorMsg = string.Join("; ", lineErrors.Select(e => e.Message));
                }
                else if (!string.IsNullOrEmpty(line.Error))
                {
                    errorMsg = line.Error;
                }
                
                sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{EscapeCSV(line.Label)},{EscapeCSV(line.Operation)},{EscapeCSV(line.Operand)},{fmt},{EscapeCSV(line.AddressingMode)},{EscapeCSV(errorMsg)},{EscapeCSV(line.Comment)}");
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exporta un resumen del Paso 1 a CSV
        /// </summary>
        public void ExportSummaryToCSV(string outputPath)
        {
            var sb = new StringBuilder();
            sb.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC");
            sb.AppendLine($"NOMBRE_PROGRAMA,{PROGRAM_NAME},{PROGRAM_NAME}");
            sb.AppendLine($"DIRECCION_INICIO,{START_ADDRESS:X4},{START_ADDRESS}");
            sb.AppendLine($"LONGITUD_PROGRAMA,{PROGRAM_LENGTH:X4},{PROGRAM_LENGTH}");
            sb.AppendLine($"TOTAL_SIMBOLOS,,{TABSIM.Count}");
            sb.AppendLine($"TOTAL_LINEAS,,{IntermediateLines.Count}");
            sb.AppendLine($"TOTAL_ERRORES,,{Errors.Count}");
            if (BASE_VALUE.HasValue)
                sb.AppendLine($"VALOR_BASE,{BASE_VALUE.Value:X4},{BASE_VALUE.Value}");

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exporta todos los archivos CSV del Paso 1
        /// </summary>
        public void ExportAllToCSV(string baseOutputPath)
        {
            string directory = Path.GetDirectoryName(baseOutputPath) ?? ".";
            string baseName = Path.GetFileNameWithoutExtension(baseOutputPath);
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string symtabPath = Path.Combine(directory, $"{baseName}_SYMTAB_{timestamp}.csv");
            string intermediatePath = Path.Combine(directory, $"{baseName}_LISTADO_{timestamp}.csv");
            string summaryPath = Path.Combine(directory, $"{baseName}_RESUMEN_{timestamp}.csv");

            ExportSymbolTableToCSV(symtabPath);
            ExportIntermediateListingToCSV(intermediatePath);
            ExportSummaryToCSV(summaryPath);

            Console.WriteLine("Archivos CSV generados:");
            Console.WriteLine($"  - Tabla de simbolos: {Path.GetFileName(symtabPath)}");
            Console.WriteLine($"  - Listado intermedio: {Path.GetFileName(intermediatePath)}");
            Console.WriteLine($"  - Resumen: {Path.GetFileName(summaryPath)}");
        }

        /// <summary>
        /// EXPORTACIÓN A CSV: Genera un archivo CSV con TABSIM y Archivo Intermedio
        /// 
        /// ESTRUCTURA DEL ARCHIVO CSV:
        /// 
        /// SECCIÓN 1 - TABLA DE SÍMBOLOS (TABSIM):
        /// - Columnas: SIMBOLO, DIRECCION_HEX, DIRECCION_DEC
        /// - Contiene todos los símbolos definidos en el programa con sus direcciones
        /// - Ordenados por dirección de menor a mayor
        /// 
        /// SECCIÓN 2 - ARCHIVO INTERMEDIO:
        /// - Columnas: NL, CONTLOC_HEX, CONTLOC_DEC, ETQ, CODOP, OPR, FMT, MOD, COMENTARIO
        /// - NL: Número de línea secuencial
        /// - CONTLOC: Dirección de memoria donde se encuentra la instrucción
        /// - ETQ: Etiqueta (símbolo definido en esta línea)
        /// - CODOP: Código de operación (instrucción o directiva)
        /// - OPR: Operando de la instrucción
        /// - FMT: Formato de la instrucción (1, 2, 3, o 4)
        /// - MOD: Modo de direccionamiento (Inmediato, Indirecto, Indexado, Simple)
        /// - COMENTARIO: Comentarios del programador
        /// 
        /// NOTAS DE FORMATO:
        /// - Valores hexadecimales con comillas para forzar formato texto en Excel
        /// - UTF-8 con BOM para mejor compatibilidad con Excel
        /// - Todas las celdas escapadas correctamente para evitar errores
        /// </summary>
        public void ExportToSingleCSV(string outputPath, List<SICXEError>? allErrors = null)
        {
            var sb = new StringBuilder();
            
            // UTF-8 BOM para mejor compatibilidad con Excel
            var utf8WithBom = new UTF8Encoding(true);
            
            // ═══════════════════ SECCIÓN 1: TABLA DE SÍMBOLOS (TABSIM) ═══════════════════
            sb.AppendLine("=== TABLA DE SIMBOLOS (TABSIM) ===");
            sb.AppendLine("SIMBOLO,DIRECCION_HEX,DIRECCION_DEC");
            
            foreach (var symbol in TABSIM.OrderBy(s => s.Value))
            {
                // Usar comillas para forzar formato texto en valores hexadecimales
                sb.AppendLine($"\"{symbol.Key}\",\"{symbol.Value:X4}\",{symbol.Value}");
            }
            
            if (TABSIM.Count == 0)
            {
                sb.AppendLine("\"(Sin simbolos definidos)\",\"\",\"\"");
            }
            sb.AppendLine();
            
            // ═══════════════════ SECCIÓN 2: ARCHIVO INTERMEDIO ═══════════════════
            sb.AppendLine("=== ARCHIVO INTERMEDIO ===");
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                // Forzar formato texto para valores hexadecimales con comillas
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";
                
                sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{FormatCSVCell(line.Label)},{FormatCSVCell(line.Operation)},{FormatCSVCell(line.Operand)},{fmt},{FormatCSVCell(line.AddressingMode)},{FormatCSVCell(line.Comment)}");
            }

            File.WriteAllText(outputPath, sb.ToString(), utf8WithBom);
        }

        /*
        /// <summary>
        /// MÉTODO DESHABILITADO: Exporta TABSIM y archivo intermedio a Excel con formato correcto usando ClosedXML
        /// Este método está comentado porque no funciona correctamente en todas las configuraciones.
        /// Se mantiene el código como referencia para futuras implementaciones.
        /// </summary>
        public void ExportToExcel(string outputPath, List<SICXEError>? allErrors = null)
        {
            using var workbook = new ClosedXML.Excel.XLWorkbook();
            
            // ═══════════════════ HOJA 1: TABLA DE SÍMBOLOS ═══════════════════
            var tabsimSheet = workbook.Worksheets.Add("TABSIM");
            
            // Encabezados
            tabsimSheet.Cell(1, 1).Value = "SIMBOLO";
            tabsimSheet.Cell(1, 2).Value = "DIRECCION_HEX";
            tabsimSheet.Cell(1, 3).Value = "DIRECCION_DEC";
            
            // Formato de encabezados
            var tabsimHeaderRange = tabsimSheet.Range(1, 1, 1, 3);
            tabsimHeaderRange.Style.Font.Bold = true;
            tabsimHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightGray;
            tabsimHeaderRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
            tabsimHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            
            // Datos
            int row = 2;
            foreach (var symbol in TABSIM.OrderBy(s => s.Value))
            {
                tabsimSheet.Cell(row, 1).Value = symbol.Key;
                tabsimSheet.Cell(row, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;
                
                // Forzar como TEXTO para valores hexadecimales usando formato @
                tabsimSheet.Cell(row, 2).Value = $"{symbol.Value:X4}";
                tabsimSheet.Cell(row, 2).Style.NumberFormat.Format = "@";
                tabsimSheet.Cell(row, 2).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                
                tabsimSheet.Cell(row, 3).Value = symbol.Value;
                tabsimSheet.Cell(row, 3).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
                tabsimSheet.Cell(row, 3).Style.NumberFormat.Format = "0";
                row++;
            }
            
            // Ajustar anchos de columna automáticamente al contenido
            tabsimSheet.Columns().AdjustToContents();
            
            // Bordes para toda la tabla
            if (row > 2)
            {
                tabsimSheet.Range(1, 1, row - 1, 3).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                tabsimSheet.Range(1, 1, row - 1, 3).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            }
            
            // ═══════════════════ HOJA 2: ARCHIVO INTERMEDIO ═══════════════════
            var intermediateSheet = workbook.Worksheets.Add("ARCHIVO INTERMEDIO");
            
            // Encabezados
            string[] headers = { "NL", "CONTLOC_HEX", "CONTLOC_DEC", "ETQ", "CODOP", "OPR", "FMT", "MOD", "COMENTARIO" };
            for (int i = 0; i < headers.Length; i++)
            {
                intermediateSheet.Cell(1, i + 1).Value = headers[i];
            }
            
            // Formato de encabezados
            var intermediateHeaderRange = intermediateSheet.Range(1, 1, 1, headers.Length);
            intermediateHeaderRange.Style.Font.Bold = true;
            intermediateHeaderRange.Style.Fill.BackgroundColor = ClosedXML.Excel.XLColor.LightBlue;
            intermediateHeaderRange.Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
            intermediateHeaderRange.Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
            
            // Datos
            row = 2;
            foreach (var line in IntermediateLines)
            {
                intermediateSheet.Cell(row, 1).Value = line.LineNumber;
                intermediateSheet.Cell(row, 1).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                intermediateSheet.Cell(row, 1).Style.NumberFormat.Format = "0";
                
                if (line.Address >= 0)
                {
                    // CONTLOC_HEX como TEXTO usando formato @
                    intermediateSheet.Cell(row, 2).Value = $"{line.Address:X4}";
                    intermediateSheet.Cell(row, 2).Style.NumberFormat.Format = "@";
                    intermediateSheet.Cell(row, 2).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                    
                    // CONTLOC_DEC como NÚMERO
                    intermediateSheet.Cell(row, 3).Value = line.Address;
                    intermediateSheet.Cell(row, 3).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Right;
                    intermediateSheet.Cell(row, 3).Style.NumberFormat.Format = "0";
                }
                
                intermediateSheet.Cell(row, 4).Value = line.Label;
                intermediateSheet.Cell(row, 4).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;
                
                intermediateSheet.Cell(row, 5).Value = line.Operation;
                intermediateSheet.Cell(row, 5).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;
                
                intermediateSheet.Cell(row, 6).Value = line.Operand;
                intermediateSheet.Cell(row, 6).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;
                
                if (line.Format > 0)
                {
                    intermediateSheet.Cell(row, 7).Value = line.Format;
                    intermediateSheet.Cell(row, 7).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                    intermediateSheet.Cell(row, 7).Style.NumberFormat.Format = "0";
                }
                
                intermediateSheet.Cell(row, 8).Value = line.AddressingMode;
                intermediateSheet.Cell(row, 8).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Center;
                
                intermediateSheet.Cell(row, 9).Value = line.Comment;
                intermediateSheet.Cell(row, 9).Style.Alignment.Horizontal = ClosedXML.Excel.XLAlignmentHorizontalValues.Left;
                
                row++;
            }
            
            // Ajustar anchos de columna automáticamente al contenido
            intermediateSheet.Columns().AdjustToContents();
            
            // Bordes para toda la tabla
            if (row > 2)
            {
                intermediateSheet.Range(1, 1, row - 1, headers.Length).Style.Border.OutsideBorder = ClosedXML.Excel.XLBorderStyleValues.Thin;
                intermediateSheet.Range(1, 1, row - 1, headers.Length).Style.Border.InsideBorder = ClosedXML.Excel.XLBorderStyleValues.Hair;
            }
            
            workbook.SaveAs(outputPath);
        }
        */

        /// <summary>
        /// Formatea una celda para CSV, escapando valores especiales y forzando formato texto
        /// </summary>
        private string FormatCSVCell(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escapar comillas dobles
            value = value.Replace("\"", "\"\"");
            
            // Siempre usar comillas para mantener formato consistente
            return $"\"{value}\"";
        }

        private string EscapeCSV(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }

            return value;
        }
    }

    /// <summary>
    /// Representa una línea del archivo intermedio
    /// </summary>
    public class IntermediateLine
    {
        public int LineNumber { get; set; }
        public int Address { get; set; }
        public string Label { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Operand { get; set; } = "";
        public string Comment { get; set; } = "";
        public int Format { get; set; }
        public string AddressingMode { get; set; } = "-";
        public int Increment { get; set; }
        public string Error { get; set; } = "";
    }
    
    /// <summary>
    /// Información de código de operación
    /// </summary>
    public class OpCodeInfo
    {
        public int Opcode { get; set; }
        public int Format { get; set; }
    }
}
