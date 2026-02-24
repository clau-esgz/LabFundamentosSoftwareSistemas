using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace laboratorioPractica3
{
    /// <summary>
    /// Implementa el Paso 1 del ensamblador SIC/XE de dos pasadas:
    /// - Asigna direcciones a todas las instrucciones (CONTLOC)
    /// - Construye la tabla de símbolos (TABSIM)
    /// - Genera el archivo intermedio con formato y modo de direccionamiento
    /// - Detecta errores: etiquetas duplicadas, símbolos no definidos, operandos inválidos
    /// </summary>
    public class Paso1 : SICXEBaseListener
    {
        private int CONTLOC = 0;  // Contador de localidades
        private int START_ADDRESS = 0;
        private string PROGRAM_NAME = "";
        private int PROGRAM_LENGTH = 0;
        private int? BASE_VALUE = null;  // Valor de la directiva BASE para Paso 2
        
        private Dictionary<string, int> TABSIM = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);  // Tabla de símbolos
        private List<IntermediateLine> IntermediateLines = new List<IntermediateLine>();
        private List<SICXEError> Errors = new List<SICXEError>();  // Usar SICXEError en lugar de List<string>
        private HashSet<string> ReferencedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);  // Símbolos referenciados
        
        private int CurrentLine = 0;
        private bool ProgramStarted = false;
        
        // Tabla de códigos de operación (OPTAB) con formatos
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
        
        // Tabla de registros SIC/XE
        private static readonly Dictionary<string, int> REGISTERS = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "A", 0 },   // Acumulador
            { "X", 1 },   // Registro índice
            { "L", 2 },   // Registro de enlace
            { "B", 3 },   // Registro base
            { "S", 4 },   // Registro general
            { "T", 5 },   // Registro general
            { "F", 6 },   // Registro punto flotante
            { "PC", 8 },  // Contador de programa
            { "SW", 9 }   // Palabra de estado
        };


        public IReadOnlyDictionary<string, int> SymbolTable => TABSIM;
        public IReadOnlyList<IntermediateLine> Lines => IntermediateLines;
        public IReadOnlyList<SICXEError> ErrorList => Errors;  // Retornar SICXEError
        public int ProgramStartAddress => START_ADDRESS;
        public int ProgramSize => PROGRAM_LENGTH;
        public string ProgramName => PROGRAM_NAME;
        public int? BaseValue => BASE_VALUE;

        public override void EnterLine([NotNull] SICXEParser.LineContext context)
        {
            CurrentLine++;
            
            var statement = context.statement();
            
            // Ignorar líneas completamente vacías (sin statement ni comment)
            if (statement == null && context.comment() == null)
            {
                return; // No agregar líneas vacías al listado
            }
            
            // Ignorar líneas que solo tienen comentario vacío
            if (statement == null)
            {
                var commentText = context.comment()?.GetText() ?? "";
                if (string.IsNullOrWhiteSpace(commentText))
                {
                    return; // No agregar líneas solo con espacios
                }
                // Si hay un comentario real, agregarlo
                var intermediateLine = new IntermediateLine
                {
                    LineNumber = IntermediateLines.Count + 1,  // Numeración continua
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

            var label = statement.label()?.GetText();
            var operation = statement.operation()?.GetText();
            var operand = statement.operand()?.GetText();
            var comment = statement.comment()?.GetText();
            
            // Detectar formato 4 (con prefijo +)
            bool isFormat4 = operation?.StartsWith("+") == true;
            string baseOperation = isFormat4 ? operation.Substring(1) : operation;
            
            // Determinar formato y modo de direccionamiento
            int format = GetInstructionFormat(baseOperation, isFormat4);
            string addressingMode = DetermineAddressingMode(operand, format);

            var intermediateLine2 = new IntermediateLine
            {
                LineNumber = IntermediateLines.Count + 1,  // Numeración continua
                Address = CONTLOC,
                Label = label ?? "",
                Operation = operation ?? "",
                Operand = operand ?? "",
                Comment = comment ?? "",
                Format = format,
                AddressingMode = addressingMode
            };
            
            // Registrar símbolos referenciados en el operando
            if (!string.IsNullOrEmpty(operand))
            {
                RegisterReferencedSymbols(operand);
            }

            if (operation?.Equals("START", StringComparison.OrdinalIgnoreCase) == true)
            {
                PROGRAM_NAME = label ?? "NONAME";
                START_ADDRESS = ParseOperand(operand);  // Respetar valor de START
                CONTLOC = START_ADDRESS;
                intermediateLine2.Address = CONTLOC;
                ProgramStarted = true;
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            if (!ProgramStarted)
            {
                CONTLOC = 0;
                START_ADDRESS = 0;
                ProgramStarted = true;
            }
            
            // Manejar directiva BASE
            if (operation?.Equals("BASE", StringComparison.OrdinalIgnoreCase) == true)
            {
                // Intentar resolver el valor de BASE (puede ser un símbolo o número)
                if (!string.IsNullOrEmpty(operand))
                {
                    if (TABSIM.ContainsKey(operand))
                    {
                        BASE_VALUE = TABSIM[operand];
                    }
                    else
                    {
                        // Guardar para resolver después
                        BASE_VALUE = null;
                    }
                }
            }

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
                    TABSIM[label] = CONTLOC;
                }
            }

            int increment = CalculateIncrement(baseOperation, operand);
            intermediateLine2.Increment = increment;
            IntermediateLines.Add(intermediateLine2);

            if (operation?.Equals("END", StringComparison.OrdinalIgnoreCase) == true)
            {
                PROGRAM_LENGTH = CONTLOC - START_ADDRESS;
                // Verificar símbolos no definidos al final
                CheckUndefinedSymbols();
                return;
            }

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
        /// Determina el modo de direccionamiento según el operando
        /// </summary>
        private string DetermineAddressingMode(string operand, int format)
        {
            if (string.IsNullOrEmpty(operand) || format == 0)
                return "-";
            
            // Formato 1: sin modo de direccionamiento
            if (format == 1)
                return "-";
            
            // Formato 2: registros
            if (format == 2)
                return "-";
            
            // Formato 3/4: múltiples modos
            if (operand.StartsWith("#"))
                return "Inmediato";
            else if (operand.StartsWith("@"))
                return "Indirecto";
            else if (operand.Contains(",X") || operand.Contains(", X"))
                return "Indexado";
            else
                return "Simple";
        }

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

        private int CalculateByteSize(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return 1;

            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase))
            {
                var content = operand.Substring(2, operand.Length - 3);
                return content.Length;
            }
            else if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase))
            {
                var content = operand.Substring(2, operand.Length - 3);
                return (content.Length + 1) / 2;
            }

            return 1;
        }

        private int ParseOperand(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return 0;

            operand = operand.TrimStart('#', '@', '=');

            try
            {
                if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                    return Convert.ToInt32(operand, 16);
                else if (operand.All(c => "0123456789ABCDEFabcdef".Contains(c)) && operand.Length > 3)
                    return Convert.ToInt32(operand, 16);
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
        /// Exporta TABSIM y archivo intermedio en UN SOLO archivo CSV
        /// El formato incluye: Resumen del programa, Tabla de símbolos y Archivo intermedio
        /// </summary>
        public void ExportToSingleCSV(string outputPath, List<SICXEError>? allErrors = null)
        {
            var sb = new StringBuilder();
            var errorsToUse = allErrors ?? Errors.ToList();
            
            // ═══════════════════ SECCIÓN 1: RESUMEN DEL PROGRAMA ═══════════════════
            sb.AppendLine("=== RESUMEN DEL PROGRAMA ===");
            sb.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC");
            sb.AppendLine($"NOMBRE_PROGRAMA,{PROGRAM_NAME},{PROGRAM_NAME}");
            sb.AppendLine($"DIRECCION_INICIO,{START_ADDRESS:X4},{START_ADDRESS}");
            sb.AppendLine($"LONGITUD_PROGRAMA,{PROGRAM_LENGTH:X4},{PROGRAM_LENGTH}");
            sb.AppendLine($"TOTAL_SIMBOLOS,,{TABSIM.Count}");
            sb.AppendLine($"TOTAL_LINEAS,,{IntermediateLines.Count}");
            sb.AppendLine($"TOTAL_ERRORES,,{errorsToUse.Count}");
            if (BASE_VALUE.HasValue)
                sb.AppendLine($"VALOR_BASE,{BASE_VALUE.Value:X4},{BASE_VALUE.Value}");
            sb.AppendLine();
            
            // ═══════════════════ SECCIÓN 2: TABLA DE SÍMBOLOS (TABSIM) ═══════════════════
            sb.AppendLine("=== TABLA DE SIMBOLOS (TABSIM) ===");
            sb.AppendLine("SIMBOLO,DIRECCION_HEX,DIRECCION_DEC");
            
            foreach (var symbol in TABSIM.OrderBy(s => s.Value))
            {
                sb.AppendLine($"{symbol.Key},{symbol.Value:X4},{symbol.Value}");
            }
            
            if (TABSIM.Count == 0)
            {
                sb.AppendLine("(Sin simbolos definidos),,");
            }
            sb.AppendLine();
            
            // ═══════════════════ SECCIÓN 3: ARCHIVO INTERMEDIO ═══════════════════
            sb.AppendLine("=== ARCHIVO INTERMEDIO ===");
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,ERR,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                string addressHex = (line.Address >= 0) ? $"{line.Address:X4}" : "";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "";
                
                // Buscar todos los errores para esta línea
                var lineErrors = errorsToUse.Where(e => e.Line == line.LineNumber);
                string errorMsg = "";
                
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
            sb.AppendLine();
            
            // ═══════════════════ SECCIÓN 4: LISTADO DE ERRORES ═══════════════════
            if (errorsToUse.Count > 0)
            {
                sb.AppendLine("=== ERRORES DETECTADOS ===");
                sb.AppendLine("LINEA,COLUMNA,TIPO,MENSAJE");
                
                foreach (var error in errorsToUse.OrderBy(e => e.Line).ThenBy(e => e.Column))
                {
                    sb.AppendLine($"{error.Line},{error.Column},{error.Type},{EscapeCSV(error.Message)}");
                }
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
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
