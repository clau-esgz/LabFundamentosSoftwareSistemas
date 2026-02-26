using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    /// <summary>
    /// Implementa el Paso 2 del ensamblador SIC/XE de dos pasadas.
    /// 
    /// OBJETIVO DEL PASO 2:
    /// - Generar el código objeto para cada línea del archivo intermedio
    /// - Detectar errores propios del Paso 2:
    ///   · Símbolo no definido en TABSIM al resolver operando
    ///   · Desplazamiento fuera de rango para PC-relativo / BASE-relativo
    ///   · BASE no definido cuando se necesita BASE-relativo
    ///   · Registro inválido en formato 2
    ///   · Valor inmediato fuera de rango para formato 3
    /// 
    /// DATOS COMPARTIDOS CON PASO 1 (no se duplican):
    /// - Paso1.OPTAB            → opcodes y formatos (gramática: instruction)
    /// - Paso1.DIRECTIVES       → directivas (gramática: directive)
    /// - Paso1.REGISTER_NUMBERS → número de registro SIC/XE
    /// - Paso1.REGISTERS        → nombres de registros válidos
    /// 
    /// CODIFICACIÓN DE INSTRUCCIONES SIC/XE:
    /// ┌─────────┬───────────────────────────────────────────────────────────────┐
    /// │ Formato │ Estructura de bits                                           │
    /// ├─────────┼───────────────────────────────────────────────────────────────┤
    /// │    1    │ [opcode 8 bits]                                    = 1 byte  │
    /// │    2    │ [opcode 8][r1 4][r2 4]                             = 2 bytes │
    /// │    3    │ [opcode 6][n][i][x][b][p][e=0][disp 12 bits]      = 3 bytes │
    /// │    4    │ [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]  = 4 bytes │
    /// └─────────┴───────────────────────────────────────────────────────────────┘
    /// </summary>
    internal class Paso2
    {
        // ═══════════════════ DATOS DEL PASO 1 ═══════════════════
        private readonly IReadOnlyList<IntermediateLine> _lines;
        private readonly IReadOnlyDictionary<string, int> _tabSim;
        private readonly int _startAddress;
        private readonly int _programLength;
        private readonly string _programName;

        // ═══════════════════ VARIABLES DEL PASO 2 ═══════════════════
        private int? _currentBase;

        // ═══════════════════ RESULTADOS DEL PASO 2 ═══════════════════
        public List<SICXEError> Errors { get; } = new();
        public List<ObjectCodeLine> ObjectCodeLines { get; } = new();

        // ═══════════════════ CONSTRUCTOR ═══════════════════
        public Paso2(
            IReadOnlyList<IntermediateLine> lines,
            IReadOnlyDictionary<string, int> tabSim,
            int startAddress,
            int programLength,
            string programName,
            int? baseValue)
        {
            _lines = lines;
            _tabSim = tabSim;
            _startAddress = startAddress;
            _programLength = programLength;
            _programName = programName;
            _currentBase = baseValue;
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║                    MÉTODO PRINCIPAL: ObjectCodeGeneration            ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Recorre cada línea del archivo intermedio y genera su código objeto.
        /// Si una línea ya tiene error del Paso 1, no genera código objeto.
        /// Los errores nuevos del Paso 2 se agregan a la lista Errors.
        /// </summary>
        public void ObjectCodeGeneration()
        {
            foreach (var line in _lines)
            {
                string objCode = "";
                string errorPaso2 = "";

                // ── Si la línea ya tiene error del Paso 1, no generar código objeto ──
                if (!string.IsNullOrWhiteSpace(line.Error))
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", line.Error));
                    continue;
                }

                // ── Líneas de comentario o sin operación: sin código objeto ──
                if (line.Address < 0 || string.IsNullOrWhiteSpace(line.Operation))
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", ""));
                    continue;
                }

                string op = line.Operation.TrimStart('+').ToUpperInvariant();

                // ═══ DIRECTIVAS (Paso1.DIRECTIVES, gramática: directive) ═══
                if (Paso1.DIRECTIVES.Contains(op))
                {
                    (objCode, errorPaso2) = ProcessDirective(line, op);

                    // Actualizar BASE/NOBASE en tiempo real durante el recorrido
                    if (op == "BASE" && !string.IsNullOrEmpty(line.Operand))
                    {
                        if (_tabSim.TryGetValue(line.Operand, out int baseAddr))
                            _currentBase = baseAddr;
                        else if (TryParseNumeric(line.Operand, out int numVal))
                            _currentBase = numVal;
                    }
                    else if (op == "NOBASE")
                    {
                        _currentBase = null;
                    }

                    if (!string.IsNullOrEmpty(errorPaso2))
                        Errors.Add(new SICXEError(line.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));

                    ObjectCodeLines.Add(new ObjectCodeLine(line, objCode, errorPaso2));
                    continue;
                }

                // ═══ INSTRUCCIONES (Paso1.OPTAB, gramática: instruction) ═══
                if (!Paso1.OPTAB.TryGetValue(op, out var opInfo))
                {
                    errorPaso2 = $"[Paso 2] Instrucción desconocida: '{op}'";
                    Errors.Add(new SICXEError(line.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", errorPaso2));
                    continue;
                }

                // Generar código objeto según formato (gramática: format1/format2/format34)
                (objCode, errorPaso2) = line.Format switch
                {
                    1 => GenerateFormat1(opInfo),
                    2 => GenerateFormat2(line, opInfo),
                    3 => GenerateFormat3(line, opInfo),
                    4 => GenerateFormat4(line, opInfo),
                    _ => ("", "")
                };

                if (!string.IsNullOrEmpty(errorPaso2))
                    Errors.Add(new SICXEError(line.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));

                ObjectCodeLines.Add(new ObjectCodeLine(line, objCode, errorPaso2));
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  FORMATO 1 (gramática: format1Instruction → FIX|FLOAT|HIO|...)      ║
        // ║  Estructura: [opcode 8 bits] = 1 byte                               ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private (string ObjCode, string Error) GenerateFormat1(OpCodeInfo opInfo)
        {
            return (opInfo.Opcode.ToString("X2"), "");
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  FORMATO 2 (gramática: format2Instruction → ADDR|CLEAR|COMPR|...)   ║
        // ║  Estructura: [opcode 8][r1 4][r2 4] = 2 bytes                      ║
        // ║  Usa Paso1.REGISTER_NUMBERS para codificar registros                ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private (string ObjCode, string Error) GenerateFormat2(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string[] parts = operand.Split(',');
            string opName = line.Operation.TrimStart('+').ToUpperInvariant();

            int r1 = 0, r2 = 0;

            // SVC: operando es un número de interrupción
            if (opName == "SVC")
            {
                if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int svcNum))
                    r1 = svcNum;
                r2 = 0;
            }
            // CLEAR, TIXR: un solo registro
            else if (opName == "CLEAR" || opName == "TIXR")
            {
                if (parts.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))
                {
                    string err = $"[Paso 2] Registro inválido: '{parts[0].Trim()}' (válidos: {string.Join(", ", Paso1.REGISTER_NUMBERS.Keys)})";
                    return ("????", err);
                }
                r2 = 0;
            }
            // SHIFTL, SHIFTR: registro + número de posiciones (se codifica n-1)
            else if (opName == "SHIFTL" || opName == "SHIFTR")
            {
                if (parts.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))
                {
                    string err = $"[Paso 2] Registro inválido: '{parts[0].Trim()}'";
                    return ("????", err);
                }
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int shiftN))
                    r2 = shiftN - 1;  // SIC/XE codifica n-1
            }
            // Dos registros: ADDR, COMPR, DIVR, MULR, RMO, SUBR
            else
            {
                if (parts.Length >= 1)
                {
                    if (!Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))
                    {
                        string err = $"[Paso 2] Registro inválido: '{parts[0].Trim()}'";
                        return ("????", err);
                    }
                }
                if (parts.Length >= 2)
                {
                    if (!Paso1.REGISTER_NUMBERS.TryGetValue(parts[1].Trim(), out r2))
                    {
                        string err = $"[Paso 2] Registro inválido: '{parts[1].Trim()}'";
                        return ("????", err);
                    }
                }
            }

            int objCode = (opInfo.Opcode << 8) | (r1 << 4) | r2;
            return (objCode.ToString("X4"), "");
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  FORMATO 3 (gramática: format34Instruction SIN prefijo +)           ║
        // ║  Estructura: [opcode 6][n][i][x][b][p][e=0][disp 12 bits] = 3 bytes║
        // ║                                                                     ║
        // ║  MODOS DE DIRECCIONAMIENTO (gramática: operandExpr):                ║
        // ║    Simple    (n=1,i=1): operandValue directo                        ║
        // ║    Inmediato (n=0,i=1): PREFIX_IMMEDIATE operandValue (#)           ║
        // ║    Indirecto (n=1,i=0): PREFIX_INDIRECT operandValue  (@)           ║
        // ║    Indexado  (x=1):     operandValue indexing (,X)                  ║
        // ║                                                                     ║
        // ║  CÁLCULO DE DESPLAZAMIENTO:                                         ║
        // ║    1. PC-relativo: disp = target - PC  (rango -2048..2047)          ║
        // ║    2. BASE-relativo: disp = target - BASE (rango 0..4095)           ║
        // ║    3. Si ninguno funciona → ERROR de Paso 2                         ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private (string ObjCode, string Error) GenerateFormat3(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string op = line.Operation.TrimStart('+').ToUpperInvariant();

            // ── Caso especial: RSUB no tiene operando → n=1,i=1, disp=0 ──
            if (op == "RSUB")
            {
                int firstByte = (opInfo.Opcode & 0xFC) | 0x03; // n=1, i=1
                int rsub = firstByte << 16; // xbpe=0000, disp=000
                return (rsub.ToString("X6"), "");
            }

            // ── Determinar bits n, i, x según modo de direccionamiento ──
            // (El modo ya fue determinado por la gramática en Paso 1)
            int n = 1, i = 1, x = 0;
            string cleanOperand = operand;

            switch (line.AddressingMode)
            {
                case "Inmediato":   // gramática: PREFIX_IMMEDIATE operandValue
                    n = 0; i = 1;
                    cleanOperand = operand.TrimStart('#');
                    break;
                case "Indirecto":   // gramática: PREFIX_INDIRECT operandValue
                    n = 1; i = 0;
                    cleanOperand = operand.TrimStart('@');
                    break;
                case "Indexado":    // gramática: operandValue indexing (COMMA IDENT)
                    n = 1; i = 1; x = 1;
                    cleanOperand = operand.Split(',')[0].Trim();
                    break;
                default:            // Simple: operandValue sin prefijo
                    n = 1; i = 1;
                    break;
            }

            bool isImmediate = (n == 0 && i == 1);

            // ── Resolver dirección objetivo ──
            int targetAddress;

            if (TryParseNumeric(cleanOperand, out int numericValue))
            {
                // Operando es valor numérico directo
                if (isImmediate)
                {
                    // Inmediato con constante: disp = valor directo, p=0, b=0
                    if (numericValue < 0 || numericValue > 4095)
                    {
                        string err = $"[Paso 2] Valor inmediato fuera de rango para formato 3: {numericValue} (máx 4095). Use formato 4 (+)";
                        return ("??????", err);
                    }
                    int fb = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpe = (x << 3); // b=0, p=0, e=0
                    int obj = (fb << 16) | (xbpe << 12) | (numericValue & 0xFFF);
                    return (obj.ToString("X6"), "");
                }
                targetAddress = numericValue;
            }
            else
            {
                // Operando es un símbolo → buscar en TABSIM
                if (!_tabSim.TryGetValue(cleanOperand, out targetAddress))
                {
                    string err = $"[Paso 2] Símbolo no definido en TABSIM: '{cleanOperand}'";
                    return ("??????", err);
                }
            }

            // ── Calcular desplazamiento (PC-relativo o BASE-relativo) ──
            int pc = line.Address + line.Increment; // PC = dirección actual + tamaño instrucción
            int bBit = 0, pBit = 0;
            int displacement;

            // 1. Intentar PC-relativo: rango -2048 a 2047
            displacement = targetAddress - pc;
            if (displacement >= -2048 && displacement <= 2047)
            {
                pBit = 1; bBit = 0;
            }
            // 2. Intentar BASE-relativo: rango 0 a 4095
            else if (_currentBase.HasValue)
            {
                displacement = targetAddress - _currentBase.Value;
                if (displacement >= 0 && displacement <= 4095)
                {
                    bBit = 1; pBit = 0;
                }
                else
                {
                    string err = $"[Paso 2] Desplazamiento fuera de rango para formato 3 " +
                                 $"(PC-rel: {targetAddress - pc}, BASE-rel: {displacement}). Use formato 4 (+)";
                    return ("??????", err);
                }
            }
            else
            {
                string err = $"[Paso 2] Desplazamiento fuera de rango para PC-relativo ({displacement}) y BASE no definido";
                return ("??????", err);
            }

            // ── Construir código objeto ──
            {
                int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | (bBit << 2) | (pBit << 1) | 0; // e=0
                int objCode = (firstByte << 16) | (xbpe << 12) | (displacement & 0xFFF);
                return (objCode.ToString("X6"), "");
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  FORMATO 4 (gramática: FORMAT4_PREFIX instruction → +LDA, +JSUB)    ║
        // ║  Estructura: [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]      ║
        // ║  Dirección ABSOLUTA de 20 bits, sin PC/BASE relativo.               ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private (string ObjCode, string Error) GenerateFormat4(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string op = line.Operation.TrimStart('+').ToUpperInvariant();

            // ── Caso especial: +RSUB ──
            if (op == "RSUB")
            {
                int firstByte = (opInfo.Opcode & 0xFC) | 0x03; // n=1, i=1
                int xbpe = 1; // e=1
                int rsub = (firstByte << 24) | (xbpe << 20); // addr=0
                return (rsub.ToString("X8"), "");
            }

            // ── Determinar bits n, i, x ──
            int n = 1, i = 1, x = 0;
            string cleanOperand = operand;

            switch (line.AddressingMode)
            {
                case "Inmediato":
                    n = 0; i = 1;
                    cleanOperand = operand.TrimStart('#');
                    break;
                case "Indirecto":
                    n = 1; i = 0;
                    cleanOperand = operand.TrimStart('@');
                    break;
                case "Indexado":
                    n = 1; i = 1; x = 1;
                    cleanOperand = operand.Split(',')[0].Trim();
                    break;
                default:
                    n = 1; i = 1;
                    break;
            }

            // ── Resolver dirección objetivo ──
            int targetAddress;

            if (TryParseNumeric(cleanOperand, out int numericValue))
            {
                targetAddress = numericValue;
            }
            else
            {
                if (!_tabSim.TryGetValue(cleanOperand, out targetAddress))
                {
                    string err = $"[Paso 2] Símbolo no definido en TABSIM: '{cleanOperand}'";
                    return ("????????", err);
                }
            }

            // ── Construir código objeto: dirección absoluta 20 bits, e=1 ──
            {
                int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                int objCode = (firstByte << 24) | (xbpe << 20) | (targetAddress & 0xFFFFF);
                return (objCode.ToString("X8"), "");
            }
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  DIRECTIVAS CON CÓDIGO OBJETO (Paso1.DIRECTIVES, gramática: directive)║
        // ║  Solo BYTE y WORD generan código objeto                              ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        private (string ObjCode, string Error) ProcessDirective(IntermediateLine line, string op)
        {
            return op switch
            {
                "BYTE" => GenerateByteObjCode(line.Operand),
                "WORD" => GenerateWordObjCode(line.Operand),
                _ => ("", "") // START, END, BASE, NOBASE, RESB, RESW, etc.
            };
        }

        /// <summary>
        /// BYTE C'texto' → código ASCII concatenado (gramática: CHARCONST → C'...')
        /// BYTE X'hex'   → valor hexadecimal directo (gramática: HEXCONST → X'...')
        /// </summary>
        private (string ObjCode, string Error) GenerateByteObjCode(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return ("", "[Paso 2] Operando vacío para directiva BYTE");

            // gramática: CHARCONST : C '\'' ~[\r\n']+ '\'' ;
            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3);
                var sb = new StringBuilder();
                foreach (char c in content)
                    sb.Append(((int)c).ToString("X2"));
                return (sb.ToString(), "");
            }

            // gramática: HEXCONST : X '\'' [0-9A-Fa-f]+ '\'' ;
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3).ToUpperInvariant();
                return (content, "");
            }

            return ("", $"[Paso 2] Formato de operando inválido para BYTE: '{operand}'");
        }

        /// <summary>
        /// WORD n → valor entero en 24 bits (6 dígitos hex)
        /// Ejemplo: WORD 3 → 000003
        /// </summary>
        private (string ObjCode, string Error) GenerateWordObjCode(string operand)
        {
            if (TryParseNumeric(operand, out int value))
                return ((value & 0xFFFFFF).ToString("X6"), "");

            return ("", $"[Paso 2] Operando inválido para directiva WORD: '{operand}'");
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  UTILIDADES                                                          ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        /// <summary>
        /// Intenta parsear un operando como valor numérico.
        /// Soporta formatos de la gramática:
        ///   NUMBER    : [0-9]+                          → decimal
        ///   HEXNUMBER : [0-9][0-9A-Fa-f]* H            → hex con sufijo H
        ///   HEXCONST  : X '\'' [0-9A-Fa-f]+ '\''       → X'...'
        /// </summary>
        private bool TryParseNumeric(string operand, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(operand)) return false;

            operand = operand.Trim().TrimStart('#', '@');
            if (string.IsNullOrEmpty(operand)) return false;

            // gramática: HEXNUMBER → sufijo H
            if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = operand[..^1];
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            // Prefijo 0x (compatibilidad)
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(operand[2..], NumberStyles.HexNumber, null, out value);

            // gramática: HEXCONST → X'...'
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string hexPart = operand.Substring(2, operand.Length - 3);
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            // gramática: NUMBER → decimal
            return int.TryParse(operand, out value);
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  REPORTE EN CONSOLA                                                  ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        public string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PASO 2 - ENSAMBLADOR SIC/XE                          ║");
            sb.AppendLine("║              GENERACIÓN DE CÓDIGO OBJETO                          ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            sb.AppendLine($"Programa        : {_programName}");
            sb.AppendLine($"Dir. inicio     : {_startAddress:X4}h  ({_startAddress})");
            sb.AppendLine($"Long. programa  : {_programLength:X4}h  ({_programLength} bytes)");
            if (_currentBase.HasValue)
                sb.AppendLine($"Valor BASE      : {_currentBase.Value:X4}h  ({_currentBase.Value})");
            sb.AppendLine();

            sb.AppendLine("═══════════════════ ARCHIVO INTERMEDIO CON CÓDIGO OBJETO ═══════════");
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"FMT",-4} | {"MOD",-12} | {"COD_OBJ",-12} | {"ERR"}");
            sb.AppendLine(new string('─', 135));

            foreach (var objLine in ObjectCodeLines)
            {
                var line = objLine.IntermLine;
                string loc = (line.Address >= 0) ? $"{line.Address:X4}h" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "-";

                string errorDisplay = CombineErrors(line.Error, objLine.ErrorPaso2);
                if (errorDisplay.Length > 45)
                    errorDisplay = errorDisplay[..42] + "...";

                sb.AppendLine($"{line.LineNumber,-4} | {loc,-8} | {line.Label,-10} | {line.Operation,-10} | {line.Operand,-15} | {fmt,-4} | {line.AddressingMode,-12} | {objLine.ObjectCode,-12} | {errorDisplay}");
            }
            sb.AppendLine();

            if (Errors.Count > 0)
            {
                sb.AppendLine("═══════════════════ ERRORES DEL PASO 2 ═════════════════════════");
                foreach (var error in Errors.OrderBy(e => e.Line))
                {
                    sb.AppendLine($"  • {error}");
                }
                sb.AppendLine($"\n  Total errores Paso 2: {Errors.Count}");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════ RESUMEN DEL PASO 2 ═════════════════════════");
            int linesWithObj = ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ObjectCode));
            int linesWithError = ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ErrorPaso2));
            sb.AppendLine($"  * Líneas procesadas        : {ObjectCodeLines.Count}");
            sb.AppendLine($"  * Líneas con código objeto  : {linesWithObj}");
            sb.AppendLine($"  * Líneas con error Paso 2   : {linesWithError}");
            sb.AppendLine($"  * Errores del Paso 2        : {Errors.Count}");

            return sb.ToString();
        }

        // ╔═══════════════════════════════════════════════════════════════════════╗
        // ║  EXPORTACIÓN A CSV                                                   ║
        // ╚═══════════════════════════════════════════════════════════════════════╝

        public void ExportToCSV(string outputPath)
        {
            var sb = new StringBuilder();
            var utf8WithBom = new UTF8Encoding(true);

            sb.AppendLine("=== PASO 2 - CODIGO OBJETO ===");
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,COD_OBJ,ERR_PASO1,ERR_PASO2,COMENTARIO");

            foreach (var objLine in ObjectCodeLines)
            {
                var line = objLine.IntermLine;
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";

                sb.AppendLine(string.Join(",",
                    line.LineNumber,
                    addressHex,
                    addressDec,
                    FormatCSVCell(line.Label),
                    FormatCSVCell(line.Operation),
                    FormatCSVCell(line.Operand),
                    fmt,
                    FormatCSVCell(line.AddressingMode),
                    FormatCSVCell(objLine.ObjectCode),
                    FormatCSVCell(line.Error),
                    FormatCSVCell(objLine.ErrorPaso2),
                    FormatCSVCell(line.Comment)));
            }

            sb.AppendLine();
            sb.AppendLine("=== RESUMEN DEL PASO 2 ===");
            sb.AppendLine("PROPIEDAD,VALOR");
            sb.AppendLine($"\"NOMBRE_PROGRAMA\",\"{_programName}\"");
            sb.AppendLine($"\"DIR_INICIO\",\"{_startAddress:X4}\"");
            sb.AppendLine($"\"LONGITUD_PROGRAMA\",\"{_programLength:X4}\"");
            sb.AppendLine($"\"LINEAS_CON_CODIGO\",{ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ObjectCode))}");
            sb.AppendLine($"\"ERRORES_PASO2\",{Errors.Count}");

            File.WriteAllText(outputPath, sb.ToString(), utf8WithBom);
        }

        private string CombineErrors(string errPaso1, string errPaso2)
        {
            if (string.IsNullOrEmpty(errPaso1) && string.IsNullOrEmpty(errPaso2)) return "";
            if (string.IsNullOrEmpty(errPaso1)) return errPaso2;
            if (string.IsNullOrEmpty(errPaso2)) return errPaso1;
            return errPaso1 + "; " + errPaso2;
        }

        private string FormatCSVCell(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            value = value.Replace("\"", "\"\"");
            if (value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("=") || value.StartsWith("@"))
                value = "'" + value;
            return $"\"{value}\"";
        }
    }

    /// <summary>
    /// Envuelve una IntermediateLine del Paso 1 con su código objeto
    /// y errores generados en el Paso 2, sin modificar los datos originales.
    /// </summary>
    public class ObjectCodeLine
    {
        public IntermediateLine IntermLine { get; }
        public string ObjectCode { get; }
        public string ErrorPaso2 { get; }

        public ObjectCodeLine(IntermediateLine intermLine, string objectCode, string errorPaso2)
        {
            IntermLine = intermLine;
            ObjectCode = objectCode;
            ErrorPaso2 = errorPaso2;
        }
    }
}
