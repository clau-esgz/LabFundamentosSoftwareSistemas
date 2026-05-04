using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
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
        
        private readonly Dictionary<string, Bloques> BLOCKS_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new Bloques()
        };

        private Bloques BLOCKS => GetBlocksForSection(CurrentControlSectionName);

        private Bloques GetBlocksForSection(string? sectionName)
        {
            string normalized = string.IsNullOrWhiteSpace(sectionName) ? "Por Omision" : sectionName.Trim();
            if (!BLOCKS_BY_CSECT.TryGetValue(normalized, out var blocks))
            {
                blocks = new Bloques();
                BLOCKS_BY_CSECT[normalized] = blocks;
            }

            return blocks;
        }
        private int CONTLOC
        {
            get => BLOCKS.CurrentLocation;
            set => BLOCKS.CurrentLocation = value;
        }
        private int START_ADDRESS = 0;
        private int CONTLOC_FINAL = 0;
        private int EXECUTION_ENTRY_POINT = 0;  // Dirección de inicio de ejecución (del operando de END)
        private int? FIRST_EXECUTABLE_ADDRESS = null;  // Primera instrucción ejecutable que genera código objeto
        private string PROGRAM_NAME = "";
        private int PROGRAM_LENGTH = 0;
        private int? BASE_VALUE = null;
        private string BASE_OPERAND = "";
        private bool BLOCKS_FINALIZED = false;
        private bool AllowExternalDeclarations = false;

        // TABSIM_EXT guarda valor y tipo (absoluto/relativo) por simbolo
        // Requisito 1: la tabla indica si cada simbolo es absoluto o relativo
        private SimbolosYExpresiones TABSIM_EXT = new SimbolosYExpresiones();
        private Dictionary<string, int> TABSIM => TABSIM_EXT.GetAllSymbols().ToDictionary(k => k.Key, v => v.Value.Value, StringComparer.OrdinalIgnoreCase);

        // Contenedores por sección de control (base para soporte CSECT/EXTDEF/EXTREF)
        private string CurrentControlSectionName = "Por Omision";
        private int CurrentControlSectionNumber = 0;
        private int NextControlSectionNumber = 1;
        private readonly Dictionary<string, int> CSECT_CP_BY_NAME = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = 0
        };
        private readonly Dictionary<string, int> CSECT_NUMBER_BY_NAME = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = 0
        };
        private readonly Dictionary<string, Dictionary<string, SymbolInfo>> TABSIM_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
        };
        private readonly Dictionary<string, List<BloqueInfo>> TABBLK_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new List<BloqueInfo>()
        };
        private readonly Dictionary<string, HashSet<string>> TABREG_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        };
        private readonly Dictionary<string, List<string>> EXTDEF_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new List<string>()
        };
        private readonly Dictionary<string, List<string>> EXTREF_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new List<string>()
        };
        private readonly Dictionary<string, Dictionary<string, List<int>>> EXTDEF_DECL_LINES_BY_CSECT = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase)
        };

        private void EnsureControlSectionContainers(string sectionName)
        {
            if (!TABSIM_BY_CSECT.ContainsKey(sectionName))
                TABSIM_BY_CSECT[sectionName] = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);

            if (!TABBLK_BY_CSECT.ContainsKey(sectionName))
                TABBLK_BY_CSECT[sectionName] = new List<BloqueInfo>();

            if (!EXTDEF_BY_CSECT.ContainsKey(sectionName))
                EXTDEF_BY_CSECT[sectionName] = new List<string>();

            if (!EXTREF_BY_CSECT.ContainsKey(sectionName))
                EXTREF_BY_CSECT[sectionName] = new List<string>();

            if (!EXTDEF_DECL_LINES_BY_CSECT.ContainsKey(sectionName))
                EXTDEF_DECL_LINES_BY_CSECT[sectionName] = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);

            if (!TABREG_BY_CSECT.ContainsKey(sectionName))
                TABREG_BY_CSECT[sectionName] = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (!CSECT_CP_BY_NAME.ContainsKey(sectionName))
                CSECT_CP_BY_NAME[sectionName] = 0;
        }

        private void SaveCurrentControlSectionState()
        {
            CSECT_CP_BY_NAME[CurrentControlSectionName] = CONTLOC;
        }

        private void OpenControlSection(string sectionName)
        {
            if (!CSECT_NUMBER_BY_NAME.TryGetValue(sectionName, out int sectionNumber))
            {
                sectionNumber = NextControlSectionNumber++;
                CSECT_NUMBER_BY_NAME[sectionName] = sectionNumber;
            }

            EnsureControlSectionContainers(sectionName);

            CurrentControlSectionName = sectionName;
            CurrentControlSectionNumber = sectionNumber;

            BLOCKS.SwitchBlock("Por Omision");
            CONTLOC = CSECT_CP_BY_NAME[sectionName];
            // Registrar el bloque por omisión en la tabla de bloques de la sección
            EnsureControlSectionContainers(sectionName);
            var defaultBlock = BLOCKS.CurrentBlock;
            if (!TABBLK_BY_CSECT[sectionName].Any(b => string.Equals(b.Name, defaultBlock.Name, StringComparison.OrdinalIgnoreCase)))
                TABBLK_BY_CSECT[sectionName].Add(defaultBlock);
        }

        private List<string> ParseSymbolList(string? operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return new List<string>();

            return operand
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private void AddSymbolToCurrentSection(string name, int value, SymbolType type, bool isExternal = false)
        {
            TABSIM_EXT.AddSymbol(name, value, type, BLOCKS.CurrentBlockName, BLOCKS.CurrentBlockNumber, isExternal, CurrentControlSectionName);

            EnsureControlSectionContainers(CurrentControlSectionName);
            if (TABSIM_EXT.TryGetValue(name, out var info, CurrentControlSectionName))
                TABSIM_BY_CSECT[CurrentControlSectionName][name] = info;
        }
        
        private List<IntermediateLine> IntermediateLines = new List<IntermediateLine>();
        private List<SICXEError> Errors = new List<SICXEError>();
        private HashSet<string> ReferencedSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        
        // Diccionario para rastrear en qué línea se usa cada símbolo (para reportar errores correctamente)
        private Dictionary<string, List<int>> SymbolUsageLines = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Dictionary<string, List<int>>> SymbolUsageLinesBySection = new(StringComparer.OrdinalIgnoreCase);
        
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
        public SimbolosYExpresiones SymbolTableExtended => TABSIM_EXT;
        public IReadOnlyList<IntermediateLine> Lines => IntermediateLines;
        public IReadOnlyList<SICXEError> ErrorList => Errors;
        public int ProgramStartAddress => START_ADDRESS;
        public int ProgramSize => PROGRAM_LENGTH;
        public string ProgramName => PROGRAM_NAME;
        public int? BaseValue => BASE_VALUE;
        public int ExecutionEntryPoint => EXECUTION_ENTRY_POINT;

        public IReadOnlyDictionary<string, IReadOnlyList<BloqueInfo>> GetBlockTableBySection()
        {
            FinalizeBlocksAndRelocate();

            var snapshot = new Dictionary<string, IReadOnlyList<BloqueInfo>>(StringComparer.OrdinalIgnoreCase);
            foreach (var kv in TABBLK_BY_CSECT)
            {
                var list = kv.Value
                    .OrderBy(b => b.Number)
                    .Select(b => new BloqueInfo
                    {
                        Name = b.Name,
                        Number = b.Number,
                        StartAddress = b.StartAddress,
                        LocationCounter = b.LocationCounter,
                        Length = b.Length
                    })
                    .ToList();
                snapshot[kv.Key] = list;
            }

            return snapshot;
        }

        public IReadOnlyList<ControlSection> GetControlSections()
        {
            FinalizeBlocksAndRelocate();

            var result = new List<ControlSection>();
            foreach (var sec in CSECT_NUMBER_BY_NAME.OrderBy(k => k.Value))
            {
                string sectionName = sec.Key;
                var cs = new ControlSection
                {
                    Name = sectionName,
                    Number = sec.Value,
                    ProgramCounter = CSECT_CP_BY_NAME.TryGetValue(sectionName, out int cp) ? cp : 0,
                    Length = GetBlocksForSection(sectionName).GetAllBlocks().Sum(b => b.Length)
                };

                if (TABSIM_BY_CSECT.TryGetValue(sectionName, out var symbols))
                {
                    foreach (var s in symbols.Values)
                    {
                        cs.SymbolTable[s.Name] = new Symbol
                        {
                            Name = s.Name,
                            AddressOrValue = s.IsExternal ? null : s.Value,
                            Type = s.IsExternal ? '-' : (s.Type == SymbolType.Relative ? 'R' : 'A'),
                            BlockNumber = s.BlockNumber,
                            IsExternal = s.IsExternal
                        };
                    }
                }

                if (TABBLK_BY_CSECT.TryGetValue(sectionName, out var blocks))
                {
                    foreach (var b in blocks)
                    {
                        cs.BlockTable[b.Name] = new Block
                        {
                            Name = b.Name,
                            Number = b.Number,
                            StartAddress = b.StartAddress,
                            Length = b.Length,
                            LocationCounter = b.LocationCounter
                        };
                    }
                }

                if (EXTDEF_BY_CSECT.TryGetValue(sectionName, out var extDef))
                {
                    foreach (var symbol in extDef)
                        cs.ExternalDefinitions.Add(symbol);
                }

                if (EXTREF_BY_CSECT.TryGetValue(sectionName, out var extRef))
                {
                    foreach (var symbol in extRef)
                        cs.ExternalReferences.Add(symbol);
                }

                result.Add(cs);
            }

            return result;
        }

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

        private static bool ContieneExpresion(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return false;

            return text.Contains('*') || text.Contains('/') || text.Contains('+') ||
                   text.Contains('-') || text.Contains('(') || text.Contains(')');
        }

        /// <summary>
        /// Factory method para crear una línea intermedia base.
        /// Centraliza la creación de IntermediateLine con valores por defecto para reducir duplicación.
        /// </summary>
        /// <param name="antlrLine">Número de línea ANTLR original de la gramática</param>
        /// <param name="label">Etiqueta o símbolo (null se convierte en cadena vacía)</param>
        /// <param name="operation">Operación (instrucción o directiva)</param>
        /// <param name="operand">Operando de la instrucción o directiva</param>
        /// <param name="comment">Comentario al final de la línea</param>
        /// <returns>Nueva IntermediateLine con direccionamiento inicial CONTLOC</returns>
        private IntermediateLine CrearLineaIntermediaBase(int antlrLine, string? label, string? operation, string? operand, string? comment)
        {
            return new IntermediateLine
            {
                LineNumber = IntermediateLines.Count + 1,
                SourceLine = antlrLine,
                Address = CONTLOC,
                Label = label ?? string.Empty,
                Operation = operation ?? string.Empty,
                Operand = operand ?? string.Empty,
                Comment = comment ?? string.Empty,
                Format = 0,
                AddressingMode = "-"
            };
        }

        /// <summary>
        /// Registra un error semántico en la lista de errores del Paso 1.
        /// Unifica el reporte de errores semánticos en un único punto.
        /// </summary>
        /// <param name="antlrLine">Número de línea donde ocurrió el error</param>
        /// <param name="mensaje">Descripción del error semántico</param>
        private void AgregarErrorSemantico(int antlrLine, string mensaje)
        {
            Errors.Add(new SICXEError(antlrLine, 0, mensaje, SICXEErrorType.Semantico));
        }

        /// <summary>
        /// Intenta registrar una etiqueta en la tabla de símbolos de la sección actual.
        /// Detecta duplicados y reporta errores. Retorna true si la etiqueta se registró exitosamente.
        /// </summary>
        /// <param name="label">La etiqueta a registrar</param>
        /// <param name="antlrLine">Línea ANTLR para reportar errores</param>
        /// <param name="line">IntermediateLine que será modificada si hay error</param>
        /// <returns>true si la etiqueta se registró; false si es duplicada o inválida</returns>
        private bool RegistrarEtiquetaSiDisponible(string? label, int antlrLine, IntermediateLine line)
        {
            if (string.IsNullOrWhiteSpace(label))
                return true;

            // Verificar si el símbolo ya existe en la sección actual
            if (TABSIM_EXT.ContainsKey(label, CurrentControlSectionName))
            {
                string dupMsg = $"Símbolo duplicado: {label}";
                if (string.IsNullOrWhiteSpace(line.Error))
                    line.Error = dupMsg;
                else if (!line.Error.Contains(dupMsg))
                    line.Error += "; " + dupMsg;

                AgregarErrorSemantico(antlrLine, dupMsg);
                return false;
            }

            // Registrar el símbolo como relativo en la dirección actual (CONTLOC)
            AddSymbolToCurrentSection(label, CONTLOC, SymbolType.Relative);
            return true;
        }

        /// <summary>
        /// Adjunta un mensaje de error a una línea intermedia, evitando duplicados.
        /// Si la línea ya tiene error, lo concatena con "; " para múltiples errores.
        /// </summary>
        /// <param name="line">Línea intermedia a modificar</param>
        /// <param name="mensaje">Mensaje de error a adjuntar</param>
        private static void AdjuntarErrorALinea(IntermediateLine line, string mensaje)
        {
            if (string.IsNullOrWhiteSpace(line.Error))
                line.Error = mensaje;
            else if (!line.Error.Contains(mensaje))
                line.Error += "; " + mensaje;
        }

        /// <summary>
        /// Verifica si una operación contiene una expresión en su operando.
        /// Las expresiones incluyen operadores: +, -, *, /, paréntesis, etc.
        /// </summary>
        /// <param name="operacion">La operación a verificar</param>
        /// <param name="operando">El operando que puede contener expresión</param>
        /// <returns>true si contiene expresión; false si es un valor simple</returns>
        private static bool EsOperacionConExpresion(string operacion, string operando)
        {
            return !string.IsNullOrWhiteSpace(operacion) && ContieneExpresion(operando);
        }

        /// <summary>
        /// Detecta si una directiva WORD o BYTE contiene una expresión en su operando.
        /// Usado para determinar si se debe analizar y clasificar la expresión para modificación en Paso 2.
        /// </summary>
        /// <param name="operacion">Operación (WORD, BYTE, o variantes con + para formato 4)</param>
        /// <param name="operand">Operando de la directiva</param>
        /// <returns>true si es WORD/BYTE con expresión; false en caso contrario</returns>
        private static bool EsWordOBByteConExpresion(string operacion, string operand)
        {
            string baseOp = operacion.TrimStart('+');
            return (baseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase) ||
                    baseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase)) &&
                   ContieneExpresion(operand);
        }

        /// <summary>
        /// Detecta si una directiva ORG contiene una expresión en su operando.
        /// ORG con expresión requiere evaluación y posible generación de modificación en Paso 2.
        /// </summary>
        /// <param name="operacion">Operación (ORG o +ORG para formato 4)</param>
        /// <param name="operand">Operando de la directiva ORG</param>
        /// <returns>true si es ORG con expresión; false si es ORG con dirección simple</returns>
        private static bool EsOrgConExpresion(string operacion, string operand)
        {
            return operacion.TrimStart('+').Equals("ORG", StringComparison.OrdinalIgnoreCase) && ContieneExpresion(operand);
        }

        /// <summary>
        /// Detecta si una directiva EQU contiene una expresión en su operando.
        /// EQU asigna un símbolo a un valor calculado; requiere un label.
        /// </summary>
        /// <param name="operacion">Operación (EQU o +EQU para formato 4)</param>
        /// <param name="label">La etiqueta siendo definida por EQU</param>
        /// <param name="operand">La expresión del valor a asignar</param>
        /// <returns>true si es EQU válido con expresión; false en caso contrario</returns>
        private static bool EsEquConExpresion(string operacion, string label, string operand)
        {
            return operacion.TrimStart('+').Equals("EQU", StringComparison.OrdinalIgnoreCase) &&
                   !string.IsNullOrWhiteSpace(label) &&
                   !string.IsNullOrWhiteSpace(operand);
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
            // Núcleo del Paso 1: procesa cada línea fuente y decide:
            // 1) si entra a TABSIM, 2) cuánto incrementa CONTLOC,
            // 3) qué se registra en archivo intermedio y errores.
            CurrentLine++;
            
            // Obtener la línea real del contexto de ANTLR
            int antlrLine = context.Start?.Line ?? CurrentLine;

            // Verificar si hay error sintáctico/léxico en esta línea (usar línea de ANTLR)
            bool hasSyntaxError = HasSyntaxErrorOnLine(antlrLine);
            var lineErrors = GetErrorsForLine(antlrLine);

            var statement = context.statement();

            // Si no existe statement y tampoco hay comentario, procesar por fallback o devolver
            if (TryProcessMissingStatement(context, antlrLine, hasSyntaxError))
                return;
            
            if (TryProcessCommentOnly(context, antlrLine))
                return;

            // Extraer componentes usando el contexto de la gramática
            var label = statement.label()?.GetText();
            var operationContext = statement.operation();
            var operand = statement.operand()?.GetText();
            var comment = statement.comment()?.GetText();
            var operation = operationContext?.GetText();
            string baseOperation = (operation ?? string.Empty).TrimStart('+');
            bool isStartDirective = baseOperation.Equals("START", StringComparison.OrdinalIgnoreCase);
            bool isCsectDirective = baseOperation.Equals("CSECT", StringComparison.OrdinalIgnoreCase);
            bool isExtdefDirective = baseOperation.Equals("EXTDEF", StringComparison.OrdinalIgnoreCase);
            bool isExtrefDirective = baseOperation.Equals("EXTREF", StringComparison.OrdinalIgnoreCase);
            
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
            bool isFormat4 = operationContext?.PLUS() != null || (operation?.StartsWith("+") == true);
            int format = GetFormatFromGrammar(operationContext, isFormat4);
            // El modo de direccionamiento solo aplica a instrucciones de formato 3 o 4
            string addressingMode = (format == 3 || format == 4)
                ? DetermineAddressingModeFromGrammar(statement.operand())
                : "-";
            
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
                ControlSectionName = CurrentControlSectionName,
                ControlSectionNumber = CurrentControlSectionNumber,
                Label = label ?? "",
                Operation = operation ?? "",
                Operand = operand ?? "",
                Comment = comment ?? "",
                Format = format,
                AddressingMode = addressingMode,
                Error = errorMsg,
                BlockName = BLOCKS.CurrentBlockName,
                BlockNumber = BLOCKS.CurrentBlockNumber
            };
            
            // Registrar símbolos referenciados con el número de línea para rastrear errores.
            // USE no referencia símbolos de TABSIM: su operando es nombre de bloque.
            bool isUseDirective = operationContext?.directive()?.USE() != null ||
                                  (!string.IsNullOrEmpty(operation) && operation.Equals("USE", StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrEmpty(operand) && !isUseDirective)
                RegisterReferencedSymbols(operand, IntermediateLines.Count + 1);

            // Directiva START
            if (operationContext?.directive()?.START() != null)
            {
                PROGRAM_NAME = label ?? "NONAME";
                START_ADDRESS = ParseOperand(operand);
                SaveCurrentControlSectionState();
                string startSectionName = string.IsNullOrWhiteSpace(PROGRAM_NAME) ? "Por Omision" : PROGRAM_NAME;
                if (!CSECT_NUMBER_BY_NAME.ContainsKey(startSectionName))
                    CSECT_NUMBER_BY_NAME[startSectionName] = 0;
                EnsureControlSectionContainers(startSectionName);
                CurrentControlSectionName = startSectionName;
                CurrentControlSectionNumber = CSECT_NUMBER_BY_NAME[startSectionName];
                CSECT_CP_BY_NAME[startSectionName] = 0;
                BLOCKS.SwitchBlock("Por Omision");
                CONTLOC = 0;

                // Registrar el bloque por omisión en la tabla de bloques de la nueva sección CSECT
                EnsureControlSectionContainers(CurrentControlSectionName);
                var defBlk = BLOCKS.CurrentBlock;
                if (!TABBLK_BY_CSECT[CurrentControlSectionName].Any(b => string.Equals(b.Name, defBlk.Name, StringComparison.OrdinalIgnoreCase)))
                    TABBLK_BY_CSECT[CurrentControlSectionName].Add(defBlk);
                // Registrar el bloque por omisión en la tabla de bloques de la sección START
                EnsureControlSectionContainers(startSectionName);
                var defaultBlk = BLOCKS.CurrentBlock;
                if (!TABBLK_BY_CSECT[startSectionName].Any(b => string.Equals(b.Name, defaultBlk.Name, StringComparison.OrdinalIgnoreCase)))
                    TABBLK_BY_CSECT[startSectionName].Add(defaultBlk);
                intermediateLine2.Address = CONTLOC;
                intermediateLine2.ControlSectionName = CurrentControlSectionName;
                intermediateLine2.ControlSectionNumber = CurrentControlSectionNumber;
                ProgramStarted = true;
                AllowExternalDeclarations = true;
                
                // NOTA: La etiqueta en START es el nombre del programa, NO se inserta en TABSIM
                // según especificación SIC/XE

                intermediateLine2.SemanticValue = $"{START_ADDRESS:X4}h";
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            if (!ProgramStarted && !string.IsNullOrEmpty(operation))
            {
                string msg = "Programa debe iniciar con START";
                Errors.Add(new SICXEError(CurrentLine, 0, msg, SICXEErrorType.Semantico));
                return;
            }

            // Directiva CSECT
            if (isCsectDirective)
            {
                SaveCurrentControlSectionState();
                string newSectionName = string.IsNullOrWhiteSpace(label)
                    ? $"CSECT_{NextControlSectionNumber}"
                    : label.Trim();

                if (string.IsNullOrWhiteSpace(label))
                {
                    const string csectErr = "CSECT requiere etiqueta con nombre de sección";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? csectErr
                        : intermediateLine2.Error + "; " + csectErr;
                    Errors.Add(new SICXEError(antlrLine, 0, csectErr, SICXEErrorType.Semantico));
                }

                if (CSECT_NUMBER_BY_NAME.ContainsKey(newSectionName))
                {
                    string dupSectionErr = $"Sección de control duplicada: {newSectionName}";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? dupSectionErr
                        : intermediateLine2.Error + "; " + dupSectionErr;
                    Errors.Add(new SICXEError(antlrLine, 0, dupSectionErr, SICXEErrorType.Semantico));
                }
                else
                {
                    CSECT_NUMBER_BY_NAME[newSectionName] = NextControlSectionNumber++;
                }

                EnsureControlSectionContainers(newSectionName);
                CSECT_CP_BY_NAME[newSectionName] = 0;
                CurrentControlSectionName = newSectionName;
                CurrentControlSectionNumber = CSECT_NUMBER_BY_NAME[newSectionName];
                BLOCKS.SwitchBlock("Por Omision");
                CONTLOC = 0;

                AllowExternalDeclarations = true;

                intermediateLine2.Address = CONTLOC;
                intermediateLine2.Increment = 0;
                intermediateLine2.SemanticValue = $"CSECT={CurrentControlSectionName}, CP=0000h";
                intermediateLine2.ControlSectionName = CurrentControlSectionName;
                intermediateLine2.ControlSectionNumber = CurrentControlSectionNumber;
                SetCurrentBlockOnIntermediateLine(intermediateLine2);
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Directiva EXTDEF
            if (isExtdefDirective)
            {
                intermediateLine2.Increment = 0;

                if (!AllowExternalDeclarations)
                {
                    const string extdefPosErr = "EXTDEF solo se permite inmediatamente tras START/CSECT";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? extdefPosErr
                        : intermediateLine2.Error + "; " + extdefPosErr;
                    Errors.Add(new SICXEError(antlrLine, 0, extdefPosErr, SICXEErrorType.Semantico));
                }

                EnsureControlSectionContainers(CurrentControlSectionName);
                var symbols = ParseSymbolList(operand);
                if (symbols.Count == 0)
                {
                    const string extdefOperandErr = "EXTDEF requiere al menos un símbolo";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? extdefOperandErr
                        : intermediateLine2.Error + "; " + extdefOperandErr;
                    Errors.Add(new SICXEError(antlrLine, 0, extdefOperandErr, SICXEErrorType.Semantico));
                }
                else
                {
                    foreach (var symbol in symbols)
                    {
                        if (!EXTDEF_BY_CSECT[CurrentControlSectionName].Contains(symbol, StringComparer.OrdinalIgnoreCase))
                            EXTDEF_BY_CSECT[CurrentControlSectionName].Add(symbol);

                        if (!EXTDEF_DECL_LINES_BY_CSECT[CurrentControlSectionName].TryGetValue(symbol, out var declLines))
                        {
                            declLines = new List<int>();
                            EXTDEF_DECL_LINES_BY_CSECT[CurrentControlSectionName][symbol] = declLines;
                        }

                        if (!declLines.Contains(antlrLine))
                            declLines.Add(antlrLine);
                    }
                }

                SetCurrentBlockOnIntermediateLine(intermediateLine2);
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Directiva EXTREF
            if (isExtrefDirective)
            {
                intermediateLine2.Increment = 0;

                if (!AllowExternalDeclarations)
                {
                    const string extrefPosErr = "EXTREF solo se permite inmediatamente tras START/CSECT";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? extrefPosErr
                        : intermediateLine2.Error + "; " + extrefPosErr;
                    Errors.Add(new SICXEError(antlrLine, 0, extrefPosErr, SICXEErrorType.Semantico));
                }

                EnsureControlSectionContainers(CurrentControlSectionName);
                var symbols = ParseSymbolList(operand);
                if (symbols.Count == 0)
                {
                    const string extrefOperandErr = "EXTREF requiere al menos un símbolo";
                    intermediateLine2.Error = string.IsNullOrWhiteSpace(intermediateLine2.Error)
                        ? extrefOperandErr
                        : intermediateLine2.Error + "; " + extrefOperandErr;
                    Errors.Add(new SICXEError(antlrLine, 0, extrefOperandErr, SICXEErrorType.Semantico));
                }
                else
                {
                    foreach (var symbol in symbols)
                    {
                        if (!TABSIM_EXT.TryGetValue(symbol, out var existing, CurrentControlSectionName) || !existing.IsExternal)
                            AddSymbolToCurrentSection(symbol, -1, SymbolType.Absolute, isExternal: true);

                        if (!EXTREF_BY_CSECT[CurrentControlSectionName].Contains(symbol, StringComparer.OrdinalIgnoreCase))
                            EXTREF_BY_CSECT[CurrentControlSectionName].Add(symbol);
                    }
                }

                SetCurrentBlockOnIntermediateLine(intermediateLine2);
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Cualquier línea no-EXTDEF/EXTREF desactiva la ventana estricta de declaraciones externas
            AllowExternalDeclarations = false;
            
            // Directiva USE
            if (operationContext?.directive()?.USE() != null ||
                (!string.IsNullOrEmpty(operation) && operation.Equals("USE", StringComparison.OrdinalIgnoreCase)))
            {
                // Si la línea USE define etiqueta, se registra en el bloque actual antes del cambio
                if (!string.IsNullOrEmpty(label))
                {
                    if (TABSIM_EXT.ContainsKey(label, CurrentControlSectionName))
                    {
                        var dupErr = new SICXEError(antlrLine, 0, $"Símbolo duplicado: {label}", SICXEErrorType.Semantico);
                        Errors.Add(dupErr);
                        if (string.IsNullOrEmpty(intermediateLine2.Error))
                            intermediateLine2.Error = dupErr.Message;
                        else
                            intermediateLine2.Error += "; " + dupErr.Message;
                    }
                    else
                    {
                        AddSymbolToCurrentSection(label, CONTLOC, SymbolType.Relative);
                    }
                }

                var selectedBlock = BLOCKS.SwitchBlock(operand);
                // Registrar bloque seleccionado en la tabla de bloques de la sección actual
                EnsureControlSectionContainers(CurrentControlSectionName);
                if (!TABBLK_BY_CSECT[CurrentControlSectionName].Any(b => string.Equals(b.Name, selectedBlock.Name, StringComparison.OrdinalIgnoreCase)))
                    TABBLK_BY_CSECT[CurrentControlSectionName].Add(selectedBlock);
                // En USE se muestra el CP del bloque destino (después del cambio)
                intermediateLine2.Address = selectedBlock.LocationCounter;
                intermediateLine2.SemanticValue = $"BLQ={selectedBlock.Number}, CP={selectedBlock.LocationCounter:X4}h";
                intermediateLine2.Increment = 0;
                intermediateLine2.BlockName = selectedBlock.Name;
                intermediateLine2.BlockNumber = selectedBlock.Number;
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Directiva BASE
            if (operationContext?.directive()?.BASE() != null)
            {
                BASE_OPERAND = operand ?? "";
                if (!string.IsNullOrEmpty(operand) && TABSIM_EXT.TryGetValue(operand, out var symInfo, CurrentControlSectionName))
                    BASE_VALUE = symInfo.Value;
            }

            // Si hay error sintáctico/léxico en esta línea:
            // EXCEPCIÓN: WORD y BYTE con operandos complejos (expresiones) son válidos sintácticamente
            // solo que el parser ANTLR no puede reconocerlos. Intentamos evaluarlos de todas formas.
            if (hasSyntaxError)
            {
                // Verificar si es WORD o BYTE con expresión como operando
                bool isWordOrByteWithExpression = false;
                bool isEquWithExpression = false;
                if (!string.IsNullOrEmpty(operation))
                {
                    string baseOp = operation.TrimStart('+');
                    if ((baseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase) || 
                         baseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrEmpty(operand) &&
                        (operand.Contains('*') || operand.Contains('/') || operand.Contains('+') || 
                         operand.Contains('-') || operand.Contains('(') || operand.Contains(')')))
                    {
                        isWordOrByteWithExpression = true;
                    }
                    else if (baseOp.Equals("EQU", StringComparison.OrdinalIgnoreCase) &&
                             !string.IsNullOrEmpty(operand) &&
                             (operand.Contains('*') || operand.Contains('/') || operand.Contains('+') ||
                              operand.Contains('-') || operand.Contains('(') || operand.Contains(')')))
                    {
                        isEquWithExpression = true;
                    }
                }

                // Si NO es WORD/BYTE con expresión, retorna sin incrementar
                if (!isWordOrByteWithExpression && !isEquWithExpression)
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
                // Si es WORD/BYTE/EQU con expresión, CONTINÚA PROCESANDO (NO retorna)
                // Limpia el error sintáctico porque se va a intentar evaluar como expresión
                intermediateLine2.Error = "";
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
                    if (GetFormatFromGrammar(operationContext, false) != 3) 
                    {
                        architecturalError = "El formato 4 (+) solo aplica a instrucciones de formato 3";
                    }
                }

                if (architecturalError != null)
                {
                    intermediateLine2.Increment = 0;
                    if (string.IsNullOrEmpty(intermediateLine2.Error))
                        intermediateLine2.Error = architecturalError;
                    else
                        intermediateLine2.Error += "; " + architecturalError;
                        
                    IntermediateLines.Add(intermediateLine2);
                    Errors.Add(new SICXEError(antlrLine, 0, architecturalError, SICXEErrorType.Semantico));
                    return;
                }
            }

            // Directiva EQU
            // Requisitos 3, 4 y 7:
            // - EQU con * toma el CONTLOC (relativo)
            // - EQU con constante define simbolo absoluto
            // - EQU con expresion usa evaluador aritmetico y valida reglas de apareamiento
            if (operationContext?.directive()?.EQU() != null ||
                (!string.IsNullOrEmpty(operation) && operation.Equals("EQU", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrEmpty(label))
                {
                    if (TABSIM_EXT.ContainsKey(label, CurrentControlSectionName))
                    {
                        var dupErr = new SICXEError(antlrLine, 0, $"Símbolo duplicado: {label}", SICXEErrorType.Semantico);
                        lineErrors.Add(dupErr);
                        Errors.Add(dupErr);
                        if (string.IsNullOrEmpty(intermediateLine2.Error))
                            intermediateLine2.Error = dupErr.Message;
                        else
                            intermediateLine2.Error += "; " + dupErr.Message;
                    }
                    else
                    {
                        if (HasRelativeSymbolsFromDifferentBlocks(operand))
                        {
                            const string blockExprError = "Error: Expresión (diferentes bloques)";
                            AddSymbolToCurrentSection(label, -1, SymbolType.Absolute);
                            intermediateLine2.Error = string.IsNullOrEmpty(intermediateLine2.Error)
                                ? blockExprError
                                : intermediateLine2.Error + "; " + blockExprError;
                            intermediateLine2.SemanticValue = "FFFFh";
                            Errors.Add(new SICXEError(antlrLine, 0, blockExprError, SICXEErrorType.Semantico));

                            intermediateLine2.Increment = 0;
                            IntermediateLines.Add(intermediateLine2);
                            return;
                        }

                        // Evalua expresión para EQU y obtiene tipo (absoluto/relativo).
                        // Regla de este ensamblador: EQU NO permite referencias adelantadas;
                        // todos los símbolos usados deben estar previamente definidos en TABSIM.
                        if (TABSIM_EXT.ContainsExternalSymbol(operand, CurrentControlSectionName))
                        {
                            const string equExternalError = "EQU no permite símbolos externos";
                            AddSymbolToCurrentSection(label, -1, SymbolType.Absolute);
                            intermediateLine2.Error = string.IsNullOrEmpty(intermediateLine2.Error)
                                ? equExternalError
                                : intermediateLine2.Error + "; " + equExternalError;
                            intermediateLine2.SemanticValue = "FFFFh";
                            Errors.Add(new SICXEError(antlrLine, 0, equExternalError, SICXEErrorType.Semantico));
                            intermediateLine2.Increment = 0;
                            IntermediateLines.Add(intermediateLine2);
                            return;
                        }

                        var (evalVal, evalType, evalErr) = TABSIM_EXT.EvaluateExpression(operand, CONTLOC, allowUndefinedSymbols: false, controlSectionName: CurrentControlSectionName);
                        if (evalErr != null)
                        {
                            var errEq = new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico);
                            Errors.Add(errEq);
                            intermediateLine2.Error = string.IsNullOrEmpty(intermediateLine2.Error) ? evalErr : intermediateLine2.Error + "; " + evalErr;
                            // Asignar -1 (FFFF) para continuar con el ensamblado (Punto 6)
                            AddSymbolToCurrentSection(label, -1, SymbolType.Absolute);
                            intermediateLine2.SemanticValue = "FFFFh";
                        }
                        else
                        {
                            AddSymbolToCurrentSection(label, evalVal, evalType);
                            intermediateLine2.SemanticValue = $"{evalVal:X4}h";
                        }
                    }
                }
                intermediateLine2.Increment = 0;
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Directiva ORG: cambiar la ubicación actual a un nuevo valor
            if (operationContext?.directive()?.ORG() != null || 
                (!string.IsNullOrEmpty(operation) && operation.Equals("ORG", StringComparison.OrdinalIgnoreCase)))
            {
                if (!string.IsNullOrEmpty(operand))
                {
                    // Evaluar la expresión del operando ORG
                    var (evalVal, evalType, evalErr) = TABSIM_EXT.EvaluateExpression(operand, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                    if (evalErr != null)
                    {
                        var errOrg = new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico);
                        Errors.Add(errOrg);
                        if (string.IsNullOrEmpty(intermediateLine2.Error))
                            intermediateLine2.Error = evalErr;
                        else if (!intermediateLine2.Error.Contains(evalErr))
                            intermediateLine2.Error += "; " + evalErr;
                    }
                    else
                    {
                        // Cambiar CONTLOC al nuevo valor (ORG establece una ubicación absoluta)
                        CONTLOC = evalVal;
                        intermediateLine2.SemanticValue = $"{evalVal:X4}h";
                    }
                }
                intermediateLine2.Increment = 0;  // ORG no incrementa, reestablece el contador
                SetCurrentBlockOnIntermediateLine(intermediateLine2);
                IntermediateLines.Add(intermediateLine2);
                return;
            }

            // Directiva WORD o BYTE: intentar evaluar como expresion si es necesario
            // Requisito 9: WORD soporta expresiones en el operando
            // Verifica tanto operationContext (para casos normales) como operation (para casos con error sintáctico)
            bool isWordDirective = operationContext?.directive()?.WORD() != null;
            bool isByteDirective = operationContext?.directive()?.BYTE() != null;

            // Si no se detectó por contexto, verifica por el texto de operation extraído manualmente
            if (!isWordDirective && !isByteDirective && !string.IsNullOrEmpty(operation))
            {
                string baseOp = operation.TrimStart('+');
                isWordDirective = baseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase);
                isByteDirective = baseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase);
            }

            if (isWordDirective || isByteDirective)
            {
                // Intentar evaluar el operando como expresión (puede contener *, +, -, símbolos, etc.)
                if (!string.IsNullOrEmpty(operand))
                {
                    // Si contiene caracteres de expresión, intentar evaluarla
                    // Incluye: operadores (+, -, *, /), paréntesis, o comienza con letra (símbolo) pero no es literal C'/X'
                    bool hasExpressionOperators = operand.Contains('*') || operand.Contains('/') || operand.Contains('+') || 
                                                 operand.Contains('-') || operand.Contains('(') || operand.Contains(')');
                    bool startsWithSymbol = char.IsLetter(operand[0]) && !operand.StartsWith("C'") && !operand.StartsWith("X'");

                    if (hasExpressionOperators || startsWithSymbol)
                    {
                        var (evalVal, evalType, evalErr, evalMeta) = TABSIM_EXT.EvaluateExpressionForObject(operand, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                        if (evalErr != null)
                        {
                            var errExpr = new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico);
                            Errors.Add(errExpr);
                            if (string.IsNullOrEmpty(intermediateLine2.Error))
                                intermediateLine2.Error = evalErr;
                            else if (!intermediateLine2.Error.Contains(evalErr))
                                intermediateLine2.Error += "; " + evalErr;
                        }
                        else if (isWordDirective)
                        {
                            intermediateLine2.SemanticValue = $"{evalVal:X4}h";
                        }

                        if (evalMeta.ExternalSymbols.Count > 0)
                            intermediateLine2.ExternalReferenceSymbols = evalMeta.ExternalSymbols;
                            foreach (var sym in evalMeta.ExternalSymbols)
                            {
                                if (!TABSIM_EXT.ContainsKey(sym, CurrentControlSectionName))
                                    AddSymbolToCurrentSection(sym, -1, SymbolType.Absolute, isExternal: true);
                            }

                        if (evalMeta.HasUnpairedRelative)
                            intermediateLine2.RequiresModification = true;

                        if (isWordDirective)
                        {
                            intermediateLine2.ModificationRequests = evalMeta.ModificationRequests;
                            intermediateLine2.RelativeModuleSign = evalMeta.RelativeModuleSign;
                        }

                        if (isWordDirective)
                            intermediateLine2.HasInternalRelativeModification = evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative;
                    }
                }
            }

            // Para instrucciones con expresiones, evaluar el operando sin prefijos de modo
            // Requisito 8: expresiones en operandos de instrucciones formato 3 y 4
            if (!string.IsNullOrEmpty(operand) && 
                (operand.Contains('*') || operand.Contains('/') || operand.Contains('+') || 
                 operand.Contains('-') || operand.Contains('(') || operand.Contains(')')))
            {
                // Quitar prefijos de modo de direccionamiento si existen
                string operandForEvaluation = NormalizeOperandForExpressionEvaluation(operand);

                // Si la expresión aún contiene operadores después de quitar el prefijo
                if (operandForEvaluation.Contains('*') || operandForEvaluation.Contains('/') || operandForEvaluation.Contains('+') || 
                    operandForEvaluation.Contains('-') || operandForEvaluation.Contains('(') || operandForEvaluation.Contains(')'))
                {
                    var (evalVal, evalType, evalErr, evalMeta) = TABSIM_EXT.EvaluateExpressionForObject(operandForEvaluation, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                    if (evalErr == null && string.IsNullOrEmpty(intermediateLine2.Error))
                    {
                        intermediateLine2.SemanticValue = $"{evalVal:X4}h";
                    }

                    if (evalMeta.ExternalSymbols.Count > 0)
                        intermediateLine2.ExternalReferenceSymbols = evalMeta.ExternalSymbols;
                        foreach (var sym in evalMeta.ExternalSymbols)
                        {
                            if (!TABSIM_EXT.ContainsKey(sym, CurrentControlSectionName))
                                AddSymbolToCurrentSection(sym, -1, SymbolType.Absolute, isExternal: true);
                        }

                    if (evalMeta.ModificationRequests.Count > 0)
                    {
                        intermediateLine2.ModificationRequests = evalMeta.ModificationRequests;
                        intermediateLine2.RequiresModification = true;
                    }

                    if (evalMeta.HasUnpairedRelative)
                        intermediateLine2.RequiresModification = true;

                    if (format == 4)
                        intermediateLine2.HasInternalRelativeModification = evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative;
                }
            }

            // Formato 4 también requiere metadatos de relocalización para operandos simples
            // (ejemplo: +LDT ENDA), no solo para expresiones con operadores.
            if (format == 4 && !string.IsNullOrWhiteSpace(operand))
            {
                string operandForEvaluation = NormalizeOperandForExpressionEvaluation(operand);
                if (!string.IsNullOrWhiteSpace(operandForEvaluation))
                {
                    var (evalVal, evalType, evalErr, evalMeta) = TABSIM_EXT.EvaluateExpressionForObject(
                        operandForEvaluation,
                        CONTLOC,
                        allowUndefinedSymbols: true,
                        controlSectionName: CurrentControlSectionName);

                    if (evalErr == null)
                    {
                        if (string.IsNullOrWhiteSpace(intermediateLine2.SemanticValue))
                            intermediateLine2.SemanticValue = $"{evalVal:X4}h";

                        if (evalMeta.ExternalSymbols.Count > 0)
                            intermediateLine2.ExternalReferenceSymbols = evalMeta.ExternalSymbols;
                            foreach (var sym in evalMeta.ExternalSymbols)
                            {
                                if (!TABSIM_EXT.ContainsKey(sym, CurrentControlSectionName))
                                    AddSymbolToCurrentSection(sym, -1, SymbolType.Absolute, isExternal: true);
                            }

                        if (evalMeta.ModificationRequests.Count > 0)
                            intermediateLine2.ModificationRequests = evalMeta.ModificationRequests;

                        intermediateLine2.HasInternalRelativeModification = evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative;
                        intermediateLine2.RelativeModuleSign = evalMeta.RelativeModuleSign;
                        if (intermediateLine2.HasInternalRelativeModification)
                            intermediateLine2.RequiresModification = true;
                    }
                }
            }

            // Para otras instrucciones/directivas, si hay etiqueta y no está duplicada, insertar
            if (!string.IsNullOrEmpty(label))
            {
                if (TABSIM_EXT.ContainsKey(label, CurrentControlSectionName))
                {
                    var dupErr = new SICXEError(antlrLine, 0, $"Símbolo duplicado: {label}", SICXEErrorType.Semantico);
                    lineErrors.Add(dupErr);
                }
                else
                {
                    AddSymbolToCurrentSection(label, CONTLOC, SymbolType.Relative);
                }
            }

            // Cálculo del incremento usando la gramática
            int increment = CalculateIncrementFromGrammar(operationContext, operand, operation);
            intermediateLine2.Increment = increment;
            intermediateLine2.SemanticValue = CalculateSemanticValue(operationContext, operand);

            // Si es instrucción ejecutable válida, registrar su dirección como candidata al registro E.
            bool isInstructionLine = operationContext?.instruction() != null;
            if (isInstructionLine && !FIRST_EXECUTABLE_ADDRESS.HasValue && increment > 0)
            {
                FIRST_EXECUTABLE_ADDRESS = intermediateLine2.Address;
            }

            // Guardar el CONTLOC actual en la línea intermedio
            IntermediateLines.Add(intermediateLine2);

            // ═══════════════════════════════════════════════════════════════════════
            // DIRECTIVA END: Cierre del Programa COMPLETO
            // ═══════════════════════════════════════════════════════════════════════
            // REGLA ARQUITECTURAL SIC/XE CRÍTICA:
            // ✓ END solo puede estar en la sección PRINCIPAL (donde está START)
            // ✓ END NO puede estar en secciones CSECT - es error arquitectural
            // ✓ Cuando se encuentra END, el programa completo termina
            // ✓ Todas las secciones (PRINCIPAL y CSECT) se finalizan implícitamente
            // ✓ No hay múltiples END - solo uno en todo el programa
            //
            // El operando de END es SIEMPRE un símbolo de PRINCIPAL
            //    PRINCIPAL START 1000       ← Abre sección principal
            //       EXTDEF SYM1
            //       ...código...
            //       END ENDA                ← Cierra PROGRAMA (busca ENDA EN PRINCIPAL)
            //
            //    MOD1 CSECT                 ← Sección auxiliar (sin END)
            //       EXTREF SYM1
            //       ...código...
            //       (NO hay END aquí)       ← Error: END SOLO en PRINCIPAL
            // ═══════════════════════════════════════════════════════════════════════
            if (operationContext?.directive()?.END() != null)
            {
                SaveCurrentControlSectionState();

                // Validación arquitectural: END solo es válido en la sección PRINCIPAL
                // Nota: permitir el caso práctico donde el operando de END referencia explícitamente
                // un símbolo definido en la sección principal aunque la directiva aparezca después
                // de abrir otra CSECT; en ese caso se considera que END cierra el programa.
                string mainSection = string.IsNullOrWhiteSpace(PROGRAM_NAME) ? "Por Omision" : PROGRAM_NAME;

                if (!string.Equals(CurrentControlSectionName, mainSection, StringComparison.OrdinalIgnoreCase))
                {
                    // Si el operando de END referencia un símbolo definido en la sección PRINCIPAL,
                    // tratamos END como perteneciente a la sección principal (aceptamos y cambiamos
                    // el contexto para realizar el cierre). Si no, es error arquitectural.
                    if (!string.IsNullOrEmpty(operand) && TABSIM_EXT.TryGetValue(operand, out var entrySym, mainSection))
                    {
                        // Mover registros de uso del/los símbolos del operando desde la sección actual
                        // hacia la sección principal para que las validaciones posteriores no reporten
                        // uso externo no declarado.
                        string oldSection = CurrentControlSectionName;
                        int thisLine = intermediateLine2.LineNumber;

                        // Extraer tokens simbólicos del operando
                        var tokens = Regex.Matches(operand ?? string.Empty, "[A-Za-z_][A-Za-z0-9_]*").Cast<Match>().Select(m => m.Value).Distinct(StringComparer.OrdinalIgnoreCase);
                        foreach (var tok in tokens)
                        {
                            // Remover de la sección vieja
                            if (SymbolUsageLinesBySection.TryGetValue(oldSection, out var oldSecDict) && oldSecDict.TryGetValue(tok, out var oldList))
                            {
                                if (oldList.Contains(thisLine))
                                    oldList.Remove(thisLine);
                                // Si la lista quedó vacía, quitar la clave
                                if (oldList.Count == 0)
                                    oldSecDict.Remove(tok);
                            }

                            // Añadir a la sección principal
                            if (!SymbolUsageLinesBySection.TryGetValue(mainSection, out var mainSecDict))
                            {
                                mainSecDict = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                                SymbolUsageLinesBySection[mainSection] = mainSecDict;
                            }
                            if (!mainSecDict.TryGetValue(tok, out var mainList))
                            {
                                mainList = new List<int>();
                                mainSecDict[tok] = mainList;
                            }
                            if (!mainList.Contains(thisLine))
                                mainList.Add(thisLine);
                        }

                        // Forzar que la sección actual sea la principal para el cierre
                        CurrentControlSectionName = mainSection;
                        if (CSECT_NUMBER_BY_NAME.TryGetValue(mainSection, out var num))
                            CurrentControlSectionNumber = num;
                        else
                            CurrentControlSectionNumber = 0;
                        // Asegurar que exista un CP registrado para la sección principal
                        if (!CSECT_CP_BY_NAME.ContainsKey(mainSection))
                            CSECT_CP_BY_NAME[mainSection] = 0;
                    }
                    else
                    {
                        // ERROR: END en sección incorrecta (CSECT)
                        string endInWrongSection = $"END solo es válido en la sección principal '{mainSection}', no en '{CurrentControlSectionName}'";
                        intermediateLine2.Error = endInWrongSection;
                        Errors.Add(new SICXEError(antlrLine, 0, endInWrongSection, SICXEErrorType.Semantico));
                        IntermediateLines.Add(intermediateLine2);
                        return;
                    }
                }

                // END está en sección PRINCIPAL - obtener CP final de PRINCIPAL
                if (CSECT_CP_BY_NAME.TryGetValue(mainSection, out int endCpMain))
                {
                    intermediateLine2.Address = endCpMain;
                    intermediateLine2.BlockName = "Por Omision";
                    intermediateLine2.BlockNumber = 0;
                }

                // END finaliza TODO el Paso 1:
                // - Finaliza bloques USE de TODAS las secciones
                // - Reubica símbolos en todas las secciones a direcciones absolutas
                // - Fija tamaño total del programa (suma de todas las secciones CSECT)
                FinalizeBlocksAndRelocate();

                // Resolver punto de entrada del programa (EXECUTION_ENTRY_POINT)
                // El operando de END DEBE ser un símbolo de la sección PRINCIPAL
                if (!string.IsNullOrEmpty(operand))
                {
                    // END operando = etiqueta del punto de entrada (búsqueda EN PRINCIPAL)
                    if (TABSIM_EXT.TryGetValue(operand, out var entrySymbol, mainSection))
                    {
                        EXECUTION_ENTRY_POINT = entrySymbol.Value;
                    }
                    else
                    {
                        // Etiqueta no definida EN PRINCIPAL: marcar como inválida
                        EXECUTION_ENTRY_POINT = 0xFFFFFF;
                    }
                }
                else
                {
                    // END sin operando: usar primera instrucción ejecutable o START
                    EXECUTION_ENTRY_POINT = FIRST_EXECUTABLE_ADDRESS ?? START_ADDRESS;
                }

                // Validar EXTDEF/EXTREF de TODAS las secciones y símbolos sin definir
                ValidateExternalDefinitions();
                CheckUndefinedSymbols();
                return;
            }

            // Incrementar CONTLOC (solo si NO hay error sintáctico - ya manejado arriba)
            CONTLOC += increment;
        }

        // Helper: procesa líneas sin 'statement' parseado por ANTLR (posibles fallbacks) -> retorna true si la línea fue manejada y no continuar
        private bool TryProcessMissingStatement(SICXEParser.LineContext context, int antlrLine, bool hasSyntaxError)
        {
            // Solo procesar cuando NO exista statement ni comment; en caso contrario salir rápido.
            if (!(context.statement() == null && context.comment() == null))
                return false;

            if (context.statement() == null && context.comment() == null)
            {
                // Si hay error sintáctico, intentar extraer manualmente para procesar WORD/BYTE/ORG
                if (hasSyntaxError && SourceLines.Length >= antlrLine && !ProcessedErrorLines.Contains(antlrLine))
                {
                    ProcessedErrorLines.Add(antlrLine);  // Marcar como procesada para evitar duplicados
                    string originalLine = SourceLines[antlrLine - 1];
                    var parsedLine = ParseLineManually(originalLine);

                    // Determinar si la operación es WORD/BYTE para uso posterior
                    string parsedOpText = parsedLine.Operation ?? string.Empty;
                    string parsedBaseOp = parsedOpText.TrimStart('+');
                    bool isWordDirective = parsedBaseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase);
                    bool isByteDirective = parsedBaseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase);

                    // Detectar si es WORD, BYTE u ORG con expresión, o instrucción con expresión
                    bool isWordOrByteWithExpression = EsWordOBByteConExpresion(parsedLine.Operation ?? string.Empty, parsedLine.Operand ?? string.Empty);
                    bool isOrgWithExpression = EsOrgConExpresion(parsedLine.Operation ?? string.Empty, parsedLine.Operand ?? string.Empty);
                    bool isInstructionWithExpression = OPTAB.ContainsKey((parsedLine.Operation ?? string.Empty).TrimStart('+')) && ContieneExpresion(parsedLine.Operand);
                    bool isEquWithExpression = EsEquConExpresion(parsedLine.Operation ?? string.Empty, parsedLine.Label ?? string.Empty, parsedLine.Operand ?? string.Empty);

                    // Si es EQU con expresión en fallback por error sintáctico, procesarlo para TABSIM
                    if (isEquWithExpression)
                    {
                        string labelSafe = parsedLine.Label ?? string.Empty;
                        string operationSafe = parsedLine.Operation ?? string.Empty;
                        string operandSafe = parsedLine.Operand ?? string.Empty;
                        string commentSafe = parsedLine.Comment ?? string.Empty;
                        var intermediateLine = CrearLineaIntermediaBase(antlrLine, labelSafe, operationSafe, operandSafe, commentSafe);

                        if (TABSIM_EXT.ContainsKey(labelSafe, CurrentControlSectionName))
                        {
                            string dupMsg = $"Símbolo duplicado: {labelSafe}";
                            intermediateLine.Error = dupMsg;
                            Errors.Add(new SICXEError(antlrLine, 0, dupMsg, SICXEErrorType.Semantico));
                        }
                        else
                        {
                            if (HasRelativeSymbolsFromDifferentBlocks(operandSafe))
                            {
                                const string blockExprError = "Error: Expresión (diferentes bloques)";
                                AddSymbolToCurrentSection(labelSafe, -1, SymbolType.Absolute);
                                intermediateLine.Error = blockExprError;
                                intermediateLine.SemanticValue = "FFFFh";
                                Errors.Add(new SICXEError(antlrLine, 0, blockExprError, SICXEErrorType.Semantico));

                                intermediateLine.Increment = 0;
                                SetCurrentBlockOnIntermediateLine(intermediateLine);
                                IntermediateLines.Add(intermediateLine);
                                return true;
                            }

                            if (TABSIM_EXT.ContainsExternalSymbol(operandSafe, CurrentControlSectionName))
                            {
                                const string equExternalError = "EQU no permite símbolos externos";
                                AddSymbolToCurrentSection(labelSafe, -1, SymbolType.Absolute);
                                intermediateLine.Error = equExternalError;
                                intermediateLine.SemanticValue = "FFFFh";
                                Errors.Add(new SICXEError(antlrLine, 0, equExternalError, SICXEErrorType.Semantico));
                                intermediateLine.Increment = 0;
                                SetCurrentBlockOnIntermediateLine(intermediateLine);
                                IntermediateLines.Add(intermediateLine);
                                return true;
                            }

                            var (evalVal, evalType, evalErr) = TABSIM_EXT.EvaluateExpression(operandSafe, CONTLOC, allowUndefinedSymbols: false, controlSectionName: CurrentControlSectionName);
                            if (evalErr != null)
                            {
                                string expressionMsg = evalErr.Contains("más de un término relativo positivo", StringComparison.OrdinalIgnoreCase)
                                    ? "Error: Expresión (diferentes bloques)"
                                    : evalErr;
                                AddSymbolToCurrentSection(labelSafe, -1, SymbolType.Absolute);
                                intermediateLine.Error = expressionMsg;
                                intermediateLine.SemanticValue = "FFFFh";
                                Errors.Add(new SICXEError(antlrLine, 0, expressionMsg, SICXEErrorType.Semantico));
                            }
                            else
                            {
                                AddSymbolToCurrentSection(labelSafe, evalVal, evalType);
                                intermediateLine.SemanticValue = $"{evalVal:X4}h";
                            }
                        }

                        intermediateLine.Increment = 0;
                        SetCurrentBlockOnIntermediateLine(intermediateLine);
                        IntermediateLines.Add(intermediateLine);
                        return true;
                    }

                    // Si es ORG con expresión, procesarlo
                    if (isOrgWithExpression)
                    {
                        string labelSafe = parsedLine.Label ?? string.Empty;
                        string operationSafe = parsedLine.Operation ?? string.Empty;
                        string operandSafe = parsedLine.Operand ?? string.Empty;
                        string commentSafe = parsedLine.Comment ?? string.Empty;
                        var intermediateLine = CrearLineaIntermediaBase(antlrLine, labelSafe, operationSafe, operandSafe, commentSafe);

                        if (!string.IsNullOrEmpty(labelSafe))
                        {
                            if (TABSIM_EXT.ContainsKey(labelSafe, CurrentControlSectionName))
                            {
                                string dupMsg = $"Símbolo duplicado: {labelSafe}";
                                intermediateLine.Error = string.IsNullOrWhiteSpace(intermediateLine.Error)
                                    ? dupMsg
                                    : intermediateLine.Error + "; " + dupMsg;
                                Errors.Add(new SICXEError(antlrLine, 0, dupMsg, SICXEErrorType.Semantico));
                            }
                            else
                            {
                                AddSymbolToCurrentSection(labelSafe, CONTLOC, SymbolType.Relative);
                            }
                        }

                        // Intentar evaluar la expresión ORG
                        if (!string.IsNullOrEmpty(operandSafe))
                        {
                            var (evalVal, evalType, evalErr) = TABSIM_EXT.EvaluateExpression(operandSafe, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                            if (evalErr != null)
                            {
                                intermediateLine.Error = evalErr;
                                Errors.Add(new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico));
                            }
                            else
                            {
                                // Cambiar CONTLOC al nuevo valor
                                CONTLOC = evalVal;
                                intermediateLine.SemanticValue = $"{evalVal:X4}h";
                            }
                        }

                        intermediateLine.Increment = 0;  // ORG no incrementa
                        SetCurrentBlockOnIntermediateLine(intermediateLine);
                        IntermediateLines.Add(intermediateLine);
                        return true;
                    }

                    // Si es WORD/BYTE con expresión, procesarlo
                    if (isWordOrByteWithExpression)
                    {
                        // Crear intermediateLine similar a lo normal
                        string labelSafe = parsedLine.Label ?? string.Empty;
                        string operationSafe = parsedLine.Operation ?? string.Empty;
                        string operandSafe = parsedLine.Operand ?? string.Empty;
                        string commentSafe = parsedLine.Comment ?? string.Empty;
                        string baseOp = operationSafe.TrimStart('+');
                        var intermediateLine = CrearLineaIntermediaBase(antlrLine, labelSafe, operationSafe, operandSafe, commentSafe);

                        if (!string.IsNullOrEmpty(labelSafe))
                        {
                            if (TABSIM_EXT.ContainsKey(labelSafe, CurrentControlSectionName))
                            {
                                string dupMsg = $"Símbolo duplicado: {labelSafe}";
                                intermediateLine.Error = string.IsNullOrWhiteSpace(intermediateLine.Error)
                                    ? dupMsg
                                    : intermediateLine.Error + "; " + dupMsg;
                                Errors.Add(new SICXEError(antlrLine, 0, dupMsg, SICXEErrorType.Semantico));
                            }
                            else
                            {
                                AddSymbolToCurrentSection(labelSafe, CONTLOC, SymbolType.Relative);
                            }
                        }

                        // Intentar evaluar la expresión
                        if (!string.IsNullOrEmpty(operandSafe))
                        {
                            var (evalVal, evalType, evalErr, evalMeta) = TABSIM_EXT.EvaluateExpressionForObject(operandSafe, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                            if (evalErr == null && operationSafe.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                            {
                                intermediateLine.SemanticValue = $"{evalVal:X4}h";
                            }
                            else if (evalErr != null)
                            {
                                intermediateLine.Error = evalErr;
                                Errors.Add(new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico));
                            }

                            if (evalMeta.ExternalSymbols.Count > 0)
                            {
                                intermediateLine.ExternalReferenceSymbols = evalMeta.ExternalSymbols;
                                // Insertar símbolos externos en TABSIM si aún no existen
                                foreach (var sym in evalMeta.ExternalSymbols)
                                {
                                    if (!TABSIM_EXT.ContainsKey(sym, CurrentControlSectionName))
                                        AddSymbolToCurrentSection(sym, -1, SymbolType.Absolute, isExternal: true);
                                }
                            }

                            if (evalMeta.HasUnpairedRelative)
                                intermediateLine.RequiresModification = true;

                            if (isWordDirective)
                            {
                                intermediateLine.ModificationRequests = evalMeta.ModificationRequests;
                                intermediateLine.RelativeModuleSign = evalMeta.RelativeModuleSign;
                            }

                            if (isWordDirective)
                                intermediateLine.HasInternalRelativeModification = evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative;
                        }

                        // Calcular incremento
                        int wordByteIncrement = 0;
                        if (baseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase))
                            wordByteIncrement = CalculateByteSize(operandSafe);
                        else if (baseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                            wordByteIncrement = 3;

                        intermediateLine.Increment = wordByteIncrement;
                        SetCurrentBlockOnIntermediateLine(intermediateLine);
                        IntermediateLines.Add(intermediateLine);

                        // Incrementar CONTLOC
                        CONTLOC += wordByteIncrement;
                        return true;
                    }

                    // Si es instrucción con expresión, procesarlo
                    if (isInstructionWithExpression)
                    {
                        string labelSafe = parsedLine.Label ?? string.Empty;
                        string operationSafe = parsedLine.Operation ?? string.Empty;
                        string operandSafe = parsedLine.Operand ?? string.Empty;
                        string commentSafe = parsedLine.Comment ?? string.Empty;
                        string baseOp = operationSafe.TrimStart('+');
                        if (OPTAB.TryGetValue(baseOp, out var opCodeInfo))
                        {
                            var intermediateLine = CrearLineaIntermediaBase(antlrLine, labelSafe, operationSafe, operandSafe, commentSafe);
                            intermediateLine.Format = operationSafe.StartsWith("+", StringComparison.OrdinalIgnoreCase) ? 4 : opCodeInfo.Format;
                            intermediateLine.AddressingMode = DetermineAddressingMode(operandSafe);

                            if (!string.IsNullOrEmpty(labelSafe))
                            {
                                if (TABSIM_EXT.ContainsKey(labelSafe, CurrentControlSectionName))
                                {
                                    string dupMsg = $"Símbolo duplicado: {labelSafe}";
                                    intermediateLine.Error = string.IsNullOrWhiteSpace(intermediateLine.Error)
                                        ? dupMsg
                                        : intermediateLine.Error + "; " + dupMsg;
                                    Errors.Add(new SICXEError(antlrLine, 0, dupMsg, SICXEErrorType.Semantico));
                                }
                                else
                                {
                                    AddSymbolToCurrentSection(labelSafe, CONTLOC, SymbolType.Relative);
                                }
                            }

                            // Intentar evaluar la expresión del operando
                            if (!string.IsNullOrEmpty(operandSafe))
                            {
                                // Quitar prefijos de modo deDireccionamiento si existen
                                string operandForEvaluation = NormalizeOperandForExpressionEvaluation(operandSafe);

                                var (evalVal, evalType, evalErr, evalMeta) = TABSIM_EXT.EvaluateExpressionForObject(operandForEvaluation, CONTLOC, allowUndefinedSymbols: true, controlSectionName: CurrentControlSectionName);
                                if (evalErr != null)
                                {
                                    intermediateLine.Error = evalErr;
                                    Errors.Add(new SICXEError(antlrLine, 0, evalErr, SICXEErrorType.Semantico));
                                }
                                else
                                {
                                    intermediateLine.SemanticValue = $"{evalVal:X4}h";
                                }

                                if (evalMeta.ExternalSymbols.Count > 0)
                                {
                                    intermediateLine.ExternalReferenceSymbols = evalMeta.ExternalSymbols;
                                    foreach (var sym in evalMeta.ExternalSymbols)
                                    {
                                        if (!TABSIM_EXT.ContainsKey(sym, CurrentControlSectionName))
                                            AddSymbolToCurrentSection(sym, -1, SymbolType.Absolute, isExternal: true);
                                    }
                                }

                                if (evalMeta.ModificationRequests.Count > 0)
                                {
                                    intermediateLine.ModificationRequests = evalMeta.ModificationRequests;
                                    intermediateLine.RequiresModification = true;
                                }

                                if (operationSafe.StartsWith("+", StringComparison.OrdinalIgnoreCase))
                                {
                                    intermediateLine.HasInternalRelativeModification = evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative;
                                    intermediateLine.RelativeModuleSign = evalMeta.RelativeModuleSign;
                                    intermediateLine.RequiresModification = intermediateLine.RequiresModification || intermediateLine.HasInternalRelativeModification;
                                }
                            }

                            // Calcular incremento basado en el formato
                            int instructionIncrement = 0;
                            if (opCodeInfo.Format == 1)
                                instructionIncrement = 1;
                            else if (opCodeInfo.Format == 2)
                                instructionIncrement = 2;
                            else if (opCodeInfo.Format == 3 || opCodeInfo.Format == 4)
                            {
                                // Determinar si es formato 3 o 4 basándose en el prefijo '+'
                                instructionIncrement = operationSafe.StartsWith("+", StringComparison.OrdinalIgnoreCase) ? 4 : 3;
                            }

                            intermediateLine.Increment = instructionIncrement;
                            SetCurrentBlockOnIntermediateLine(intermediateLine);
                            IntermediateLines.Add(intermediateLine);

                            // Guardar la primera instrucción ejecutable encontrada.
                            if (!FIRST_EXECUTABLE_ADDRESS.HasValue && instructionIncrement > 0)
                            {
                                FIRST_EXECUTABLE_ADDRESS = intermediateLine.Address;
                            }

                            // Incrementar CONTLOC
                            CONTLOC += instructionIncrement;
                            return true;
                        }
                    }

                    var lineErrors = GetErrorsForLine(antlrLine);
                    var errorLine = ParseErrorLine(originalLine: SourceLines[antlrLine - 1], errors: lineErrors);
                    errorLine.SourceLine = antlrLine;
                    IntermediateLines.Add(errorLine);
                    foreach (var err in lineErrors)
                        Errors.Add(err);
                }

                // Al llegar aquí, la línea fue procesada (o al menos se registró un error); indicar handled=true
                return true;
            }
            return false;
        }

        // Helper: procesa líneas que solo contienen comentario (statement == null) -> retorna true si manejado
        private bool TryProcessCommentOnly(SICXEParser.LineContext context, int antlrLine)
        {
            if (context.statement() == null)
            {
                var commentText = context.comment()?.GetText() ?? "";
                if (string.IsNullOrWhiteSpace(commentText))
                    return true; // nothing to do, handled

                var intermediateLine = new IntermediateLine
                {
                    LineNumber = IntermediateLines.Count + 1,
                    SourceLine = antlrLine,
                    Address = -1,
                    ControlSectionName = CurrentControlSectionName,
                    ControlSectionNumber = CurrentControlSectionNumber,
                    Comment = commentText,
                    BlockName = BLOCKS.CurrentBlockName,
                    BlockNumber = BLOCKS.CurrentBlockNumber
                };
                IntermediateLines.Add(intermediateLine);
                return true;
            }
            return false;
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
                ControlSectionName = CurrentControlSectionName,
                ControlSectionNumber = CurrentControlSectionNumber,
                Label = label,
                Operation = operation,
                Operand = operand,
                Comment = comment,
                Format = 0,
                AddressingMode = "-",
                Increment = 0,  // NO incrementar CONTLOC
                Error = errorMsg,
                BlockName = BLOCKS.CurrentBlockName,
                BlockNumber = BLOCKS.CurrentBlockNumber
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
        /// - "Instrucción no existe": la operación no pertenesc al conjunto de instrucciones SIC/XE
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
        private int GetFormatFromGrammar(SICXEParser.OperationContext? operationContext, bool isFormat4)
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
        /// Parámetro adicional `operation` (string) para casos con error sintáctico donde operationContext es null
        /// </summary>
        private int CalculateIncrementFromGrammar(SICXEParser.OperationContext? operationContext, string? operand, string? operation = null)
        {
            // Política de CP: aquí se define exactamente cuánto avanza CONTLOC
            // por instrucción/directiva. Este método es la fuente de verdad del incremento.
            // Primero intenta usar operationContext (caso normal)
            if (operationContext != null)
            {
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
                    if (operationContext.PLUS() != null)
                        return 4;

                    // Usar las reglas de la gramática
                    if (instruction.format1Instruction() != null)
                        return 1;

                    if (instruction.format2Instruction() != null)
                        return 2;

                    if (instruction.format34Instruction() != null)
                        return 3;
                }
            }

            // Si operationContext es null o no hay instrucción, intenta usar operation (string)
            // Esto es para casos con error sintáctico donde se extrajo manualmente
            if (!string.IsNullOrEmpty(operation))
            {
                string baseOp = operation.TrimStart('+');
                if (baseOp.Equals("BYTE", StringComparison.OrdinalIgnoreCase))
                    return CalculateByteSize(operand);
                else if (baseOp.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                    return 3;
                else if (baseOp.Equals("RESB", StringComparison.OrdinalIgnoreCase))
                    return ParseOperand(operand);
                else if (baseOp.Equals("RESW", StringComparison.OrdinalIgnoreCase))
                    return ParseOperand(operand) * 3;
                else if (baseOp.Equals("END", StringComparison.OrdinalIgnoreCase) || 
                         baseOp.Equals("BASE", StringComparison.OrdinalIgnoreCase) ||
                         baseOp.Equals("NOBASE", StringComparison.OrdinalIgnoreCase) ||
                         baseOp.Equals("LTORG", StringComparison.OrdinalIgnoreCase))
                    return 0;
                // TODO: Agregar detección de instrucciones si es necesario
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

            // Quitar literales C'..' y X'..' para no capturar tokens internos como símbolos.
            string scanOperand = Regex.Replace(operand, "[cCxX]'[^']*'", string.Empty);

            foreach (Match m in Regex.Matches(scanOperand, "[A-Za-z_][A-Za-z0-9_]*"))
            {
                string token = m.Value;
                if (REGISTERS.Contains(token))
                {
                    EnsureControlSectionContainers(CurrentControlSectionName);
                    TABREG_BY_CSECT[CurrentControlSectionName].Add(token);
                    continue;
                }

                if (token.EndsWith("H", StringComparison.OrdinalIgnoreCase))
                    continue;

                ReferencedSymbols.Add(token);

                string sectionName = string.IsNullOrWhiteSpace(CurrentControlSectionName)
                    ? "Por Omision"
                    : CurrentControlSectionName;

                if (!SymbolUsageLinesBySection.TryGetValue(sectionName, out var sectionUsages))
                {
                    sectionUsages = new Dictionary<string, List<int>>(StringComparer.OrdinalIgnoreCase);
                    SymbolUsageLinesBySection[sectionName] = sectionUsages;
                }

                if (!sectionUsages.TryGetValue(token, out var sectionLines))
                {
                    sectionLines = new List<int>();
                    sectionUsages[token] = sectionLines;
                }
                if (!sectionLines.Contains(lineNumber))
                    sectionLines.Add(lineNumber);

                if (!SymbolUsageLines.TryGetValue(token, out var globalLines))
                {
                    globalLines = new List<int>();
                    SymbolUsageLines[token] = globalLines;
                }
                if (!globalLines.Contains(lineNumber))
                    globalLines.Add(lineNumber);
            }
        }

        /// <summary>
        /// Limpia duplicados en la lista de líneas intermedias dejando la última aparición
        /// para cada número de línea. Esto evita que la misma línea fuente aparezca
        /// repetida en el archivo intermedio/CSV cuando se insertó por varios caminos.
        /// </summary>
        public void FinalizeIntermediateLines()
        {
            IntermediateLines = IntermediateLines
                .GroupBy(l => l.LineNumber)
                .Select(g => g.Last())
                .OrderBy(l => l.LineNumber)
                .ToList();
        }
        
        /// <summary>
        /// Verifica símbolos referenciados pero no definidos
        /// Agrega el error a las líneas correspondientes del archivo intermedio
        /// </summary>
        private void CheckUndefinedSymbols()
        {
            // Validación de referencias diferidas:
            // revisa símbolos usados en operandos que nunca se definieron en TABSIM.
            var alreadyReported = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var sectionEntry in SymbolUsageLinesBySection)
            {
                string sectionName = sectionEntry.Key;

                foreach (var symbolEntry in sectionEntry.Value)
                {
                    string symbol = symbolEntry.Key;
                    if (REGISTERS.Contains(symbol))
                        continue;

                    bool existsInCurrentSection = TABSIM_EXT.ContainsKey(symbol, sectionName);
                    bool existsInAnySection = TABSIM_EXT.ContainsKey(symbol);
                    bool declaredAsExtRefInSection = EXTREF_BY_CSECT.TryGetValue(sectionName, out var extRefs) &&
                                                    extRefs.Contains(symbol, StringComparer.OrdinalIgnoreCase);

                    if (existsInCurrentSection)
                        continue;

                    if (existsInAnySection && !declaredAsExtRefInSection)
                    {
                        foreach (var lineNum in symbolEntry.Value)
                        {
                            string errorMsg = $"Uso externo no declarado: '{symbol}' debe declararse en EXTREF de la sección '{sectionName}'";
                            string key = $"{lineNum}|{errorMsg}|{SICXEErrorType.Semantico}";
                            if (alreadyReported.Add(key))
                            {
                                Errors.Add(new SICXEError(lineNum, 0, errorMsg, SICXEErrorType.Semantico));
                            }
                        }

                        continue;
                    }

                    if (!existsInAnySection)
                    {
                        foreach (var lineNum in symbolEntry.Value)
                        {
                            string errorMsg = $"[Simbolo no definido] La etiqueta '{symbol}' no esta definida en TABSIM";
                            string key = $"{lineNum}|{errorMsg}|{SICXEErrorType.Semantico}";
                            if (alreadyReported.Add(key))
                            {
                                Errors.Add(new SICXEError(lineNum, 0, errorMsg, SICXEErrorType.Semantico));
                            }
                        }
                    }
                }
            }

            // Compatibilidad: conserva recorrido global para casos viejos donde no se pudo mapear sección.
            foreach (var symbol in ReferencedSymbols)
            {
                if (TABSIM_EXT.ContainsKey(symbol) || REGISTERS.Contains(symbol))
                    continue;

                if (SymbolUsageLines.ContainsKey(symbol))
                {
                    foreach (var lineNum in SymbolUsageLines[symbol])
                    {
                        string errorMsg = $"[Simbolo no definido] La etiqueta '{symbol}' no esta definida en TABSIM";
                        string key = $"{lineNum}|{errorMsg}|{SICXEErrorType.Semantico}";
                        if (alreadyReported.Add(key))
                            Errors.Add(new SICXEError(lineNum, 0, errorMsg, SICXEErrorType.Semantico));
                    }
                }
            }
        }

        private void ValidateExternalDefinitions()
        {
            var emitted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var secEntry in EXTDEF_BY_CSECT)
            {
                string sectionName = secEntry.Key;
                var definedSymbols = TABSIM_BY_CSECT.ContainsKey(sectionName)
                    ? TABSIM_BY_CSECT[sectionName]
                    : new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);

                foreach (var symbol in secEntry.Value.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    bool existsInSection = definedSymbols.TryGetValue(symbol, out var symInfo);
                    bool invalid = !existsInSection || (symInfo != null && symInfo.IsExternal);
                    if (!invalid)
                        continue;

                    string msg = $"EXTDEF inválido: símbolo '{symbol}' no definido en sección actual";

                    var declLines = EXTDEF_DECL_LINES_BY_CSECT.ContainsKey(sectionName) &&
                                    EXTDEF_DECL_LINES_BY_CSECT[sectionName].TryGetValue(symbol, out var lines)
                        ? lines
                        : new List<int>();

                    if (declLines.Count == 0)
                    {
                        string key = $"0|{msg}|{sectionName}|{symbol}";
                        if (emitted.Add(key))
                            Errors.Add(new SICXEError(0, 0, msg, SICXEErrorType.Semantico));
                        continue;
                    }

                    foreach (int line in declLines)
                    {
                        string key = $"{line}|{msg}|{sectionName}|{symbol}";
                        if (!emitted.Add(key))
                            continue;

                        Errors.Add(new SICXEError(line, 0, msg, SICXEErrorType.Semantico));

                        var il = IntermediateLines.FirstOrDefault(l => l.SourceLine == line &&
                            string.Equals(l.Operation.TrimStart('+'), "EXTDEF", StringComparison.OrdinalIgnoreCase));
                        if (il != null)
                        {
                            if (string.IsNullOrWhiteSpace(il.Error))
                                il.Error = msg;
                            else if (!il.Error.Contains(msg, StringComparison.OrdinalIgnoreCase))
                                il.Error += "; " + msg;
                        }
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
        private int CalculateByteSize(string? operand)
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
        /// Determina el modo de direccionamiento de una instrucción basándose en el operando.
        /// Modos soportados: Inmediato (#), Indirecto (@), Indexado (,X), Simple
        /// </summary>
        private string DetermineAddressingMode(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return "Simple";

            if (operand.StartsWith("#", StringComparison.Ordinal))
                return "Inmediato";

            if (operand.StartsWith("@", StringComparison.Ordinal))
                return "Indirecto";

            if (operand.Contains(",X", StringComparison.OrdinalIgnoreCase) ||
                operand.Contains(", X", StringComparison.OrdinalIgnoreCase) ||
                operand.Replace(" ", string.Empty).Contains(",X", StringComparison.OrdinalIgnoreCase))
                return "Indexado";

            return "Simple";
        }

        private static string NormalizeOperandForExpressionEvaluation(string operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return operand;

            string value = operand.Trim();
            if (value.StartsWith("#", StringComparison.Ordinal) || value.StartsWith("@", StringComparison.Ordinal))
                value = value.Substring(1).Trim();

            string compact = value.Replace(" ", string.Empty);
            if (compact.EndsWith(",X", StringComparison.OrdinalIgnoreCase))
            {
                int commaIndex = compact.LastIndexOf(',');
                if (commaIndex > 0)
                    compact = compact.Substring(0, commaIndex);
            }

            return compact;
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
                return TABSIM_EXT.TryGetValue(operand, out var symInfo, CurrentControlSectionName) ? $"{symInfo.Value:X4}h" : operand;

            return "";
        }

        private bool HasRelativeSymbolsFromDifferentBlocks(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            var blocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in Regex.Matches(expression, "[A-Za-z_][A-Za-z0-9_]*"))
            {
                string token = match.Value;
                if (TABSIM_EXT.TryGetValue(token, out var symbolInfo, CurrentControlSectionName) && symbolInfo.Type == SymbolType.Relative)
                {
                    blocks.Add(symbolInfo.BlockName ?? "Por Omision");
                    if (blocks.Count > 1)
                        return true;
                }
            }

            return false;
        }

        private bool IsStrictInvalidEquExpression(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            if (!expression.Contains('+'))
                return false;

            foreach (Match match in Regex.Matches(expression, "[A-Za-z_][A-Za-z0-9_]*"))
            {
                string token = match.Value;
                if (TABSIM_EXT.TryGetValue(token, out var symbolInfo, CurrentControlSectionName) && symbolInfo.Type == SymbolType.Relative)
                {
                    return true;
                }
            }

            return false;
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
        private int ParseOperand(string? operand)
        {
            // Parser numérico auxiliar para directivas y valores inmediatos.
            // Soporta decimal, hex con sufijo H, prefijo 0x y X'..'.
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

        public string GenerateReport(Dictionary<int, string>? objectCodes = null, IEnumerable<SICXEError>? extraErrors = null)
        {
            // Reporte consolidado de ensamblado:
            // TABSIM + intermedio + errores unificados (Paso 1 / Paso 2).
            FinalizeBlocksAndRelocate();

            var sb = new StringBuilder();
            
            sb.AppendLine("===============================================================");
            sb.AppendLine("            PASO 1 - ENSAMBLADOR SIC/XE");
            sb.AppendLine("            ANALISIS Y ASIGNACION DE DIRECCIONES");
            sb.AppendLine("===============================================================");
            sb.AppendLine();
            
            int finalContloc = CONTLOC_FINAL > 0 ? CONTLOC_FINAL : (START_ADDRESS + PROGRAM_LENGTH);
            sb.AppendLine($"Programa        : {PROGRAM_NAME}");
            sb.AppendLine($"Dir. inicio     : {START_ADDRESS:X4}h  ({START_ADDRESS})");
            sb.AppendLine($"CONTLOC final   : {finalContloc:X4}h  ({finalContloc})");
            sb.AppendLine($"Long. programa  : {PROGRAM_LENGTH:X4}h  ({PROGRAM_LENGTH} bytes)");
            sb.AppendLine($"Total simbolos  : {TABSIM_EXT.Count}");
            if (BASE_VALUE.HasValue)
            {
                string baseDisplay = string.IsNullOrEmpty(BASE_OPERAND) ? "" : $"'{BASE_OPERAND}' -> ";
                sb.AppendLine($"Valor BASE      : {baseDisplay}{BASE_VALUE.Value:X4}h  ({BASE_VALUE.Value})  [almacenado para Paso 2]");
            }
            sb.AppendLine();
            // Imprimir tablas por sección de control
            foreach (var sec in TABSIM_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                sb.AppendLine($"=== SECCION: {sec} ===");

                sb.AppendLine("  - TABLA DE SÍMBOLOS (TABSIM)");
                sb.AppendLine($"    {"SIMBOLO",-20} | {"DIRECCION (HEX)",-18} | {"DIRECCION (DEC)",-18} | {"TIPO",-10} | {"BLOQUE",-14}");
                sb.AppendLine("    " + new string('-', 90));
                var symtab = TABSIM_BY_CSECT[sec];
                if (symtab != null && symtab.Count > 0)
                {
                    // Si todos los símbolos de la sección son externos, mostrar marcador
                    if (symtab.Values.All(s => s.IsExternal))
                    {
                        sb.AppendLine("    (Solo externos) | -    | -                | -         | -             ");
                    }
                    else
                    {
                        foreach (var kv in symtab.OrderBy(k => k.Key, StringComparer.OrdinalIgnoreCase))
                        {
                            var s = kv.Value;
                            if (s.IsExternal)
                            {
                                sb.AppendLine(string.Format("    {0,-20} | {1,-18} | {2,-18} | {3,-10} | {4,-14}", s.Name, "-", "-", "-", "-"));
                            }
                            else
                            {
                                int displayValue = s.Type == SymbolType.Relative ? s.RelativeValue : s.Value;
                                string typeDisplay = s.Type == SymbolType.Relative ? "R" : "A";
                                string bloque = $"{s.BlockName} ({s.BlockNumber})";
                                sb.AppendLine(string.Format("    {0,-20} | {1,-18} | {2,-18} | {3,-10} | {4,-14}", s.Name, (displayValue & 0xFFFF).ToString("X4") + "h", displayValue.ToString(), typeDisplay, bloque));
                            }
                        }
                    }
                }
                else
                {
                    sb.AppendLine("    (No hay simbolos definidos en esta sección)");
                }

                sb.AppendLine();
                sb.AppendLine("  - TABLA DE BLOQUES (TABBLK)");
                var blks = TABBLK_BY_CSECT.ContainsKey(sec) ? TABBLK_BY_CSECT[sec] : new List<BloqueInfo>();
                int totalSectionLength = blks?.Sum(b => b.Length) ?? 0;
                sb.AppendLine($"    Tamaño total de la sección: {totalSectionLength:X4}h ({totalSectionLength} bytes)");
                sb.AppendLine($"    {"#",-4} | {"NUM",-6} | {"NOMBRE",-12} | {"DIR_INI_HEX",-12} | {"DIR_INI_DEC",-12} | {"LONG_HEX",-10} | {"LONG_DEC",-10}");
                sb.AppendLine("    " + new string('-', 87));
                if (blks != null && blks.Count > 0)
                {
                    int row = 1;
                    foreach (var block in blks)
                    {
                        sb.AppendLine($"    {row,-4} | {block.Number,-6} | {block.Name,-12} | {block.StartAddress:X4}h{"",-7} | {block.StartAddress,-12} | {block.Length:X4}h{"",-5} | {block.Length,-10}");
                        row++;
                    }
                }
                else
                {
                    sb.AppendLine("    (No hay bloques definidos en esta sección)");
                }

                sb.AppendLine();
                sb.AppendLine("  - TABLA DE REGISTROS (USADOS EN LA SECCIÓN)");
                var regs = TABREG_BY_CSECT.ContainsKey(sec) ? TABREG_BY_CSECT[sec] : new HashSet<string>();
                if (regs != null && regs.Count > 0)
                {
                    sb.AppendLine("    Registro(s): " + string.Join(", ", regs.OrderBy(r => r, StringComparer.OrdinalIgnoreCase)));
                }
                else
                {
                    sb.AppendLine("    (No se usaron registros en esta sección)");
                }

                sb.AppendLine();
            }

            sb.AppendLine("------------------- TABLA DE BLOQUES (TABBLK) -------------------");
            sb.AppendLine($"{"CSECT",-14} | {"NUM",-6} | {"NOMBRE",-12} | {"DIR_INI_HEX",-12} | {"DIR_INI_DEC",-12} | {"LONG_HEX",-10} | {"LONG_DEC",-10}");
            sb.AppendLine(new string('-', 80));
            foreach (var sec in TABBLK_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var block in TABBLK_BY_CSECT[sec].OrderBy(b => b.Number))
                {
                    sb.AppendLine($"{sec,-14} | {block.Number,-6} | {block.Name,-12} | {block.StartAddress:X4}h{"",-7} | {block.StartAddress,-12} | {block.Length:X4}h{"",-5} | {block.Length,-10}");
                }
            }
            sb.AppendLine();

            sb.AppendLine("------------------- ARCHIVO INTERMEDIO -------------------");
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"BLQ",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"VALOR_SEM",-15} | {"FMT",-4} | {"MOD",-12} | {"MARCA",-6} | {"COD_OBJ",-12} | {"ERR"}");
            sb.AppendLine(new string('-', 150));

            foreach (var line in IntermediateLines)
            {
                string loc = (line.Address >= 0) ? $"{line.Address:X4}h" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "-";
                // Fusionar errores de la línea sin duplicarlos.
                var mensajesLinea = new List<string>();

                if (!string.IsNullOrWhiteSpace(line.Error))
                {
                    mensajesLinea.AddRange(
                        line.Error
                            .Split(';', StringSplitOptions.RemoveEmptyEntries)
                            .Select(m => m.Trim())
                    );
                }

                if (extraErrors != null)
                {
                    var lineErrors = extraErrors.Where(e => e.Line == line.SourceLine || e.Line == line.LineNumber);
                    mensajesLinea.AddRange(lineErrors.Select(e => e.Message));
                }

                string errorDisplay = string.Join("; ", mensajesLinea
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct(StringComparer.OrdinalIgnoreCase));
                
                // Truncar error si es muy largo para la consola
                if (errorDisplay.Length > 40)
                    errorDisplay = errorDisplay.Substring(0, 37) + "...";

                string codObj = "";
                if (objectCodes != null && objectCodes.TryGetValue(line.LineNumber, out var oc))
                    codObj = oc;
                string relocationMark = line.ExternalReferenceSymbols.Count > 0 ? "*SE" : (line.RequiresModification ? "*R" : "");
                
                sb.AppendLine($"{line.LineNumber,-4} | {loc,-8} | {line.BlockNumber,-8} | {line.Label,-10} | {line.Operation,-10} | {line.Operand,-15} | {line.SemanticValue,-15} | {fmt,-4} | {line.AddressingMode,-12} | {relocationMark,-6} | {codObj,-12} | {errorDisplay}");
            }
            sb.AppendLine();

            var baseErrors = extraErrors ?? Errors;
            var allErrors = baseErrors
                .GroupBy(e => new { e.Line, e.Column, e.Message, e.Type })
                .Select(g => g.First())
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();

            if (allErrors.Any())
            {
                sb.AppendLine("------------------- ERRORES DETECTADOS -------------------");
                
                // Agrupar errores por tipo
                var lexicalErrors = allErrors.Where(e => e.Type == SICXEErrorType.Lexico).OrderBy(e => e.Line);
                var syntaxErrors = allErrors.Where(e => e.Type == SICXEErrorType.Sintactico).OrderBy(e => e.Line);
                var semanticErrors = allErrors.Where(e => e.Type == SICXEErrorType.Semantico).OrderBy(e => e.Line);
                
                if (lexicalErrors.Any())
                {
                    sb.AppendLine("  [ERRORES LEXICOS]");
                    foreach (var error in lexicalErrors)
                    {
                        sb.AppendLine($"  - {error}");
                    }
                    sb.AppendLine();
                }
                
                if (syntaxErrors.Any())
                {
                    sb.AppendLine("  [ERRORES SINTACTICOS]");
                    foreach (var error in syntaxErrors)
                    {
                        sb.AppendLine($"  - {error}");
                    }
                    sb.AppendLine();
                }
                
                if (semanticErrors.Any())
                {
                    sb.AppendLine("  [ERRORES SEMANTICOS]");
                    foreach (var error in semanticErrors)
                    {
                        sb.AppendLine($"  - {error}");
                    }
                    sb.AppendLine();
                }
                
                sb.AppendLine($"  Total errores: {allErrors.Count()}");
            }

            // ═══════════════════ RESUMEN DEL ANÁLISIS ═══════════════════
            sb.AppendLine();
            sb.AppendLine("------------------- RESUMEN DEL ANALISIS -------------------");
            sb.AppendLine($"  * Tabla de simbolos (TABSIM): {TABSIM_EXT.Count} simbolo(s) definido(s)");
            sb.AppendLine($"  * Tabla de bloques (TABBLK): {BLOCKS_BY_CSECT.Values.Sum(b => b.GetAllBlocks().Count)} bloque(s)");
            sb.AppendLine($"  * Longitud del programa: {PROGRAM_LENGTH:X4}h ({PROGRAM_LENGTH} bytes)");
            if (BASE_VALUE.HasValue)
            {
                string baseSum = string.IsNullOrEmpty(BASE_OPERAND) ? "" : $"'{BASE_OPERAND}' -> ";
                sb.AppendLine($"  * BASE almacenado: {baseSum}{BASE_VALUE.Value:X4}h ({BASE_VALUE.Value}) [disponible para Paso 2]");
            }
            sb.AppendLine($"  * Archivo intermedio: {IntermediateLines.Count} linea(s) generada(s)");
                sb.AppendLine($"  * Errores detectados: {Errors.Count}");

            return sb.ToString();
        }

        /// <summary>
        /// Exporta la tabla de símbolos a un archivo CSV (salida simple, sin tipo)
        /// Nota: el archivo "TABSIMXE" con tipo se genera en ExportToSingleCSV
        /// </summary>
        public void ExportSymbolTableToCSV(string outputPath)
        {
            FinalizeBlocksAndRelocate();

            var sb = new StringBuilder();
            sb.AppendLine("SECCION,SIMBOLO,DIRECCION_HEX,DIRECCION_DEC,TIPO,BLOQUE,NUM_BLOQUE,EXTERNO");

            bool firstSection = true;
            foreach (var sec in TABSIM_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (!firstSection)
                    sb.AppendLine(",,,,,,"); // renglón en blanco entre secciones

                firstSection = false;
                var symbols = TABSIM_BY_CSECT[sec]
                    .OrderByDescending(k => k.Value.IsExternal)
                    .ThenBy(k => k.Value.Value)
                    .ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase);

                foreach (var symbol in symbols)
                {
                    // Para símbolos externos omitimos dir/valor/tipo/bloque
                    if (symbol.Value.IsExternal)
                    {
                        sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell(symbol.Value.Name)},\"-\",\"-\",\"-\",\"-\",\"-\"");
                    }
                    else
                    {
                        int displayValue = symbol.Value.Type == SymbolType.Relative ? symbol.Value.RelativeValue : symbol.Value.Value;
                        string typeDisplay = symbol.Value.Type == SymbolType.Relative ? "R" : "A";
                        sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell(symbol.Value.Name)},{(displayValue & 0xFFFF):X4},{displayValue},{typeDisplay},{FormatCSVCell(symbol.Value.BlockName)},{symbol.Value.BlockNumber}");
                    }
                }
            }
            
            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exporta el listado intermedio completo a un archivo CSV
        /// </summary>
        public void ExportIntermediateListingToCSV(string outputPath)
        {
            FinalizeBlocksAndRelocate();

            var sb = new StringBuilder();
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,BLOQUE,ETQ,CODOP,OPR,VALOR_SEM,FMT,MOD,MARCA_RELOC,ERR,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";

                string errorMsg = string.IsNullOrEmpty(line.Error)
                    ? string.Join("; ", Errors.Where(e => e.Line == line.SourceLine || e.Line == line.LineNumber).Select(e => e.Message).Distinct())
                    : line.Error;
                string relocationMark = line.ExternalReferenceSymbols.Count > 0 ? "*SE" : (line.RequiresModification ? "*R" : "");
                
                sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{FormatCSVCell(line.BlockName)},{FormatCSVCell(line.Label)},{FormatCSVCell(line.Operation)},{FormatCSVCell(line.Operand)},{FormatCSVCell(line.SemanticValue)},{fmt},{FormatCSVCell(line.AddressingMode)},{FormatCSVCell(relocationMark)},{FormatCSVCell(errorMsg)},{FormatCSVCell(line.Comment)}");
            }

            File.WriteAllText(outputPath, sb.ToString(), Encoding.UTF8);
        }

        /// <summary>
        /// Exporta un resumen del Paso 1 a CSV
        /// </summary>
        public void ExportSummaryToCSV(string outputPath)
        {
            FinalizeBlocksAndRelocate();

            var sb = new StringBuilder();
            sb.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC");
            sb.AppendLine($"NOMBRE_PROGRAMA,{PROGRAM_NAME},{PROGRAM_NAME}");
            sb.AppendLine($"DIRECCION_INICIO,{START_ADDRESS:X4},{START_ADDRESS}");
            sb.AppendLine($"LONGITUD_PROGRAMA,{PROGRAM_LENGTH:X4},{PROGRAM_LENGTH}");
            sb.AppendLine($"TOTAL_BLOQUES,,{BLOCKS_BY_CSECT.Values.Sum(b => b.GetAllBlocks().Count)}");
            sb.AppendLine($"TOTAL_SIMBOLOS,,{TABSIM_EXT.Count}");
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

            // Usar nombre base sin timestamp para exportaciones individuales
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
        /// - Columnas: NL, CONTLOC_HEX, CONTLOC_DEC, ETQ, CODOP, OPR, VALOR_SEM, FMT, MOD, ERR, COMENTARIO
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
            FinalizeBlocksAndRelocate();

            // Exportación canónica de resultados del Paso 1 en un solo CSV:
            // Sección TABSIM + Sección intermedia + Resumen.
            var sb = new StringBuilder();
            
            // UTF-8 BOM para mejor compatibilidad con Excel
            var utf8WithBom = new UTF8Encoding(true);
            
            // ═══════════════════ SECCIÓN 1: TABLA DE SÍMBOLOS (TABSIM) ═══════════════════
            sb.AppendLine("=== TABLA DE SIMBOLOS (TABSIM) ===");
            sb.AppendLine("SECCION,SIMBOLO,DIRECCION_HEX,DIRECCION_DEC,TIPO,BLOQUE,NUM_BLOQUE");

            bool firstSymSection = true;
            foreach (var sec in TABSIM_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (!firstSymSection)
                    sb.AppendLine(",,,,,,"); // renglón en blanco entre secciones

                firstSymSection = false;
                var symbols = TABSIM_BY_CSECT[sec]
                    .OrderBy(k => k.Value.Value)
                    .ThenBy(k => k.Key, StringComparer.OrdinalIgnoreCase);

                // Si la sección sólo tiene símbolos externos, añadir una línea marcador
                if (symbols.All(kv => kv.Value.IsExternal) && symbols.Count() > 0)
                {
                    sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell("(Solo externos)" )},\"-\",\"-\",\"-\",\"-\",\"\"");
                }
                else
                {
                    foreach (var symbol in symbols)
                    {
                        if (symbol.Value.IsExternal)
                        {
                            // Externos: placeholders según especificación
                            sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell(symbol.Value.Name)},\"-----\",,\"-\",\"----\",\"----\",Sí");
                        }
                        else
                        {
                            int displayValue = symbol.Value.Type == SymbolType.Relative ? symbol.Value.RelativeValue : symbol.Value.Value;
                            string typeDisplay = symbol.Value.Type == SymbolType.Relative ? "R" : "A";
                            sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell(symbol.Value.Name)},\"{(displayValue & 0xFFFF):X4}\",{displayValue},{typeDisplay},{FormatCSVCell(symbol.Value.BlockName)},{symbol.Value.BlockNumber},No");
                        }
                    }
                }
            }

            if (TABSIM_EXT.Count == 0)
                sb.AppendLine("\"\",\"(Sin símbolos)\",\"\",\"\",\"\",\"\",\"\"");

            sb.AppendLine();

            // ═══════════════════ SECCIÓN 2: TABLA DE BLOQUES (TABBLK) ═══════════════════
            sb.AppendLine("=== TABLA DE BLOQUES (TABBLK) ===");
            sb.AppendLine("SECCION,TAM_SECCION_HEX,TAM_SECCION_DEC,NUM_BLOQUE,NOMBRE,DIR_INICIO_HEX,DIR_INICIO_DEC,LONGITUD_HEX,LONGITUD_DEC");
            bool firstBlkSection = true;
            foreach (var sec in TABBLK_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (!firstBlkSection)
                    sb.AppendLine(",,,,,,,,"); // renglón en blanco entre secciones

                firstBlkSection = false;
                int totalSectionLength = TABBLK_BY_CSECT[sec].Sum(b => b.Length);
                foreach (var block in TABBLK_BY_CSECT[sec].OrderBy(b => b.Number))
                {
                    sb.AppendLine($"{FormatCSVCell(sec)},\"{totalSectionLength:X4}\",{totalSectionLength},{block.Number},{FormatCSVCell(block.Name)},\"{block.StartAddress:X4}\",{block.StartAddress},\"{block.Length:X4}\",{block.Length}");
                }
            }

            sb.AppendLine();

            // ═══════════════════ SECCIÓN 3: TABLA DE REGISTROS (TABREG) ═══════════════════
            sb.AppendLine("=== TABLA DE REGISTROS (TABREG) ===");
            sb.AppendLine("SECCION,REGISTRO");
            bool firstRegSection = true;
            foreach (var sec in TABREG_BY_CSECT.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                if (!firstRegSection)
                    sb.AppendLine(","); // renglón en blanco entre secciones

                firstRegSection = false;
                var regs = TABREG_BY_CSECT[sec].OrderBy(r => r, StringComparer.OrdinalIgnoreCase).ToList();
                if (regs.Count == 0)
                {
                    sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell("(Sin registros)")}");
                }
                else
                {
                    foreach (var reg in regs)
                        sb.AppendLine($"{FormatCSVCell(sec)},{FormatCSVCell(reg)}");
                }
            }

            sb.AppendLine();
            
            // ═══════════════════ SECCIÓN 4: ARCHIVO INTERMEDIO ═══════════════════
            sb.AppendLine("=== ARCHIVO INTERMEDIO ===");
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,BLOQUE,ETQ,CODOP,OPR,VALOR_SEM,FMT,MOD,MARCA_RELOC,ERR,COMENTARIO");
            
            foreach (var line in IntermediateLines)
            {
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";
                
                string errorMsg = line.Error ?? "";
                if (allErrors != null)
                {
                    var lineErrors = allErrors.Where(e => e.Line == line.SourceLine || e.Line == line.LineNumber);
                    string additional = string.Join("; ", lineErrors.Select(e => e.Message).Distinct());
                    if (!string.IsNullOrWhiteSpace(additional))
                        errorMsg = string.IsNullOrWhiteSpace(errorMsg) ? additional : errorMsg + "; " + additional;
                }
                string relocationMark = line.ExternalReferenceSymbols.Count > 0 ? "*SE" : (line.RequiresModification ? "*R" : "");
                
                sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{FormatCSVCell(line.BlockName)},{FormatCSVCell(line.Label)},{FormatCSVCell(line.Operation)},{FormatCSVCell(line.Operand)},{FormatCSVCell(line.SemanticValue)},{fmt},{FormatCSVCell(line.AddressingMode)},{FormatCSVCell(relocationMark)},{FormatCSVCell(errorMsg)},{FormatCSVCell(line.Comment)}");
            }

            // ═══════════════════ SECCIÓN 5: RESUMEN DEL PASO 1 ═══════════════════
            sb.AppendLine();
            sb.AppendLine("=== RESUMEN DEL PASO 1 ===");
            sb.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC,DESCRIPCION");
            int csvFinalContloc = CONTLOC_FINAL > 0 ? CONTLOC_FINAL : (START_ADDRESS + PROGRAM_LENGTH);
            sb.AppendLine($"\"NOMBRE_PROGRAMA\",\"{PROGRAM_NAME}\",\"{PROGRAM_NAME}\",\"Nombre del programa\"");
            sb.AppendLine($"\"DIR_INICIO\",\"{START_ADDRESS:X4}\",\"{START_ADDRESS}\",\"Dirección de inicio (operando de START)\"");
            sb.AppendLine($"\"CONTLOC_FINAL\",\"{csvFinalContloc:X4}\",\"{csvFinalContloc}\",\"Dirección final absoluta del programa\"");
            sb.AppendLine($"\"LONGITUD_PROGRAMA\",\"{PROGRAM_LENGTH:X4}\",\"{PROGRAM_LENGTH}\",\"Suma de longitudes de todos los bloques\"");
            sb.AppendLine($"\"TOTAL_BLOQUES\",\"\",{BLOCKS_BY_CSECT.Values.Sum(b => b.GetAllBlocks().Count)},\"Bloques definidos con USE\"");
            sb.AppendLine($"\"TOTAL_SIMBOLOS\",\"\",{TABSIM_EXT.Count},\"Símbolos definidos en TABSIM\"");
            sb.AppendLine($"\"TOTAL_LINEAS\",\"\",{IntermediateLines.Count},\"Líneas en archivo intermedio\"");
            sb.AppendLine($"\"TOTAL_ERRORES\",\"\",{(allErrors?.Count ?? Errors.Count)},\"Total de errores detectados\"");
            if (BASE_VALUE.HasValue)
            {
                sb.AppendLine($"\"VALOR_BASE\",\"{BASE_VALUE.Value:X4}\",\"{BASE_VALUE.Value}\",\"Valor almacenado de BASE (para uso en Paso 2)\"");
                sb.AppendLine($"\"BASE_VALOR\",\"\",\"{BASE_OPERAND}\",\"Operando de la directiva BASE\"");
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

        private void SetCurrentBlockOnIntermediateLine(IntermediateLine line)
        {
            line.BlockName = BLOCKS.CurrentBlockName;
            line.BlockNumber = BLOCKS.CurrentBlockNumber;
            line.ControlSectionName = CurrentControlSectionName;
            line.ControlSectionNumber = CurrentControlSectionNumber;
        }

        private void FinalizeBlocksAndRelocate()
        {
            if (BLOCKS_FINALIZED)
                return;

            // Finalizar bloques por CSECT (cada sección comienza en 0 de forma independiente)
            int mainSectionLength = 0;
            foreach (var sec in CSECT_NUMBER_BY_NAME
                .OrderBy(kv => kv.Value)
                .Select(kv => kv.Key))
            {
                var blocks = GetBlocksForSection(sec);
                int sectionLength = blocks.FinalizeBlocks(0);

                if (string.Equals(sec, PROGRAM_NAME, StringComparison.OrdinalIgnoreCase) ||
                    (CSECT_NUMBER_BY_NAME.TryGetValue(sec, out int secNum) && secNum == 0))
                {
                    mainSectionLength = sectionLength;
                }
            }

            PROGRAM_LENGTH = mainSectionLength;
            CONTLOC_FINAL = START_ADDRESS + PROGRAM_LENGTH;

            var missingBlocks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            // Reubicar símbolos relativos a direcciones absolutas
            TABSIM_EXT.RelocateRelativeSymbols((sectionName, blockName) =>
            {
                var blocks = GetBlocksForSection(sectionName);
                if (blocks.TryGetBlockStartAddress(blockName, out int startAddress))
                    return startAddress;

                string normalized = string.IsNullOrWhiteSpace(blockName) ? "Por Omision" : blockName.Trim();
                if (missingBlocks.Add(normalized))
                {
                    Errors.Add(new SICXEError(0, 0, $"Bloque no encontrado durante reubicación de símbolos: '{normalized}'", SICXEErrorType.Semantico));
                }
                return 0;
            });

            // Calcular dirección absoluta del archivo intermedio según su bloque
            // y conservar Address como CP local del bloque.
            foreach (var line in IntermediateLines)
            {
                if (line.Address >= 0)
                {
                    var blocks = GetBlocksForSection(line.ControlSectionName);
                    if (blocks.TryGetBlockStartAddress(line.BlockName, out int blockStart))
                    {
                        line.AbsoluteAddress = line.Address + blockStart;
                    }
                    else
                    {
                        line.AbsoluteAddress = line.Address;
                        string normalized = string.IsNullOrWhiteSpace(line.BlockName) ? "Por Omision" : line.BlockName.Trim();
                        if (missingBlocks.Add(normalized))
                        {
                            Errors.Add(new SICXEError(line.SourceLine, 0, $"Bloque no encontrado durante reubicación de intermedio: '{normalized}'", SICXEErrorType.Semantico));
                        }

                        string blockErr = $"Bloque no encontrado: {normalized}";
                        if (string.IsNullOrWhiteSpace(line.Error))
                            line.Error = blockErr;
                        else if (!line.Error.Contains(blockErr, StringComparison.OrdinalIgnoreCase))
                            line.Error += "; " + blockErr;
                    }
                }
                else
                {
                    line.AbsoluteAddress = -1;
                }
            }

            // Ajustar BASE si era símbolo
            if (!string.IsNullOrEmpty(BASE_OPERAND) && TABSIM_EXT.TryGetValue(BASE_OPERAND, out var baseSymbol, CurrentControlSectionName))
            {
                BASE_VALUE = baseSymbol.Value;
            }

            // Ajustar primer ejecutable después de reubicar
            var firstExecutableLine = IntermediateLines
                .FirstOrDefault(l => l.Increment > 0 && !DIRECTIVES.Contains(l.Operation.TrimStart('+').ToUpperInvariant()));
            FIRST_EXECUTABLE_ADDRESS = firstExecutableLine?.AbsoluteAddress;

            BLOCKS_FINALIZED = true;
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
        public int AbsoluteAddress { get; set; } = -1;
        public string ControlSectionName { get; set; } = "Por Omision";
        public int ControlSectionNumber { get; set; }
        public string BlockName { get; set; } = "Por Omision";
        public int BlockNumber { get; set; }
        public string Label { get; set; } = "";
        public string Operation { get; set; } = "";
        public string Operand { get; set; } = "";
        public string SemanticValue { get; set; } = "";
        public string Comment { get; set; } = "";
        public int Format { get; set; }
        public string AddressingMode { get; set; } = "-";
        public List<string> ExternalReferenceSymbols { get; set; } = new List<string>();
        public List<ModificationRequest> ModificationRequests { get; set; } = new List<ModificationRequest>();
        public bool HasInternalRelativeModification { get; set; }
        public bool RequiresModification { get; set; }
        public char? RelativeModuleSign { get; set; }
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
