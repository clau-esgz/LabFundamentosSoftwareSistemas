using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    /// <summary>
    /// Paso 2 del ensamblador SIC/XE.
    /// Genera codigo objeto (formatos 1-4), procesa directivas y reporta errores.
    /// </summary>
    internal class Paso2
    {
        //paso1
        private readonly IReadOnlyList<IntermediateLine> _lineasIntermedias;
        private readonly SimbolosYExpresiones _tablaSimbolos;
        private readonly int _direccionInicio;
        private readonly int _longitudPrograma;
        private readonly string _nombrePrograma;
        private readonly int _direccionEjecucion;

        private int? _baseActual; //Valor actual de BASE, se actualiza al procesar directivas BASE/NOBASE

        /// RESULTADOS DEL PASO 2:
        public List<SICXEError> Errors { get; } = new();                   // Lista de errores semanticos
        public List<ObjectCodeLine> ObjectCodeLines { get; } = new();      // Lineas con codigo objeto generado
        public List<ObjectModule> GeneratedModules { get; } = new();       // Módulos H/D/R/T/M/E por CSECT

        private readonly Dictionary<string, ModuleBuilderState> _moduleStates = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Constructor: recibe datos compartidos del Paso 1
        /// </summary>
        public Paso2(
            IReadOnlyList<IntermediateLine> lineas,
            SimbolosYExpresiones tablaSimExt,
            int dirInicio,
            int longPrograma,
            string nombrePrograma,
            int? valorBase,
            int direccionEjecucion)
        {
            _lineasIntermedias = lineas;
            _tablaSimbolos = tablaSimExt;
            _direccionInicio = dirInicio;
            _longitudPrograma = longPrograma;
            _nombrePrograma = nombrePrograma;
            _baseActual = valorBase;
            _direccionEjecucion = direccionEjecucion;
        }

        /// <summary>
        /// Metodo principal del Paso 2.
        /// Recorre el intermedio, genera codigo objeto y registra errores por linea.
        /// </summary>
        public void ObjectCodeGeneration()
        {
            GeneratedModules.Clear();
            _moduleStates.Clear();
            InitializeModuleBuilders();

            foreach (var linea in _lineasIntermedias)
            {
                ProcesarLineaIntermedia(linea);
            }

            FinalizeModuleBuilders();
        }

        /// <summary>
        /// Procesa una línea intermedia del Paso 1: genera código objeto o procesa directivas.
        /// Ruta central que determina si es una instrucción, directiva, o línea ignorable.
        /// </summary>
        private void ProcesarLineaIntermedia(IntermediateLine linea)
        {
            if (EsLineaIgnorable(linea))
            {
                RegistrarSalidaLinea(linea, string.Empty, string.Empty);
                return;
            }

            string operacion = ObtenerOperacionNormalizada(linea);

            if (Paso1.DIRECTIVES.Contains(operacion))
            {
                ProcesarDirectiva(linea, operacion);
                return;
            }

            if (!Paso1.OPTAB.TryGetValue(operacion, out var infoOp))
            {
                RegistrarErrorYSalida(linea, string.Empty, "Error: Instrucción desconocida");
                return;
            }

            var (codigoObjeto, errorPaso2) = GenerarCodigoObjeto(linea, infoOp);
            RegistrarErrorYSalida(linea, codigoObjeto, errorPaso2);
        }

        /// <summary>
        /// Determina si una linea debe ignorarse (comentarios, sin operacion, sin direccion valida).
        /// </summary>
        private static bool EsLineaIgnorable(IntermediateLine linea)
        {
            return linea.Address < 0 || string.IsNullOrWhiteSpace(linea.Operation);
        }

        /// <summary>
        /// Obtiene operacion normalizada: elimina prefijo + (formato 4) y convierte a mayusculas.
        /// </summary>
        private static string ObtenerOperacionNormalizada(IntermediateLine linea)
            => (linea.Operation ?? string.Empty).TrimStart('+').ToUpperInvariant();

        /// <summary>
        /// Procesa directivas (BYTE, WORD, END, BASE, NOBASE, ORG, etc.).
        /// BASE/NOBASE actualizan el registro BASE para direccionamiento relativo a base.
        /// </summary>
        private void ProcesarDirectiva(IntermediateLine linea, string operacion)
        {
            var (codigoObjeto, errorPaso2) = ProcessDirective(linea, operacion);

            if (operacion == "BASE" && !string.IsNullOrEmpty(linea.Operand))
            {
                if (_tablaSimbolos.TryGetValue(linea.Operand, out var symInfo, linea.ControlSectionName))
                    _baseActual = symInfo.Value;
                else if (TryParseNumeric(linea.Operand, out int numVal))
                    _baseActual = numVal;
            }
            else if (operacion == "NOBASE")
            {
                _baseActual = null;
            }

            RegistrarErrorYSalida(linea, codigoObjeto, errorPaso2);
        }

        /// <summary>
        /// Genera codigo objeto para una instruccion segun su formato (1, 2, 3 o 4 bytes).
        /// Enruta a generadores especializados por formato de instruccion.
        /// </summary>
        private (string ObjCode, string Error) GenerarCodigoObjeto(IntermediateLine linea, OpCodeInfo infoOp)
        {
            return linea.Format switch
            {
                1 => GenerateFormat1(infoOp),
                2 => GenerateFormat2(linea, infoOp),
                3 => GenerateFormat3(linea, infoOp),
                4 => GenerateFormat4(linea, infoOp),
                _ => ("", "")
            };
        }

        /// <summary>
        /// Registra un error semantico y la salida de la linea en los contenedores de resultados.
        /// Central para capturar errores de Paso 2 (codigo objeto invalido, simbolos no resueltos, etc.).
        /// </summary>
        private void RegistrarErrorYSalida(IntermediateLine linea, string codigoObjeto, string errorPaso2)
        {
            if (!string.IsNullOrEmpty(errorPaso2))
                Errors.Add(new SICXEError(linea.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));

            RegistrarSalidaLinea(linea, codigoObjeto, errorPaso2);
        }

        /// <summary>
        /// Registra la salida de una linea en el contenedor de lineas de codigo objeto.
        /// Tambien invoca notificacion al modulo constructor para acumular modulos.
        /// </summary>
        private void RegistrarSalidaLinea(IntermediateLine linea, string codigoObjeto, string errorPaso2)
        {
            ObjectCodeLines.Add(new ObjectCodeLine(linea, codigoObjeto, errorPaso2));
            RegisterObjectEvent(linea, codigoObjeto);
        }

        private void InitializeModuleBuilders()
        {
            static int Addr(IntermediateLine l) => l.AbsoluteAddress >= 0 ? l.AbsoluteAddress : l.Address;

            var groups = _lineasIntermedias
                .Where(l => !string.IsNullOrWhiteSpace(l.ControlSectionName))
                .GroupBy(l => (Name: l.ControlSectionName, Number: l.ControlSectionNumber))
                .OrderBy(g => g.Key.Number)
                .ThenBy(g => g.Min(x => x.LineNumber))
                .ToList();

            foreach (var group in groups)
            {
                string sectionName = string.IsNullOrWhiteSpace(group.Key.Name) ? "Por Omision" : group.Key.Name;
                var lines = group.OrderBy(l => l.LineNumber).ToList();

                int sectionStart = lines
                    .Where(l => Addr(l) >= 0)
                    .Select(Addr)
                    .DefaultIfEmpty(0)
                    .Min();

                int sectionEnd = lines
                    .Where(l => Addr(l) >= 0)
                    .Select(l => Addr(l) + Math.Max(0, l.Increment))
                    .DefaultIfEmpty(sectionStart)
                    .Max();

                var module = new ObjectModule
                {
                    Name = sectionName,
                    StartAddress = sectionStart,
                    Length = Math.Max(0, sectionEnd - sectionStart)
                };

                var state = new ModuleBuilderState(module, sectionName, group.Key.Number);
                _moduleStates[sectionName] = state;

                var extDefSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var extRefSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var line in lines)
                {
                    string op = (line.Operation ?? string.Empty).Trim().TrimStart('+').ToUpperInvariant();
                    if (op == "EXTDEF")
                    {
                        foreach (var symbol in ParseSymbolList(line.Operand))
                            extDefSymbols.Add(symbol);
                    }
                    else if (op == "EXTREF")
                    {
                        foreach (var symbol in ParseSymbolList(line.Operand))
                            extRefSymbols.Add(symbol);
                    }
                }

                foreach (var symbol in extDefSymbols)
                {
                    var defLine = lines.FirstOrDefault(l => string.Equals(l.Label, symbol, StringComparison.OrdinalIgnoreCase));
                    int relAddr = defLine == null ? 0 : Math.Max(0, Addr(defLine) - sectionStart);
                    module.D.Add(new DefineRecord(symbol, relAddr));
                }

                foreach (var symbol in extRefSymbols)
                    module.R.Add(new ReferRecord(symbol));
            }
        }

        private void RegisterObjectEvent(IntermediateLine line, string objectCode)
        {
            string sectionName = string.IsNullOrWhiteSpace(line.ControlSectionName) ? "Por Omision" : line.ControlSectionName;
            if (!_moduleStates.TryGetValue(sectionName, out var state))
                return;

            string op = ObtenerOperacionNormalizada(line);
            if (EsDirectivaDeCorte(op))
                state.FlushText();

            if (!string.IsNullOrWhiteSpace(objectCode))
            {
                ProcesarTextoYModificaciones(line, objectCode, state, op);

                if (!state.FirstExecutableAddress.HasValue && line.Format > 0)
                    state.FirstExecutableAddress = GetEffectiveAddress(line);
            }
        }

        /// <summary>
        /// Determina si una operacion es directiva de corte que vacia el buffer de texto.
        /// Directivas de corte: RESB, RESW, USE, CSECT, ORG, END.
        /// </summary>
        private static bool EsDirectivaDeCorte(string op)
            => op is "RESB" or "RESW" or "USE" or "CSECT" or "ORG" or "END";

        /// <summary>
        /// Procesa el codigo objeto y modificaciones de una linea.
        /// Accumula el codigo en el buffer de texto y registra si hay modificaciones necesarias.
        /// </summary>
        private void ProcesarTextoYModificaciones(IntermediateLine line, string objectCode, ModuleBuilderState state, string op)
        {
            string cleanedObjectCode = objectCode.Replace("*", "");
            int address = GetEffectiveAddress(line);
            state.AppendText(address, cleanedObjectCode);

            if (!EsLineaConModificacion(line, op))
                return;

            ProcesarModificacionesDeLinea(line, state, address);
        }

        /// <summary>
        /// Determina si una linea requiere registro de modificacion (relocation record).
        /// WORD y formato 4 (direccionamiento extendido) generan M records.
        /// </summary>
        private static bool EsLineaConModificacion(IntermediateLine line, string op)
            => op == "WORD" || line.Format == 4;

        /// <summary>
        /// Procesa modificaciones (relocation records) para una linea.
        /// Evalua expresiones y crea M records para el modulo objeto.
        /// Maneja tanto WORD como formato 4 (direccionamiento extendido).
        /// </summary>
        private void ProcesarModificacionesDeLinea(IntermediateLine line, ModuleBuilderState state, int address)
        {
            bool isWord = string.Equals(line.Operation, "WORD", StringComparison.OrdinalIgnoreCase);
            int modAddress = isWord ? address : address + 1;
            int halfBytesLength = isWord ? 0x06 : 0x05;

            string operandForEvaluation = NormalizeOperandForExpressionEvaluation(line.Operand);
            if (string.IsNullOrWhiteSpace(operandForEvaluation))
                return;

            var (_, evalType, evalErr, evalMeta) = _tablaSimbolos.EvaluateExpressionForObject(
                operandForEvaluation,
                address,
                allowUndefinedSymbols: true,
                controlSectionName: line.ControlSectionName);

            if (evalErr != null)
                return;

            AgregarModificacionesExplícitas(state, modAddress, halfBytesLength, evalMeta);

            if (evalType == SymbolType.Relative)
            {
                char moduleSign = evalMeta.RelativeModuleSign ?? '+';
                state.AddModification(modAddress, halfBytesLength, moduleSign, state.Module.Name);
            }
        }

        private static void AgregarModificacionesExplícitas(ModuleBuilderState state, int modAddress, int halfBytesLength, ExpressionEvaluationMetadata evalMeta)
        {
            foreach (var req in evalMeta.ModificationRequests)
            {
                if (req is null)
                    continue;

                if (string.IsNullOrWhiteSpace(req.Symbol))
                    continue;

                if (req.Sign != '+' && req.Sign != '-')
                    continue;

                state.AddModification(modAddress, halfBytesLength, req.Sign, req.Symbol);
            }
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

        private void FinalizeModuleBuilders()
        {
            var ordered = _moduleStates.Values
                .OrderBy(s => s.SectionNumber)
                .ThenBy(s => s.Module.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var state in ordered)
            {
                state.FlushText();

                int eAddress = string.Equals(state.Module.Name, _nombrePrograma, StringComparison.OrdinalIgnoreCase)
                    ? _direccionEjecucion
                    : (state.FirstExecutableAddress ?? 0);

                state.Module.E = new EndRecord(eAddress);
                GeneratedModules.Add(state.Module);
            }
        }

        private static int GetEffectiveAddress(IntermediateLine linea)
            => linea.AbsoluteAddress >= 0 ? linea.AbsoluteAddress : linea.Address;

        // FORMATO 1: [opcode] (1 byte)
        /// <summary>
        /// Genera codigo objeto para formato 1.
        /// </summary>
        private (string ObjCode, string Error) GenerateFormat1(OpCodeInfo opInfo) //no hay operandos, solo el opcode
        {
            return (opInfo.Opcode.ToString("X2"), "");
        }

        // FORMATO 2 
        // Estructura: [opcode 8][r1 4][r2 4] = 2 bytes                      
        // Usa Paso1.REGISTER_NUMBERS para convertir nombres de registros a números              
      
        private (string ObjCode, string Error) GenerateFormat2(IntermediateLine linea, OpCodeInfo infoOp) //devuelve el código objeto de formato 2, junto con el mensaje de error
        {
            string operando = linea.Operand.Trim(); //obtenemos el operando, que puede ser uno o dos registros separados por coma
            string[] partes = operando.Split(','); //separamos por coma para obtener los registros
            string nombreOp = linea.Operation.ToUpperInvariant(); //obtenemos el nombre de la operacion

            int registro1 = 0, registro2 = 0; //identificadores de registro codificados en nibbles dentro de 16 bits

            //casos especiales: SVC (un solo operando numérico), CLEAR y TIXR (un solo operando registro), 
            // SHIFTL y SHIFTR (registro + número de posiciones)
            if (nombreOp == "SVC")
            {
                if (partes.Length >= 1 && int.TryParse(partes[0].Trim(), out int svcNum))
                    registro1 = svcNum;
                registro2 = 0;
            }
            else if (nombreOp == "CLEAR" || nombreOp == "TIXR") //checamos que estas intrucciones tengan registros validos 
            {
                if (partes.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(partes[0].Trim(), out registro1))
                {
                    string err = "Error: Registro invalido";
                    registro1 = 0xF;
                    registro2 = 0xF;
                    int objCodeErr = (infoOp.Opcode << 8) | (registro1 << 4) | registro2;
                    return (objCodeErr.ToString("X4"), err);
                }
                registro2 = 0;
            }
            // SHIFTL, SHIFTR
            else if (nombreOp == "SHIFTL" || nombreOp == "SHIFTR")
            {
                if (partes.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(partes[0].Trim(), out registro1))//si al menos tiene un registro y ese registro no es válido, entonces error de registro inválido
                {
                    string err = "Error: Registro invalido";
                    // ERROR: Generar código objeto con registro=0xF (todos 1s en 4 bits)
                    registro1 = 0xF;
                    registro2 = 0xF;
                    int objCodeErr = (infoOp.Opcode << 8) | (registro1 << 4) | registro2;
                    return (objCodeErr.ToString("X4"), err);
                }
                if (partes.Length >= 2 && int.TryParse(partes[1].Trim(), out int shiftN))
                    registro2 = shiftN - 1;  
            }
            // Dos registros: ADDR, COMPR, DIVR, MULR, RMO, SUBR
            else
            {
                string err = ""; //variable para acumular el mensaje de error, si es que hay error en alguno de los registros, se asigna el mensaje de error y se marca el registro inválido con 0xF
                if (partes.Length >= 1)
                {
                    if (!Paso1.REGISTER_NUMBERS.TryGetValue(partes[0].Trim(), out registro1))
                    {
                        err = "Error: Registro invalido";
                        registro1 = 0xF;  // ERROR: usar 0xF
                    }
                }
                if (partes.Length >= 2)
                {
                    if (!Paso1.REGISTER_NUMBERS.TryGetValue(partes[1].Trim(), out registro2))
                    {
                        err = "Error: Registro invalido";
                        registro2 = 0xF;  // ERROR: usar 0xF
                    }
                }
                if (!string.IsNullOrEmpty(err))
                {
                    int objCodeErr = (infoOp.Opcode << 8) | (registro1 << 4) | registro2;
                    return (objCodeErr.ToString("X4"), err);
                }
            }

            int objCode = (infoOp.Opcode << 8) | (registro1 << 4) | registro2;
            return (objCode.ToString("X4"), "");
        }


        // FORMATO 3            
        // Estructura: [opcode 6][n][i][x][b][p][e=0][disp 12 bits] = 3 bytes
        // MODOS DE DIRECCIONAMIENTO                ║
        // Simple    (n=1,i=1): operandValue directo                        
        // Inmediato (n=0,i=1): PREFIX_IMMEDIATE operandValue (#)           
        // Indirecto (n=1,i=0): PREFIX_INDIRECT operandValue  (@)           
        // Indexado  (x=1):     operandValue indexing (,X)                  
        // VALIDACIÓN: Indirecto (@) NO acepta constantes, solo etiquetas    
        // Según tabla: @m es válido, pero @c NO existe                                                                                         
        // CÁLCULO DE DESPLAZAMIENTO:                                         
        //  1. PC-relativo: disp = target - PC  (rango -2048..2047)          
        //  2. BASE-relativo: disp = target - BASE (rango 0..4095)           
        //  3. Si ninguno funciona → ERROR de Paso 2                         
        private (string ObjCode, string Error) GenerateFormat3(IntermediateLine linea, OpCodeInfo infoOp)
        {
            // Formato 3:
            // - calcula n/i/x según modo
            // - resuelve TA
            // - intenta PC-relativo, luego BASE-relativo
            // - si falla, emite FFF y error "No relativo al CP/B".
            string operando = linea.Operand.Trim();
            string operacion = linea.Operation.TrimStart('+').ToUpperInvariant();

            // Caso especial: RSUB no tiene operando → n=1,i=1, disp=0 
            if (operacion == "RSUB")
            {
                int firstByte = (infoOp.Opcode & 0xFC) | 0x03; // n=1, i=1
                int rsub = firstByte << 16; // xbpe=0000, disp=000
                return (rsub.ToString("X6"), "");
            }

            // Determinar bits n, i, x segun modo de direccionamiento.
            // El modo ya fue determinado por la gramatica en Paso 1.
            int n = 1, i = 1, x = 0;
            string operandoLimpio = operando;

            // 7) Calculo de banderas n/i/x segun el modo de direccionamiento
            switch (linea.AddressingMode)
            {
                case "Inmediato":   // gramatica: PREFIX_IMMEDIATE operandValue
                    n = 0; i = 1;
                    operandoLimpio = operando.TrimStart('#');
                    break;
                case "Indirecto":  // gramatica: PREFIX_INDIRECT operandValue
                    n = 1; i = 0;
                    operandoLimpio = operando.TrimStart('@');
                    break;
                case "Indexado": // gramatica: operandValue indexing (COMMA IDENT)
                    n = 1; i = 1; x = 1;
                    operandoLimpio = operando.Split(',')[0].Trim();
                    break;
                default:            // Simple: operandValue sin prefijo
                    n = 1; i = 1;
                    break;
            }

            operandoLimpio = StripIndexSuffix(operandoLimpio);

            bool isImmediate = (n == 0 && i == 1);
            bool isIndirect = (n == 1 && i == 0);
            bool isIndexed = (x == 1);

            // Validar modo de direccionamiento segun tabla.
            // Verificar si el operando es constante o etiqueta
            bool esOperandoNumerico = TryParseNumeric(operandoLimpio, out int _);
            
            // FORMATO 3: validaciones segun tabla de modos de direccionamiento.
            // Indirecto con constante NO existe en la tabla
            if (isIndirect && esOperandoNumerico)
            {
                string err = "Error: Modo de direccionamiento no existe";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF;
                return (objError.ToString("X6"), err);
            }

            // Resolver direccion objetivo.
            int targetAddress;
            SymbolType targetType;

            int effectiveAddress = GetEffectiveAddress(linea);
            var (evalVal, evalType, evalErr, evalMeta) = _tablaSimbolos.EvaluateExpressionForObject(operandoLimpio, effectiveAddress, controlSectionName: linea.ControlSectionName);
            
            if (evalErr != null)
            {
                string err = "Error: Simbolo no encontrado en TABSIM";
                // 5) Si no existe etiqueta, se marca el error y se usa disp=-1 (0xFFF)
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }
            
            targetAddress = evalVal;
            targetType = evalType;
            
            // Si el operando resulta una constante absoluta (ej: #3, 110H).
            // En formato 3 puede codificarse directo en disp con b=p=0 si cabe en 12 bits.
            if (targetType == SymbolType.Absolute)
            {
                if (targetAddress < 0 || targetAddress > 4095)
                {
                    string err = "Error: Operando fuera de rango";
                    int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
                int fb = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3); // b=0, p=0, e=0
                int obj = (fb << 16) | (xbpe << 12) | (targetAddress & 0xFFF);
                return (obj.ToString("X6"), "");
            }

            // Calcular desplazamiento (PC-relativo o BASE-relativo) 
            // 7) Calculo de banderas b/p y desplazamiento (PC o BASE)
            int pc = effectiveAddress + linea.Increment; // PC = dirección actual + tamaño instrucción
            int bBit = 0, pBit = 0;
            int displacement;

            // 1. Intentar PC-relativo: rango -2048 a 2047
            displacement = targetAddress - pc;
            if (displacement >= -2048 && displacement <= 2047)
            {
                pBit = 1; bBit = 0;
            }
            // 2. Intentar BASE-relativo: rango 0 a 4095
            else if (_baseActual.HasValue)
            {
                displacement = targetAddress - _baseActual.Value;
                if (displacement >= 0 && displacement <= 4095)
                {
                    bBit = 1; pBit = 0;
                }
                else
                {
                    // ERROR 4: desplazamiento no cabe en BASE-relativo -> instruccion no relativa al CP ni a la B.
                    string err = "Error: No relativo al CP/B";
                    int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
            }
            else
            {
                // ERROR 4: BASE no definida (NOBASE activo) -> instruccion no relativa al CP ni a la B.
                string err = "Error: No relativo al CP/B";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }

            // Construir codigo objeto.
            {
                // 8) Con n/i/x/b/p/e y desplazamiento calculados, se ensambla la instruccion
                int firstByte = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | (bBit << 2) | (pBit << 1) | 0; // e=0
                int objCode = (firstByte << 16) | (xbpe << 12) | (displacement & 0xFFF);
                return (objCode.ToString("X6"), "");
            }
        }
 
        // FORMATO 4 gramatica: FORMAT4_PREFIX instruction (+LDA, +JSUB)
        // Estructura: [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]      
        // Dirección ABSOLUTA de 20 bits, sin PC/BASE relativo.                                                                                    
        // VALIDACIÓN: Formato 4 SOLO acepta etiquetas (m), NO constantes (c) 
        // Según tabla de modos: +op m, +op #m, +op @m, +op m,X son válidos  
        // NO válidos: +op c, +op #c, +op c,X (genera error modo no existe)  
        //

        private (string ObjCode, string Error) GenerateFormat4(IntermediateLine linea, OpCodeInfo infoOp)
        {
            // Formato 4:
            // - campo de dirección de 20 bits
            // - si operando relativo, marca '*' para registro M
            // - para errores, usa dirección 0xFFFFF y reporta mensaje semántico.
            string operando = linea.Operand.Trim();
            string operacion = linea.Operation.TrimStart('+').ToUpperInvariant();

            // Caso especial: +RSUB
            if (operacion == "RSUB")
            {
                int firstByte = (infoOp.Opcode & 0xFC) | 0x03; // n=1, i=1
                int xbpe = 1; // e=1
                int rsub = (firstByte << 24) | (xbpe << 20); // addr=0
                return (rsub.ToString("X8"), "");
            }

            // Determinar bits n, i, x
            int n = 1, i = 1, x = 0;
            string operandoLimpio = operando;

            // 7) Calculo de banderas n/i/x segun el modo de direccionamiento
            switch (linea.AddressingMode)
            {
                case "Inmediato":
                    n = 0; i = 1;
                    operandoLimpio = operando.TrimStart('#');
                    break;
                case "Indirecto":
                    n = 1; i = 0;
                    operandoLimpio = operando.TrimStart('@');
                    break;
                case "Indexado":
                    n = 1; i = 1; x = 1;
                    operandoLimpio = operando.Split(',')[0].Trim();
                    break;
                default:
                    n = 1; i = 1;
                    break;
            }

                    operandoLimpio = StripIndexSuffix(operandoLimpio);

            bool isImmediate = (n == 0 && i == 1);

            int effectiveAddress = GetEffectiveAddress(linea);
            var (evalVal, evalType, evalErr, evalMeta) = _tablaSimbolos.EvaluateExpressionForObject(operandoLimpio, effectiveAddress, controlSectionName: linea.ControlSectionName);

            // Si no se puede resolver el operando, reportar símbolo/operando inválido.
            if (evalErr != null)
            {
                string err = "Error: Simbolo no encontrado en TABSIM u operando invalido";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                return (objError.ToString("X8"), err);
            }

            // Formato 4 con valor absoluto (constante o expresión absoluta):
            // permitido mientras quepa en 20 bits.
            // Ejemplo esperado: +SUB 350 -> 1F10015E (sin registro M).
            if (evalType == SymbolType.Absolute)
            {
                // c,X no es válido en este ensamblador para formato 4.
                if (x == 1)
                {
                    string err = "Error: Constante fuera de rango";
                    int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                    int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                    return (objError.ToString("X8"), err);
                }

                if (evalVal < 0 || evalVal > 0xFFFFF)
                {
                    string err = "Error: Operando fuera de rango";
                    int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                    int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                    return (objError.ToString("X8"), err);
                }

                int firstByte = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                int objCode = (firstByte << 24) | (xbpe << 20) | (evalVal & 0xFFFFF);
                return (objCode.ToString("X8"), "");
            }

            // Operando es etiqueta o expresion (m) -> resolver en TABSIM.
            // Se antepone * para indicar registro relocalizable
            int targetAddress = evalVal;

            // Construir código objeto: dirección absoluta 20 bits, e=1
            // Requisito 10: '*' al final marca que se debe generar registro M
            // (relocalizacion del simbolo en ProgramaObjeto)
            {
                // 7) En formato 4 se prepara info para registro de modificacion (se marca con *)
                int firstByte = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                int objCode = (firstByte << 24) | (xbpe << 20) | (targetAddress & 0xFFFFF);
                string suffix = (evalMeta.ModificationRequests.Count > 0 || evalType == SymbolType.Relative || evalMeta.HasUnpairedRelative)
                    ? "*"
                    : "";
                return (objCode.ToString("X8") + suffix, "");
            }
        }

            // DIRECTIVAS CON CODIGO OBJETO (Paso1.DIRECTIVES, gramatica: directive)
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// Procesa directivas del Paso 2.
        /// BYTE y WORD pueden generar codigo objeto; el resto no.
        /// </summary>
        private (string ObjCode, string Error) ProcessDirective(IntermediateLine line, string op)
        {
            // Directivas tratadas en Paso 2:
            // BYTE/WORD generan objeto, END se valida para punto de entrada.
            return op switch
            {
                "BYTE" => GenerateByteObjCode(line.Operand),
                "WORD" => GenerateWordObjCode(line.Operand, GetEffectiveAddress(line), line.ControlSectionName),
                "END" => ValidateEndDirective(line),
                _ => ("", "") // START, END, BASE, NOBASE, RESB, RESW, etc.
            };
        }

        private (string ObjCode, string Error) ValidateEndDirective(IntermediateLine line)
        {
            // Verifica operando de END contra TABSIM.
            // Si no existe, se reporta error de Paso 2 y el registro E usa FFFFFF.
            string operando = line.Operand?.Trim() ?? "";
            if (string.IsNullOrEmpty(operando))
                return ("", "");

            if (_tablaSimbolos.TryGetValue(operando, out _, line.ControlSectionName))
                return ("", "");

            return ("", "Error: Simbolo no encontrado en directiva END");
        }
        
        /// BYTE C'texto' → código ASCII concatenado (gramática: CHARCONST → C'...')
        /// BYTE X'hex'   → valor hexadecimal directo (gramática: HEXCONST → X'...')
        
        private (string ObjCode, string Error) GenerateByteObjCode(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return ("", "Error: Operando vacio para BYTE");

            // gramatica: CHARCONST : C '\'' ~[\r\n']+ '\'' ;
            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3);
                var sb = new StringBuilder();
                foreach (char c in content)
                    sb.Append(((int)c).ToString("X2"));
                return (sb.ToString(), "");
            }

            // gramatica: HEXCONST : X '\'' [0-9A-Fa-f]+ '\'' ;
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3).ToUpperInvariant();
                // Asegurar número par de dígitos (múltiplo de 2 para bytes completos)
                if (content.Length % 2 != 0)
                    content = "0" + content;
                return (content, "");
            }

            return ("", "Error: Formato invalido para BYTE");
        }

        /// WORD expresion -> valor entero en 24 bits (6 digitos hex)
        /// Ejemplo: WORD 3 -> 000003, WORD BUFFER-BUFEND
        private (string ObjCode, string Error) GenerateWordObjCode(string operand, int pc, string? controlSectionName)
        {
            // WORD: empaqueta valor en 24 bits.
            // Si la expresión es relativa, agrega '*' para generar registro M en objeto.
            var (val, type, err, evalMeta) = _tablaSimbolos.EvaluateExpressionForObject(operand, pc, controlSectionName: controlSectionName);
            if (err != null)
                return ("FFFFFF", "Error: " + err); // Error base para WORD invalido

            string code = (val & 0xFFFFFF).ToString("X6");
            if (evalMeta.ModificationRequests.Count > 0 || type == SymbolType.Relative || evalMeta.HasUnpairedRelative)
                code += "*";

            return (code, "");
        }


        /// Intenta parsear un operando como valor numerico.
        /// Soporta formatos de la gramatica:
        ///   NUMBER    : [0-9]+  :decimal
        ///   HEXNUMBER : [0-9][0-9A-Fa-f]* H :hex con sufijo H
        ///   HEXCONST  : X '\'' [0-9A-Fa-f]+ '\'' :X'...'
        
        private bool TryParseNumeric(string operand, out int value)
        {
            // Parser numérico uniforme para operandos en Paso 2.
            // Permite comparar rangos sin depender del tipo de notación del fuente.
            value = 0;
            if (string.IsNullOrEmpty(operand)) return false;

            operand = operand.Trim().TrimStart('#', '@');
            if (string.IsNullOrEmpty(operand)) return false;

            // gramatica: HEXNUMBER -> sufijo H
            if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = operand[..^1];
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            //prefijo 0x (compatibilidad)
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(operand[2..], NumberStyles.HexNumber, null, out value);

            // gramatica: HEXCONST -> X'...'
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string hexPart = operand.Substring(2, operand.Length - 3);
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            // gramatica: NUMBER -> decimal
            return int.TryParse(operand, out value);
        }

        private static string StripIndexSuffix(string operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return operand;

            string compact = operand.Replace(" ", string.Empty);
            if (compact.EndsWith(",X", StringComparison.OrdinalIgnoreCase))
            {
                int commaIndex = compact.LastIndexOf(',');
                if (commaIndex > 0)
                    compact = compact.Substring(0, commaIndex);
            }

            return compact;
        }

        private static List<string> ParseSymbolList(string? operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return new List<string>();

            return operand
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private sealed class ModuleBuilderState
        {
            private readonly HashSet<string> _modificationKeys = new(StringComparer.OrdinalIgnoreCase);
            private int _textStart = -1;
            private readonly StringBuilder _textBytes = new();
            private int _textByteCount;

            public ModuleBuilderState(ObjectModule module, string sectionName, int sectionNumber)
            {
                Module = module;
                SectionName = sectionName;
                SectionNumber = sectionNumber;
            }

            public ObjectModule Module { get; }
            public string SectionName { get; }
            public int SectionNumber { get; }
            public int? FirstExecutableAddress { get; set; }

            public void AppendText(int address, string hexBytes)
            {
                int bytes = string.IsNullOrWhiteSpace(hexBytes) ? 0 : hexBytes.Length / 2;
                if (bytes <= 0)
                    return;

                bool isContiguous = _textStart >= 0 && (_textStart + _textByteCount) == address;
                bool exceedsLimit = (_textByteCount + bytes) > 30;

                if (_textStart >= 0 && (!isContiguous || exceedsLimit))
                    FlushText();

                if (_textStart < 0)
                    _textStart = address;

                _textBytes.Append(hexBytes);
                _textByteCount += bytes;
            }

            public void FlushText()
            {
                if (_textByteCount <= 0)
                    return;

                Module.T.Add(new TextRecord
                {
                    StartAddress = _textStart,
                    HexBytes = _textBytes.ToString()
                });

                _textStart = -1;
                _textBytes.Clear();
                _textByteCount = 0;
            }

            public void AddModification(int address, int halfBytesLength, char sign, string symbol)
            {
                string normalizedSymbol = string.IsNullOrWhiteSpace(symbol) ? Module.Name : symbol.Trim();
                string key = $"{address:X6}|{halfBytesLength:X2}|{sign}|{normalizedSymbol}";
                if (!_modificationKeys.Add(key))
                    return;

                Module.M.Add(new ModificationRecord(address, halfBytesLength, sign, normalizedSymbol));
            }
        }

       
        public string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine("|              PASO 2 - ENSAMBLADOR SIC/XE                          |");
            sb.AppendLine("|              GENERACION DE CODIGO OBJETO                          |");
            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine();

            sb.AppendLine($"Programa        : {_nombrePrograma}");
            sb.AppendLine($"Dir. inicio     : {_direccionInicio:X4}h  ({_direccionInicio})");
            sb.AppendLine($"Long. programa  : {_longitudPrograma:X4}h  ({_longitudPrograma} bytes)");
            if (_baseActual.HasValue)
                sb.AppendLine($"Valor BASE      : {_baseActual.Value:X4}h  ({_baseActual.Value})");
            sb.AppendLine();

            sb.AppendLine("ARCHIVO INTERMEDIO CON CODIGO OBJETO");
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"FMT",-4} | {"MOD",-12} | {"COD_OBJ",-12} | {"ERR"}");
            sb.AppendLine(new string('-', 135));

            foreach (var objLine in ObjectCodeLines)
            {
                var line = objLine.IntermLine;
                string loc = (line.Address >= 0) ? $"{line.Address:X4}h" : "";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "-";
                string codObj = string.IsNullOrEmpty(objLine.ObjectCode) ? "----" : objLine.ObjectCode;

                string errorDisplay = CombinarErrores(line.Error, objLine.ErrorPaso2);
                if (errorDisplay.Length > 45)
                    errorDisplay = errorDisplay[..42] + "...";

                sb.AppendLine($"{line.LineNumber,-4} | {loc,-8} | {line.Label,-10} | {line.Operation,-10} | {line.Operand,-15} | {fmt,-4} | {line.AddressingMode,-12} | {codObj,-12} | {errorDisplay}");
            }
            sb.AppendLine();
            sb.AppendLine("RESUMEN DEL PASO 2 ");
            int linesWithObj = ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ObjectCode));
            int linesWithError = ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ErrorPaso2));
            sb.AppendLine($"  * Lineas procesadas        : {ObjectCodeLines.Count}");
            sb.AppendLine($"  * Lineas con codigo objeto  : {linesWithObj}");
            sb.AppendLine($"  * Lineas con error Paso 2   : {linesWithError}");
            sb.AppendLine($"  * Errores del Paso 2        : {Errors.Count}");

            return sb.ToString();
        }

        //CSV

        public void ExportToCSV(string outputPath)
        {
            var sb = new StringBuilder();
            var utf8WithBom = new UTF8Encoding(true);

            sb.AppendLine("=== PASO 2 - CODIGO OBJETO ===");
            sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,MARCA_RELOC,COD_OBJ,ERR_PASO1,ERR_PASO2,COMENTARIO");

            foreach (var objLine in ObjectCodeLines)
            {
                var line = objLine.IntermLine;
                string addressHex = (line.Address >= 0) ? $"\"{line.Address:X4}\"" : "\"\"";
                string addressDec = (line.Address >= 0) ? $"{line.Address}" : "\"\"";
                string fmt = (line.Format > 0) ? $"{line.Format}" : "\"\"";
                string codObj = string.IsNullOrEmpty(objLine.ObjectCode) ? "----" : objLine.ObjectCode;
                string relocationMark = line.ExternalReferenceSymbols.Count > 0 ? "*SE" : (line.RequiresModification ? "*R" : "");

                sb.AppendLine(string.Join(",",
                    line.LineNumber,
                    addressHex,
                    addressDec,
                    FormatearCeldaCSV(line.Label),
                    FormatearCeldaCSV(line.Operation),
                    FormatearCeldaCSV(line.Operand),
                    fmt,
                    FormatearCeldaCSV(line.AddressingMode),
                    FormatearCeldaCSV(relocationMark),
                    FormatearCeldaCSV(codObj),
                    FormatearCeldaCSV(line.Error),
                    FormatearCeldaCSV(objLine.ErrorPaso2),
                    FormatearCeldaCSV(line.Comment)));
            }

            sb.AppendLine();
            sb.AppendLine("=== RESUMEN DEL PASO 2 ===");
            sb.AppendLine("PROPIEDAD,VALOR");
            sb.AppendLine($"\"NOMBRE_PROGRAMA\",\"{_nombrePrograma}\"");
            sb.AppendLine($"\"DIR_INICIO\",\"{_direccionInicio:X4}\"");
            sb.AppendLine($"\"LONGITUD_PROGRAMA\",\"{_longitudPrograma:X4}\"");
            sb.AppendLine($"\"LINEAS_CON_CODIGO\",{ObjectCodeLines.Count(l => !string.IsNullOrEmpty(l.ObjectCode))}");
            sb.AppendLine($"\"ERRORES_PASO2\",{Errors.Count}");

            File.WriteAllText(outputPath, sb.ToString(), utf8WithBom);
        }

        private string CombinarErrores(string errPaso1, string errPaso2)
        {
            if (string.IsNullOrEmpty(errPaso1) && string.IsNullOrEmpty(errPaso2)) return "";
            if (string.IsNullOrEmpty(errPaso1)) return errPaso2;
            if (string.IsNullOrEmpty(errPaso2)) return errPaso1;
            return errPaso1 + "; " + errPaso2;
        }

        private string FormatearCeldaCSV(string value)
        {
            if (string.IsNullOrEmpty(value)) return "\"\"";
            value = value.Replace("\"", "\"\"");
            if (value.StartsWith("+") || value.StartsWith("-") || value.StartsWith("=") || value.StartsWith("@"))
                value = '\'' + value;
            return $"\"{value}\"";
        }
    }

    
    /// Envuelve una IntermediateLine del Paso 1 con su codigo objeto
    /// y errores generados en el Paso 2
   
    public class ObjectCodeLine
    {
        public IntermediateLine IntermLine { get; }
        public string ObjectCode { get; }
        public string ErrorPaso2 { get; }
        public string SectionName { get; }
        public int SectionNumber { get; }
        public bool RequiresModification { get; }
        public bool HasInternalRelativeModification { get; }
        public char? RelativeModuleSign { get; }
        public List<string> ExternalReferenceSymbols { get; }
        public List<ModificationRequest> ModificationRequests { get; }

        public ObjectCodeLine(IntermediateLine intermLine, string objectCode, string errorPaso2)
        {
            IntermLine = intermLine;
            ObjectCode = objectCode;
            ErrorPaso2 = errorPaso2;
            SectionName = intermLine.ControlSectionName;
            SectionNumber = intermLine.ControlSectionNumber;
            RequiresModification = intermLine.RequiresModification;
            HasInternalRelativeModification = intermLine.HasInternalRelativeModification;
            RelativeModuleSign = intermLine.RelativeModuleSign;
            ExternalReferenceSymbols = new List<string>(intermLine.ExternalReferenceSymbols);
            ModificationRequests = new List<ModificationRequest>(intermLine.ModificationRequests);
        }
    }
}

