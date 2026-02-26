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
    /// Implementa el Paso 1 del ensamblador SIC/XE de dos pasadas.
    /// 
    /// UTILIZA LA GRAMÁTICA ANTLR EXISTENTE para determinar formatos de instrucciones:
    /// - format1Instruction (gramática): FIX, FLOAT, HIO, NORM, SIO, TIO -> 1 byte
    /// - format2Instruction (gramática): ADDR, CLEAR, COMPR, etc. -> 2 bytes
    /// - format34Instruction (gramática): LDA, STA, JSUB, etc. -> 3 bytes (o 4 con +)
    /// - directive (gramática): START, END, BYTE, WORD, RESB, RESW, BASE, etc.
    /// 
    /// OBJETIVO DEL PASO 1:
    /// - Asignar direcciones a todas las instrucciones mediante el CONTLOC
    /// - Construir la tabla de símbolos (TABSIM)
    /// - Generar el archivo intermedio para el Paso 2
    /// - Detectar errores semánticos
    /// </summary>
    public class Paso1 : SICXEBaseListener
    {
        // ═══════════════════ VARIABLES DEL PASO 1 ═══════════════════
        
        private int CONTLOC = 0;
        private int START_ADDRESS = 0;
        private int CONTLOC_FINAL = 0;
        private string PROGRAM_NAME = "";
        private int PROGRAM_LENGTH = 0;
        private int? BASE_VALUE = null;
        private string BASE_OPERAND = "";
        
        private Dictionary<string, int> TABSIM = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        private List<IntermediateLine> IntermediateLines = new List<IntermediateLine>();
        private List<SICXEError> Errors = new List<SICXEError>();
        private HashSet<string> ReferencedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Diccionario para rastrear en qué línea se usa cada símbolo (para reportar errores correctamente)
        private Dictionary<string, List<int>> SymbolUsageLines = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        
        private int CurrentLine = 0;
        private bool ProgramStarted = false;
        
        // ═══════════════════ TABLA DE CÓDIGOS DE OPERACIÓN (OPTAB) ═══════════════════
        // NOTA: Se mantiene para el PASO 2 (generación de código objeto)
        // En el PASO 1 usamos la gramática ANTLR para determinar formatos
        // Esta tabla contiene los OpCodes hexadecimales verificados según especificación SIC/XE
        
        public static readonly Dictionary<string, OpCodeInfo> OPTAB = new Dictionary<string, OpCodeInfo>(StringComparer.OrdinalIgnoreCase)
        {
            // Formato 1 (1 byte) - según gramática: format1Instruction
            { "FIX", new OpCodeInfo { Opcode = 0xC4, Format = 1 } },
            { "FLOAT", new OpCodeInfo { Opcode = 0xC0, Format = 1 } },
            { "HIO", new OpCodeInfo { Opcode = 0xF4, Format = 1 } },
            { "NORM", new OpCodeInfo { Opcode = 0xC8, Format = 1 } },
            { "SIO", new OpCodeInfo { Opcode = 0xF0, Format = 1 } },
            { "TIO", new OpCodeInfo { Opcode = 0xF8, Format = 1 } },
            
            // Formato 2 (2 bytes) - según gramática: format2Instruction
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
            
            // Formato 3/4 (3 o 4 bytes) - según gramática: format34Instruction
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
        // Registros válidos del conjunto de instrucciones SIC/XE
        // Corresponden a los IDENT que la gramática reconoce como operandos de formato 2
        public static readonly HashSet<string> REGISTERS = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "A", "X", "L", "B", "S", "T", "F", "PC", "CP", "SW"
        };
        
        // ═══════════════════ NÚMERO DE REGISTRO SIC/XE ═══════════════════
        // Mapeo nombre → número según especificación SIC/XE (usado por Paso 2 en formato 2)
        public static readonly Dictionary<string, int> REGISTER_NUMBERS = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "A", 0 }, { "X", 1 }, { "L", 2 }, { "B", 3 },
            { "S", 4 }, { "T", 5 }, { "F", 6 },
            { "PC", 8 }, { "SW", 9 }
        };
        
        // ═══════════════════ DIRECTIVAS SIC/XE ═══════════════════
        // Extraídas de la regla 'directive' de la gramática SICXE.g4:
        //   directive : START | END | BYTE | WORD | RESB | RESW | BASE | NOBASE
        //             | EQU | ORG | LTORG | USE | EXTDEF | EXTREF | CSECT ;
        public static readonly HashSet<string> DIRECTIVES = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "START", "END", "BYTE", "WORD", "RESB", "RESW",
            "BASE", "NOBASE", "EQU", "ORG", "LTORG", "USE",
            "EXTDEF", "EXTREF", "CSECT"
        };
        
        // ═══════════════════ ERRORES EXTERNOS (LÉXICOS/SINTÁCTICOS) ═══════════════════
        // Almacena errores detectados por el lexer/parser para asociarlos con las líneas del archivo intermedio
        private List<SICXEError> ExternalErrors = new List<SICXEError>();
        private string[] SourceLines = Array.Empty<string>();  // Líneas originales del código fuente
        private HashSet<int> ProcessedErrorLines = new HashSet<int>();  // Líneas de error ya procesadas

        public IReadOnlyDictionary<string, int> SymbolTable => TABSIM;
        public IReadOnlyList<IntermediateLine> Lines => IntermediateLines;
        public IReadOnlyList<SICXEError> ErrorList => Errors;
        public int ProgramStartAddress => START_ADDRESS;
        public int ProgramSize => PROGRAM_LENGTH;
        public string ProgramName => PROGRAM_NAME;
        public int? BaseValue => BASE_VALUE;

        /// <summary>
        /// Agrega errores externos (léxicos/sintácticos) para asociarlos con líneas del archivo intermedio
        /// </summary>
        public void AddExternalErrors(IEnumerable<SICXEError> errors)
        {
            ExternalErrors.AddRange(errors);
        }

        /// <summary>
        /// Establece las líneas originales del código fuente para procesar errores
        /// </summary>
        public void SetSourceLines(string[] lines)
        {
            SourceLines = lines;
        }

        /// <summary>
        /// Propaga errores externos (del analizador semántico) a las líneas del archivo intermedio
        /// usando el número de línea ANTLR (SourceLine) para mapear correctamente.
        /// </summary>
        public void MergeErrors(IEnumerable<SICXEError> errors)
        {
            foreach (var error in errors)
            {
                var line = IntermediateLines.FirstOrDefault(l => l.SourceLine == error.Line);
                if (line == null) continue;

                if (string.IsNullOrEmpty(line.Error))
                    line.Error = error.Message;
                else if (!line.Error.Contains(error.Message))
                    line.Error += "; " + error.Message;
            }
        }

        /// <summary>
        /// Verifica si hay un error sintáctico/léxico en una línea específica
        /// </summary>
        private bool HasSyntaxErrorOnLine(int lineNumber)
        {
            return ExternalErrors.Any(e => e.Line == lineNumber && 
                (e.Type == SICXEErrorType.Sintactico || e.Type == SICXEErrorType.Lexico));
        }

        /// <summary>
        /// Obtiene los errores de una línea específica
        /// </summary>
        private List<SICXEError> GetErrorsForLine(int lineNumber)
        {
            return ExternalErrors.Where(e => e.Line == lineNumber).ToList();
        }

        /// <summary>
        /// MÉTODO PRINCIPAL DEL PASO 1: Usa el contexto de la gramática ANTLR para procesar líneas
        /// 
        /// MANEJO DE ERRORES SEGÚN ESPECIFICACIÓN:
        /// - Error de sintaxis: Se marca error, NO incrementa CP, etiqueta NO se inserta en TABSIM
        /// - Instrucción no existe: Se marca error, NO incrementa CP, etiqueta NO se inserta en TABSIM
        /// - Símbolo duplicado: Se marca error, pero puede afectar CP si la línea es correcta
        /// </summary>
        public override void EnterLine([NotNull] SICXEParser.LineContext context)
        {
            CurrentLine++;
            
            // Obtener la línea real del contexto de ANTLR
            int antlrLine = context.Start?.Line ?? CurrentLine;
            
            // Verificar si hay error sintáctico/léxico en esta línea (usar línea de ANTLR)
            bool hasSyntaxError = HasSyntaxErrorOnLine(antlrLine);
            var lineErrors = GetErrorsForLine(antlrLine);
            
            var statement = context.statement();
            
            // Si no hay statement Y no hay comentario, verificar si hay error en la línea
            if (statement == null && context.comment() == null)
            {
                // Si hay error sintáctico y NO hemos procesado esta línea antes, crear línea de error
                if (hasSyntaxError && SourceLines.Length >= antlrLine && !ProcessedErrorLines.Contains(antlrLine))
                {
                    ProcessedErrorLines.Add(antlrLine);  // Marcar como procesada
                    string originalLine = SourceLines[antlrLine - 1];
                    var errorLine = ParseErrorLine(originalLine, lineErrors);
                    errorLine.SourceLine = antlrLine;
                    IntermediateLines.Add(errorLine);
                    // Agregar errores al listado
                    foreach (var err in lineErrors)
                        Errors.Add(err);
                    // NO incrementar CONTLOC para errores sintácticos
                }
                return;
            }
            
            if (statement == null)
            {
                var commentText = context.comment()?.GetText() ?? "";
                if (string.IsNullOrWhiteSpace(commentText))
                    return;
                    
                var intermediateLine = new IntermediateLine
                {
                    LineNumber = IntermediateLines.Count + 1,
                    SourceLine = antlrLine,
                    Address = -1,
                    Comment = commentText
                };
                IntermediateLines.Add(intermediateLine);
                return;
            }

            // Extraer componentes usando el contexto de la gramática
            var label = statement.label()?.GetText();
            var operationContext = statement.operation();
            var operand = statement.operand()?.GetText();
            var comment = statement.comment()?.GetText();
            var operation = operationContext?.GetText();
            
            // Si hay error sintáctico, SIEMPRE usar la línea original para extraer la información
            // porque ANTLR puede no haber parseado correctamente
            if (hasSyntaxError && SourceLines.Length >= antlrLine)
            {
                string originalLine = SourceLines[antlrLine - 1];
                var parsedLine = ParseLineManually(originalLine);
                
                // Usar los valores de la línea original si están disponibles
                // Esto asegura que mostremos LDX y ##5 aunque ANTLR no los haya parseado
                if (!string.IsNullOrEmpty(parsedLine.Operation))
                    operation = parsedLine.Operation;
                if (!string.IsNullOrEmpty(parsedLine.Operand))
                    operand = parsedLine.Operand;
                if (!string.IsNullOrEmpty(parsedLine.Label))
                    label = parsedLine.Label;
                if (!string.IsNullOrEmpty(parsedLine.Comment))
                    comment = parsedLine.Comment;
            }
            
            // USAR LA GRAMÁTICA para determinar el formato de instrucción
            bool isFormat4 = operationContext?.FORMAT4_PREFIX() != null || (operation?.StartsWith("+") == true);
            int format = GetFormatFromGrammar(operationContext, isFormat4);
            // El modo de direccionamiento solo aplica a instrucciones de formato 3 o 4
            string addressingMode = (format == 3 || format == 4)
                ? DetermineAddressingModeFromGrammar(statement.operand())
                : "-";
            string baseOperation = isFormat4 ? operation?.Substring(1) : operation;
            
            // Construir mensaje de error si hay errores en esta línea
            string errorMsg = "";
            if (hasSyntaxError)
            {
                string errorTypeLabel = GetSyntaxErrorTypeLabel(operation ?? "");
                errorMsg = $"[{errorTypeLabel}] " + string.Join("; ", lineErrors.Select(e => e.Message));
            }

            var intermediateLine2 = new IntermediateLine
            {
                LineNumber = IntermediateLines.Count + 1,
                SourceLine = antlrLine,
                Address = CONTLOC,
                Label = label ?? "",
                Operation = operation ?? "",
                Operand = operand ?? "",
                Comment = comment ?? "",
                Format = format,
                AddressingMode = addressingMode,
                Error = errorMsg
            };
            
            // Registrar símbolos referenciados con el número de línea para rastrear errores
            if (!string.IsNullOrEmpty(operand))
                RegisterReferencedSymbols(operand, IntermediateLines.Count + 1);

            // Directiva START
            if (operationContext?.directive()?.START() != null)
            {
                PROGRAM_NAME = label ?? "NONAME";
                START_ADDRESS = ParseOperand(operand);
                CONTLOC = START_ADDRESS;
                intermediateLine2.Address = CONTLOC;
                ProgramStarted = true;
                
                // Para START, insertar etiqueta en TABSIM si existe
                if (!string.IsNullOrEmpty(label) && !TABSIM.ContainsKey(label))
                {
                    TABSIM[label] = CONTLOC;
                }

                intermediateLine2.SemanticValue = $"{START_ADDRESS:X4}h";
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            if (!ProgramStarted)
            {
                CONTLOC = 0;
                START_ADDRESS = 0;
                ProgramStarted = true;
            }
            
            // Directiva BASE
            if (operationContext?.directive()?.BASE() != null)
            {
                BASE_OPERAND = operand ?? "";
                if (!string.IsNullOrEmpty(operand) && TABSIM.ContainsKey(operand))
                    BASE_VALUE = TABSIM[operand];
            }

            // Si hay error sintáctico/léxico en esta línea:
            // - NO insertar etiqueta en TABSIM
            // - NO incrementar CONTLOC
            // - Marcar error en archivo intermedio
            if (hasSyntaxError)
            {
                // NO insertar etiqueta en TABSIM cuando hay error sintáctico
                intermediateLine2.Increment = 0;  // NO incrementar CONTLOC
                IntermediateLines.Add(intermediateLine2);
                
                // Agregar errores a la lista de errores del Paso 1
                foreach (var err in lineErrors)
                {
                    Errors.Add(err);
                }
                
                
                // NO incrementar CONTLOC para errores sintácticos
                return;
            }

            // ═══════════════════ VALIDACIONES ARQUITECTURALES SIC/XE ═══════════════════
            // Errores que violan las reglas de formato de la arquitectura SIC/XE.
            // Al igual que los errores de sintaxis:
            //   - NO se inserta la etiqueta en TABSIM
            //   - NO se incrementa CONTLOC
            {
                string? architecturalError = null;
                var instrContext = operationContext?.instruction();

                // Regla 1: El prefijo '+' (formato 4) solo es válido para instrucciones de formato 3
                if (isFormat4 && instrContext != null)
                {
                    if (instrContext.format1Instruction() != null)
                    {
                        string opName = (baseOperation ?? "").ToUpper();
                        architecturalError = $"[Error de sintaxis] El prefijo '+' no es válido para '{opName}' (formato 1) en la arquitectura SIC/XE.";
                    }
                    else if (instrContext.format2Instruction() != null)
                    {
                        string opName = (baseOperation ?? "").ToUpper();
                        architecturalError = $"[Error de sintaxis] El prefijo '+' no es válido para '{opName}' (formato 2) en la arquitectura SIC/XE.";
                    }
                }

                // Regla 2: Instrucciones de formato 1 (FIX, FLOAT, HIO, NORM, SIO, TIO) NO aceptan operandos
                if (architecturalError == null && format == 1 && !string.IsNullOrEmpty(operand))
                {
                    string opName = (baseOperation ?? operation ?? "").ToUpper();
                    architecturalError = $"[Error de sintaxis] La instrucción '{opName}' (formato 1) no acepta operandos en la arquitectura SIC/XE.";
                }

                if (architecturalError != null)
                {
                    intermediateLine2.Error = string.IsNullOrEmpty(intermediateLine2.Error)
                        ? architecturalError
                        : intermediateLine2.Error + "; " + architecturalError;
                    Errors.Add(new SICXEError(antlrLine, 0, architecturalError, SICXEErrorType.Semantico));
                    intermediateLine2.Increment = 0;  // NO incrementar CONTLOC
                    IntermediateLines.Add(intermediateLine2);
                    // NO insertar etiqueta en TABSIM, NO incrementar CONTLOC
                    return;
                }
            }

            // Inserción en TABSIM (solo si NO hay error sintáctico)
            if (!string.IsNullOrEmpty(label))
            {
                if (TABSIM.ContainsKey(label))
                {
                    // Símbolo duplicado: se marca error, pero la línea puede afectar CP si es correcta
                    string dupError = $"[Símbolo duplicado] La etiqueta '{label}' ya está definida en TABSIM";
                    Errors.Add(new SICXEError(CurrentLine, 0, dupError, SICXEErrorType.Semantico));
                    intermediateLine2.Error = dupError;
                    // NO se inserta en TABSIM
                }
                else
                {
                    TABSIM[label] = CONTLOC;
                }
            }

            // Cálculo del incremento usando la gramática
            int increment = CalculateIncrementFromGrammar(operationContext, operand);
            intermediateLine2.Increment = increment;
            intermediateLine2.SemanticValue = CalculateSemanticValue(operationContext, operand);
            IntermediateLines.Add(intermediateLine2);

            // Directiva END
            if (operationContext?.directive()?.END() != null)
            {
                CONTLOC_FINAL = CONTLOC;
                PROGRAM_LENGTH = CONTLOC - START_ADDRESS;
                CheckUndefinedSymbols();
                return;
            }

            // Incrementar CONTLOC (solo si NO hay error sintáctico - ya manejado arriba)
            CONTLOC += increment;
        }

        /// <summary>
        /// Crea una línea del archivo intermedio para una línea con error sintáctico
        /// cuando el parser no pudo generar un contexto válido.
        /// El CONTLOC NO se incrementa para líneas con error.
        /// Muestra la información original de la línea del código fuente.
        /// </summary>
        private IntermediateLine ParseErrorLine(string originalLine, List<SICXEError> errors)
        {
            // Limpiar la línea de comentarios (todo después de . ; o //)
            string lineWithoutComment = originalLine;
            string comment = "";
            
            // Detectar comentarios estilo //
            int doubleSlashIndex = originalLine.IndexOf("//");
            if (doubleSlashIndex >= 0)
            {
                comment = originalLine.Substring(doubleSlashIndex);
                lineWithoutComment = originalLine.Substring(0, doubleSlashIndex);
            }
            else
            {
                // Detectar comentarios estilo . o ;
                int commentIndex = originalLine.IndexOfAny(new[] { '.', ';' });
                if (commentIndex >= 0)
                {
                    comment = originalLine.Substring(commentIndex);
                    lineWithoutComment = originalLine.Substring(0, commentIndex);
                }
            }
            
            // Separar por espacios y tabs
            string[] parts = lineWithoutComment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            string label = "";
            string operation = "";
            string operand = "";
            
            if (parts.Length > 0)
            {
                // Detectar si el primer elemento es una etiqueta o una operación
                string first = parts[0];
                
                // Si comienza con letra y no es una instrucción/directiva conocida, es etiqueta
                if (char.IsLetter(first[0]) && !IsKnownOperation(first))
                {
                    label = first;
                    if (parts.Length > 1) operation = parts[1];
                    if (parts.Length > 2) operand = string.Join(" ", parts.Skip(2));
                }
                else
                {
                    operation = first;
                    if (parts.Length > 1) operand = string.Join(" ", parts.Skip(1));
                }
            }
            
            string errorTypeLabel = GetSyntaxErrorTypeLabel(operation);
            string errorMsg = $"[{errorTypeLabel}] " + string.Join("; ", errors.Select(e => e.Message));
            
            return new IntermediateLine
            {
                LineNumber = IntermediateLines.Count + 1,
                Address = CONTLOC,  // Mostrar CP actual (sin incrementar)
                Label = label,
                Operation = operation,
                Operand = operand,
                Comment = comment,
                Format = 0,
                AddressingMode = "-",
                Increment = 0,  // NO incrementar CONTLOC
                Error = errorMsg
            };
        }

        /// <summary>
        /// Parsea una línea manualmente para extraer sus componentes
        /// cuando el parser de ANTLR no pudo hacerlo correctamente debido a errores sintácticos.
        /// Retorna una tupla con (Label, Operation, Operand, Comment)
        /// </summary>
        private (string Label, string Operation, string Operand, string Comment) ParseLineManually(string originalLine)
        {
            string label = "";
            string operation = "";
            string operand = "";
            string comment = "";
            
            // Limpiar la línea de comentarios (todo después de . ; o //)
            string lineWithoutComment = originalLine;
            
            // Detectar comentarios estilo //
            int doubleSlashIndex = originalLine.IndexOf("//");
            if (doubleSlashIndex >= 0)
            {
                comment = originalLine.Substring(doubleSlashIndex);
                lineWithoutComment = originalLine.Substring(0, doubleSlashIndex);
            }
            else
            {
                // Detectar comentarios estilo . o ;
                int commentIndex = originalLine.IndexOfAny(new[] { '.', ';' });
                if (commentIndex >= 0)
                {
                    comment = originalLine.Substring(commentIndex);
                    lineWithoutComment = originalLine.Substring(0, commentIndex);
                }
            }
            
            // Separar por espacios y tabs
            string[] parts = lineWithoutComment.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            
            if (parts.Length > 0)
            {
                string first = parts[0];
                
                // Si comienza con letra y no es una instrucción/directiva conocida, es etiqueta
                if (char.IsLetter(first[0]) && !IsKnownOperation(first))
                {
                    label = first;
                    if (parts.Length > 1) operation = parts[1];
                    if (parts.Length > 2) operand = string.Join(" ", parts.Skip(2));
                }
                else
                {
                    operation = first;
                    if (parts.Length > 1) operand = string.Join(" ", parts.Skip(1));
                }
            }
            
            return (label, operation, operand, comment);
        }

        /// <summary>
        /// Determina la etiqueta del tipo de error para la columna ERR del archivo intermedio
        /// según la especificación del Paso 1:
        /// - "Instrucción no existe": la operación no pertenece al conjunto de instrucciones SIC/XE
        /// - "Error de sintaxis": la estructura de la línea es inválida léxica o sintácticamente
        /// </summary>
        private string GetSyntaxErrorTypeLabel(string operation)
        {
            if (!string.IsNullOrEmpty(operation))
            {
                string baseOp = operation.TrimStart('+');
                if (baseOp.Length > 0 && char.IsLetter(baseOp[0]) && !IsKnownOperation(baseOp))
                    return "Instrucción no existe";
            }
            return "Error de sintaxis";
        }

        /// <summary>
        /// Verifica si un texto es una operación conocida (instrucción o directiva)
        /// Usa OPTAB (gramática: instruction) y DIRECTIVES (gramática: directive)
        /// </summary>
        private bool IsKnownOperation(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            
            // Remover prefijo + si existe
            string op = text.TrimStart('+');
            
            // Verificar en OPTAB (instrucciones de la gramática)
            if (OPTAB.ContainsKey(op)) return true;
            
            // Verificar en DIRECTIVES (directivas de la gramática)
            return DIRECTIVES.Contains(op);
        }

        /// <summary>
        /// OBTIENE EL FORMATO USANDO EL CONTEXTO DE LA GRAMÁTICA
        /// En lugar de duplicar listas, usa las reglas definidas en SICXE.g4
        /// </summary>
        private int GetFormatFromGrammar(SICXEParser.OperationContext operationContext, bool isFormat4)
        {
            if (operationContext == null)
                return 0;
                
            // Si es directiva, no tiene formato
            if (operationContext.directive() != null)
                return 0;
            
            var instruction = operationContext.instruction();
            if (instruction == null)
                return 0;

            // Si tiene prefijo +, es formato 4
            if (isFormat4)
                return 4;
            
            // Usar las reglas de la gramática para determinar formato
            if (instruction.format1Instruction() != null)
                return 1;  // FIX, FLOAT, HIO, NORM, SIO, TIO
            
            if (instruction.format2Instruction() != null)
                return 2;  // ADDR, CLEAR, COMPR, DIVR, MULR, RMO, SHIFTL, SHIFTR, SUBR, SVC, TIXR
            
            if (instruction.format34Instruction() != null)
                return 3;  // ADD, ADDF, AND, COMP, etc.
            
            return 0;
        }

        /// <summary>
        /// DETERMINA EL MODO DE DIRECCIONAMIENTO USANDO LA GRAMÁTICA
        /// </summary>
        private string DetermineAddressingModeFromGrammar(SICXEParser.OperandContext operandContext)
        {
            if (operandContext == null)
                return "-";
            
            var operandExprs = operandContext.operandExpr();
            if (operandExprs == null || operandExprs.Length == 0)
                return "-";
            
            var firstOperand = operandExprs[0];
            
            // Usar las reglas de la gramática para detectar modo
            if (firstOperand.PREFIX_IMMEDIATE() != null)
                return "Inmediato";  // #valor
            
            if (firstOperand.PREFIX_INDIRECT() != null)
                return "Indirecto";  // @valor
            
            // Verificar si tiene indexación
            if (firstOperand.indexing() != null)
                return "Indexado";  // valor,X
            
            return "Simple";
        }

        /// <summary>
        /// CALCULA EL INCREMENTO USANDO LA GRAMÁTICA
        /// </summary>
        private int CalculateIncrementFromGrammar(SICXEParser.OperationContext operationContext, string operand)
        {
            if (operationContext == null)
                return 0;
            
            var directive = operationContext.directive();
            if (directive != null)
            {
                // Directivas usando la gramática
                if (directive.END() != null || directive.BASE() != null || 
                    directive.NOBASE() != null || directive.LTORG() != null)
                    return 0;
                
                if (directive.BYTE() != null)
                    return CalculateByteSize(operand);
                
                if (directive.WORD() != null)
                    return 3;
                
                if (directive.RESB() != null)
                    return ParseOperand(operand);
                
                if (directive.RESW() != null)
                    return ParseOperand(operand) * 3;
                
                return 0;
            }
            
            var instruction = operationContext.instruction();
            if (instruction != null)
            {
                // Formato 4 tiene prefijo +
                if (operationContext.FORMAT4_PREFIX() != null)
                    return 4;
                
                // Usar las reglas de la gramática
                if (instruction.format1Instruction() != null)
                    return 1;
                
                if (instruction.format2Instruction() != null)
                    return 2;
                
                if (instruction.format34Instruction() != null)
                    return 3;
            }
            
            return 0;
        }
        
        /// <summary>
        /// Registra los símbolos referenciados en un operando y la línea donde se usan
        /// </summary>
        private void RegisterReferencedSymbols(string operand, int lineNumber)
        {
            if (string.IsNullOrEmpty(operand))
                return;
                
            // Remover prefijos y sufijos
            string cleanOperand = operand.TrimStart('#', '@', '=').Split(',')[0].Trim();
            
            // Si no es un número ni un literal, es una referencia a símbolo
            if (!string.IsNullOrEmpty(cleanOperand) && 
                !char.IsDigit(cleanOperand[0]) && 
                !cleanOperand.StartsWith("C'") &&
                !cleanOperand.StartsWith("X'") &&
                !cleanOperand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                ReferencedSymbols.Add(cleanOperand);
                
                // Rastrear en qué líneas se usa este símbolo
                if (!SymbolUsageLines.ContainsKey(cleanOperand))
                {
                    SymbolUsageLines[cleanOperand] = new List<int>();
                }
                SymbolUsageLines[cleanOperand].Add(lineNumber);
            }
        }
        
        /// <summary>
        /// Verifica símbolos referenciados pero no definidos
        /// Agrega el error a las líneas correspondientes del archivo intermedio
        /// </summary>
        private void CheckUndefinedSymbols()
        {
            foreach (var symbol in ReferencedSymbols)
            {
                if (!TABSIM.ContainsKey(symbol) && !REGISTERS.Contains(symbol))
                {
                    // Obtener las líneas donde se usa este símbolo
                    if (SymbolUsageLines.ContainsKey(symbol))
                    {
                        foreach (var lineNum in SymbolUsageLines[symbol])
                        {
                            string errorMsg = $"[Símbolo no definido] La etiqueta '{symbol}' no está definida en TABSIM";
                            Errors.Add(new SICXEError(lineNum, 0, errorMsg, SICXEErrorType.Semantico));
                        }
                    }
                    else
                    {
                        // Si no tenemos la línea específica, reportar en línea 0
                        Errors.Add(new SICXEError(0, 0, $"Símbolo no definido: '{symbol}'", SICXEErrorType.Semantico));
                    }
                }
            }
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
        /// Calcula el valor semántico de una directiva según su tipo y base.
        /// - BYTE C'texto' : códigos ASCII de cada carácter en hexadecimal
        /// - BYTE X'hex'   : el valor hexadecimal tal como está
        /// - WORD n        : el entero representado en 6 dígitos hexadecimales
        /// - RESB/RESW n   : cantidad de bytes reservados (sin valor inicial)
        /// - BASE sym      : dirección del símbolo base registrado
        /// </summary>
        private string CalculateSemanticValue(SICXEParser.OperationContext? operationContext, string? operand)
        {
            if (operationContext?.directive() == null) return "";
            var directive = operationContext.directive();
            operand = operand ?? "";

    if (directive.BYTE() != null)
                return "";

            if (directive.WORD() != null)
                return "";

            if (directive.RESB() != null)
                return "";

            if (directive.RESW() != null)
                return "";

            if (directive.BASE() != null && !string.IsNullOrEmpty(operand))
                return TABSIM.TryGetValue(operand, out int baseAddr) ? $"{baseAddr:X4}h" : operand;

            return "";
        }

        /// <summary>
        /// Calcula el valor semántico de BYTE según el tipo de constante:
        /// - C'texto' → código ASCII de cada carácter concatenado en hex (ej. C'EOF' → 454F46h)
        /// - X'hex'   → valor hexadecimal directo                        (ej. X'F1'  → F1h)
        /// </summary>
        private string CalculateByteSemanticValue(string operand)
        {
            if (string.IsNullOrEmpty(operand)) return "";

            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                var content = operand.Substring(2, operand.Length - 3);
                return string.Concat(content.Select(c => ((int)c).ToString("X2"))) + "h";
            }

            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
                return operand.Substring(2, operand.Length - 3).ToUpper() + "h";

            return "";
        }

        /// <summary>
        /// Parseo de operandos numéricos
        /// Soporta:
        /// - Decimal: 100, 256
        /// - Hexadecimal con sufijo H: 12H, 100H, 0ABCh
        /// - Hexadecimal con prefijo 0x: 0x12, 0x100
        /// - Constante X'...': X'12', X'ABC'
        /// </summary>
        private int ParseOperand(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return 0;

            operand = operand.TrimStart('#', '@', '=').Trim();

            try
            {
                // Hexadecimal con sufijo H o h (ej: 12H, 100h, 0ABCh)
                if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
                {
                    string hexPart = operand.Substring(0, operand.Length - 1);
                    return Convert.ToInt32(hexPart, 16);
                }
                
                // Hexadecimal con prefijo 0x (ej: 0x12, 0xFF)
                if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(operand, 16);
                
                // Constante hexadecimal X'...' (ej: X'12', X'ABC')
                if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
                {
                    string hexPart = operand.Substring(2, operand.Length - 3);
                    return Convert.ToInt32(hexPart, 16);
                }
                
                // Número decimal
                return int.Parse(operand);
            }
            catch
            {
                return 0;
            }
        }

        public string GenerateReport()
        {
            var sb = new StringBuilder();
            
            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PASO 1 - ENSAMBLADOR SIC/XE                          ║");
            sb.AppendLine("║              ANÁLISIS Y ASIGNACIÓN DE DIRECCIONES                 ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();
            
            int finalContloc = CONTLOC_FINAL > 0 ? CONTLOC_FINAL : CONTLOC;
            sb.AppendLine($"Programa        : {PROGRAM_NAME}");
            sb.AppendLine($"Dir. inicio     : {START_ADDRESS:X4}h  ({START_ADDRESS})");
            sb.AppendLine($"CONTLOC final   : {finalContloc:X4}h  ({finalContloc})");
            sb.AppendLine($"Long. programa  : {PROGRAM_LENGTH:X4}h  ({PROGRAM_LENGTH} bytes)  [= CONTLOC_final({finalContloc:X4}h) - START({START_ADDRESS:X4}h)]");
            sb.AppendLine($"Total símbolos  : {TABSIM.Count}");
            if (BASE_VALUE.HasValue)
            {
                string baseDisplay = string.IsNullOrEmpty(BASE_OPERAND) ? "" : $"'{BASE_OPERAND}' -> ";
                sb.AppendLine($"Valor BASE      : {baseDisplay}{BASE_VALUE.Value:X4}h  ({BASE_VALUE.Value})  [almacenado para Paso 2]");
            }
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
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"VALOR_SEM",-15} | {"FMT",-4} | {"MOD",-12} | {"ERR"}");
            sb.AppendLine(new string('─', 125));
            
            foreach (var line in IntermediateLines)
            {
                string loc = (line.Address >= 0) ? $"{line.Address:X4}h" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "-";
                string errorDisplay = line.Error ?? "";
                
                // Truncar error si es muy largo para la consola
                if (errorDisplay.Length > 40)
                    errorDisplay = errorDisplay.Substring(0, 37) + "...";
                
                sb.AppendLine($"{line.LineNumber,-4} | {loc,-8} | {line.Label,-10} | {line.Operation,-10} | {line.Operand,-15} | {line.SemanticValue,-15} | {fmt,-4} | {line.AddressingMode,-12} | {errorDisplay}");
            }
            sb.AppendLine();

            if (Errors.Count > 0)
            {
                sb.AppendLine("═══════════════════ ERRORES DETECTADOS ══════════════════════════");
                
                // Agrupar errores por tipo
                var lexicalErrors = Errors.Where(e => e.Type == SICXEErrorType.Lexico).OrderBy(e => e.Line);
                var syntaxErrors = Errors.Where(e => e.Type == SICXEErrorType.Sintactico).OrderBy(e => e.Line);
                var semanticErrors = Errors.Where(e => e.Type == SICXEErrorType.Semantico).OrderBy(e => e.Line);
                
                if (lexicalErrors.Any())
                {
                    sb.AppendLine("  [ERRORES LÉXICOS]");
                    foreach (var error in lexicalErrors)
                    {
                        sb.AppendLine($"  • {error}");
                    }
                    sb.AppendLine();
                }
                
                if (syntaxErrors.Any())
                {
                    sb.AppendLine("  [ERRORES SINTÁCTICOS]");
                    foreach (var error in syntaxErrors)
                    {
                        sb.AppendLine($"  • {error}");
                    }
                    sb.AppendLine();
                }
                
                if (semanticErrors.Any())
                {
                    sb.AppendLine("  [ERRORES SEMÁNTICOS]");
                    foreach (var error in semanticErrors)
                    {
                        sb.AppendLine($"  • {error}");
                    }
                    sb.AppendLine();
                }
                
                sb.AppendLine($"  Total errores: {Errors.Count}");
            }

            // ═══════════════════ RESUMEN DEL ANÁLISIS ═══════════════════
            sb.AppendLine();
            sb.AppendLine("═══════════════════ RESUMEN DEL ANÁLISIS ════════════════════");
            sb.AppendLine($"  * Tabla de símbolos (TABSIM): {TABSIM.Count} símbolo(s) definido(s)");
            sb.AppendLine($"  * Longitud del programa: CONTLOC_final({finalContloc:X4}h) - START({START_ADDRESS:X4}h) = {PROGRAM_LENGTH:X4}h ({PROGRAM_LENGTH} bytes)");
            if (BASE_VALUE.HasValue)
            {
                string baseSum = string.IsNullOrEmpty(BASE_OPERAND) ? "" : $"'{BASE_OPERAND}' -> ";
                sb.AppendLine($"  * BASE almacenado: {baseSum}{BASE_VALUE.Value:X4}h ({BASE_VALUE.Value}) [disponible para Paso 2]");
            }
            sb.AppendLine($"  * Archivo intermedio: {IntermediateLines.Count} línea(s) generada(s)");
            sb.AppendLine($"  * Errores detectados: {Errors.Count}");

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
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,VALOR_SEM,FMT,MOD,ERR,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                // Forzar formato texto para valores hexadecimales con comillas
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";
                
                // Obtener errores para esta línea usando SourceLine para mapeo correcto
                string errorMsg = line.Error ?? "";
                if (allErrors != null)
                {
                    var lineErrors = allErrors.Where(e => e.Line == line.SourceLine);
                    if (lineErrors.Any())
                    {
                        string additionalErrors = string.Join("; ", lineErrors
                            .Select(e => e.Message)
                            .Where(m => !errorMsg.Contains(m)));
                        if (!string.IsNullOrEmpty(additionalErrors))
                            errorMsg = string.IsNullOrEmpty(errorMsg) ? additionalErrors : errorMsg + "; " + additionalErrors;
                    }
                }
                
                sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{FormatCSVCell(line.Label)},{FormatCSVCell(line.Operation)},{FormatCSVCell(line.Operand)},{FormatCSVCell(line.SemanticValue)},{fmt},{FormatCSVCell(line.AddressingMode)},{FormatCSVCell(errorMsg)},{FormatCSVCell(line.Comment)}");
            }

            // ═══════════════════ SECCIÓN 3: RESUMEN DEL PASO 1 ═══════════════════
            sb.AppendLine();
            sb.AppendLine("=== RESUMEN DEL PASO 1 ===");
            sb.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC,DESCRIPCION");
            int csvFinalContloc = CONTLOC_FINAL > 0 ? CONTLOC_FINAL : CONTLOC;
            sb.AppendLine($"\"NOMBRE_PROGRAMA\",\"{PROGRAM_NAME}\",\"{PROGRAM_NAME}\",\"Nombre del programa\"");
            sb.AppendLine($"\"DIR_INICIO\",\"{START_ADDRESS:X4}\",{START_ADDRESS},\"Dirección de inicio (operando de START)\"");
            sb.AppendLine($"\"CONTLOC_FINAL\",\"{csvFinalContloc:X4}\",{csvFinalContloc},\"CONTLOC al llegar a END\"");
            sb.AppendLine($"\"LONGITUD_PROGRAMA\",\"{PROGRAM_LENGTH:X4}\",{PROGRAM_LENGTH},\"= CONTLOC_final({csvFinalContloc:X4}h) - START({START_ADDRESS:X4}h)\"");
            sb.AppendLine($"\"TOTAL_SIMBOLOS\",\"\",{TABSIM.Count},\"Símbolos definidos en TABSIM\"");
            sb.AppendLine($"\"TOTAL_LINEAS\",\"\",{IntermediateLines.Count},\"Líneas en archivo intermedio\"");
            sb.AppendLine($"\"TOTAL_ERRORES\",\"\",{(allErrors?.Count ?? Errors.Count)},\"Total de errores detectados\"");
            if (BASE_VALUE.HasValue)
            {
                sb.AppendLine($"\"BASE_OPERANDO\",\"\",\"{BASE_OPERAND}\",\"Operando de la directiva BASE\"");
                sb.AppendLine($"\"BASE_VALOR\",\"{BASE_VALUE.Value:X4}\",{BASE_VALUE.Value},\"Valor almacenado de BASE (para uso en Paso 2)\"");
            }

            File.WriteAllText(outputPath, sb.ToString(), utf8WithBom);
        }


        /// <summary>
        /// Formatea una celda para CSV, escapando valores especiales y forzando formato texto
        /// IMPORTANTE: Previene que Excel interprete valores como fórmulas
        /// </summary>
        private string FormatCSVCell(string value)
        {
            if (string.IsNullOrEmpty(value))
                return "\"\"";

            // Escapar comillas dobles
            value = value.Replace("\"", "\"\"");
            
            // Prevenir interpretación como fórmula en Excel
            // Caracteres que Excel interpreta como inicio de fórmula: +, -, =, @
            if (value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("=") || value.StartsWith("@"))
            {
                // Agregar apóstrofo al inicio para forzar texto (se ocultará en Excel)
                // O simplemente agregar un espacio que Excel preserva
                value = "'" + value;
            }
            
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
        public int SourceLine { get; set; }  // Número de línea ANTLR (fuente original)
        public int Address { get; set; }
        public string Label { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Operand { get; set; } = "";
        public string SemanticValue { get; set; } = "";
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
