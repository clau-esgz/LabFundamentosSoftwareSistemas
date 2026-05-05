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
            ObjectCodeLines.Clear();

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
            // Centralizar marcas (*SE, *R) en un único punto antes de almacenar el código objeto (versión DISPLAY)
            // Para WORD, esta ruta además reevalúa la expresión final y propaga metadata de modificación
            // (símbolos externos, *R y signo relativo del módulo) para que ObjectModuleBuilder pueda generar M.
            string marked = AddObjectCodeMarks(codigoObjeto, linea);
            ObjectCodeLines.Add(new ObjectCodeLine(linea, marked, errorPaso2));
            // Nota: El módulo construcción ya no ocurre aquí; se deferred a FinalizeModuleBuilders()
            // que usa ObjectModuleBuilder para consolidar la lógica con ProgramaObjeto
        }

        private string BuildDisplayMarksForLine(string objectCode, IntermediateLine line)
        {
            if (string.IsNullOrEmpty(objectCode))
                return objectCode;
            string cleaned = objectCode.TrimEnd('*');
            var externals = line.ExternalReferenceSymbols ?? new List<string>();
            return laboratorioPractica3.Utils.ObjectCodeUtils.BuildDisplayMarks(cleaned, externals, line.HasInternalRelativeModification);
        }

        /// <summary>
        /// Añade las marcas de codigo objeto (*SE por cada simbolo externo, *R para relativo interno no emparejado)
        /// Centraliza la decision y evita duplicados de asteriscos generados por los generadores.
        /// Para WORD se reevalua la expresion en Paso2 (tabla completa) para recalcular marcas por forward refs.
        /// </summary>
        private string AddObjectCodeMarks(string objectCode, IntermediateLine line)
        {
            if (string.IsNullOrEmpty(objectCode))
                return objectCode;

            // Eliminar cualquier sufijo asterisco previo
            string cleaned = objectCode.TrimEnd('*');
            var marks = new List<string>();

            // Para WORD y FORMATO 4, re-evaluar expresión con la tabla de símbolos final para detectar
            // externals, relativos no emparejados y el signo de relocalización cuando la resolución
            // tardía de símbolos/forward refs cambia la metadata necesaria para generar M.
            bool requiresLateMetadataRefresh =
                string.Equals(line.Operation, "WORD", StringComparison.OrdinalIgnoreCase) ||
                line.Format == 4;

            if (requiresLateMetadataRefresh)
            {
                int addr = line.AbsoluteAddress >= 0 ? line.AbsoluteAddress : line.Address;
                var (val, type, err, meta) = _tablaSimbolos.EvaluateExpressionForObject(line.Operand, addr, allowUndefinedSymbols: true, controlSectionName: line.ControlSectionName);
                // Propagar símbolos externos encontrados a la línea
                foreach (var ext in meta.ExternalSymbols)
                {
                    if (!line.ExternalReferenceSymbols.Contains(ext, StringComparer.OrdinalIgnoreCase))
                        line.ExternalReferenceSymbols.Add(ext);
                }
                // Si hay modificación interna no emparejada -> marcar *R
                if (meta.HasUnpairedRelative || type == SymbolType.Relative)
                {
                    line.HasInternalRelativeModification = true;
                }
                // Incorporar modification requests y sign del módulo relativo
                if (meta.ModificationRequests.Count > 0)
                {
                    line.ModificationRequests = meta.ModificationRequests;
                    line.RequiresModification = true;
                }
                // Asignar el signo de modificación relativa del módulo si está presente
                if (meta.RelativeModuleSign.HasValue)
                {
                    line.RelativeModuleSign = meta.RelativeModuleSign.Value;
                }
            }

            // Agregar marcas '*' por cada simbolo externo o relativo interno no emparejado
            // (las letras 'SE' o 'R' no deben quedar dentro del campo de bytes del registro T)
            if (line.ExternalReferenceSymbols != null && line.ExternalReferenceSymbols.Count > 0)
            {
                foreach (var s in line.ExternalReferenceSymbols.Distinct(StringComparer.OrdinalIgnoreCase))
                {
                    marks.Add("*");
                }
            }

            // Agregar '*' solo si hay relativo interno no emparejado
            if (line.HasInternalRelativeModification)
            {
                marks.Add("*");
            }

            if (marks.Count == 0)
                return cleaned;

            return laboratorioPractica3.Utils.ObjectCodeUtils.BuildDisplayMarks(
                cleaned,
                line.ExternalReferenceSymbols ?? new List<string>(),
                line.HasInternalRelativeModification);
        }



        
        private void FinalizeModuleBuilders()
        {
            // Usar ObjectModuleBuilder centralizado en lugar de lógica duplicada en ModuleBuilderState
            var builder = new ObjectModuleBuilder(ObjectCodeLines, null!, _nombrePrograma, _direccionEjecucion);
            GeneratedModules.Clear();
            GeneratedModules.AddRange(builder.BuildModules());
        }

        private static int GetEffectiveAddress(IntermediateLine linea)
            => linea.AbsoluteAddress >= 0 ? linea.AbsoluteAddress : linea.Address;

        private static string BuildFormat3ErrorCode(int opcode, int n, int i, int x)
        {
            int fbError = (opcode & 0xFC) | (n << 1) | i;
            int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
            int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF;
            return objError.ToString("X6");
        }

        /// <summary>
        /// Crea un código de error para formato 4 (8 dígitos hex).
        /// Centraliza la construcción repetida de errores: [fbError 6][xbpeError 4][0xFFFFF 20].
        /// </summary>
        private static string BuildFormat4ErrorCode(int opcode, int n, int i, int x)
        {
            int fbError = (opcode & 0xFC) | (n << 1) | i;
            int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
            int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
            return objError.ToString("X8");
        }

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
                return (BuildFormat3ErrorCode(infoOp.Opcode, n, i, x), err);
            }

            // Resolver direccion objetivo.
            int targetAddress;
            SymbolType targetType;

            int effectiveAddress = GetEffectiveAddress(linea);
            var (evalVal, evalType, evalErr, evalMeta) = _tablaSimbolos.EvaluateExpressionForObject(operandoLimpio, effectiveAddress, controlSectionName: linea.ControlSectionName);
            
            if (evalErr != null)
            {
                string err = "Error: Simbolo no encontrado en TABSIM";
                return (BuildFormat3ErrorCode(infoOp.Opcode, n, i, x), err);
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
                    return (BuildFormat3ErrorCode(infoOp.Opcode, n, i, x), err);
                }
            }
            else
            {
                // ERROR 4: BASE no definida (NOBASE activo) -> instruccion no relativa al CP ni a la B.
                string err = "Error: No relativo al CP/B";
                return (BuildFormat3ErrorCode(infoOp.Opcode, n, i, x), err);
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
                return (BuildFormat4ErrorCode(infoOp.Opcode, n, i, x), err);
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
                    return (BuildFormat4ErrorCode(infoOp.Opcode, n, i, x), err);
                }

                if (evalVal < 0 || evalVal > 0xFFFFF)
                {
                    string err = "Error: Operando fuera de rango";
                    return (BuildFormat4ErrorCode(infoOp.Opcode, n, i, x), err);
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
                return (objCode.ToString("X8"), "");
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

