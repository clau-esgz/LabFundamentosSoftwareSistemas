using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    // <summary>
    // Implementa el Paso 2 del ensamblador SIC/XE de dos pasadas.
    // OBJETIVO DEL PASO 2:
    // - Generar el código objeto para cada línea del archivo intermedio
    // - Detectar errores propios del Paso 2:
    //  Símbolo no definido en TABSIM al resolver operando
    //  Desplazamiento fuera de rango para PC-relativo / BASE-relativo
    //  BASE no definido cuando se necesita BASE-relativo
    //  Registro inválido en formato 2
    //  Valor inmediato fuera de rango para formato 3
    //  Modo de direccionamiento no existe según tabla de modos
    // DATOS COMPARTIDOS CON PASO 1 :
    // - Paso1.OPTAB:opcodes y formatos (gramática: instruction)
    // - Paso1.DIRECTIVES:directivas (gramática: directive)
    // - Paso1.REGISTER_NUMBERS: número de registro SIC/XE
    // - Paso1.REGISTERS :nombres de registros válidos
   
    /// Formato ¦ Estructura de bits                                           
   
    ///    1    ¦ [opcode 8 bits] = 1 byte  
    ///    2    ¦ [opcode 8][r1 4][r2 4] = 2 bytes 
    ///    3    ¦ [opcode 6][n][i][x][b][p][e=0][disp 12 bits] = 3 bytes 
    ///    4    ¦ [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]  = 4 bytes 
    
    /// </summary>
    /// <remarks>
    /// ??????????????????????????????????????????????????????????????????????????????????
    /// ? CLASE: Paso2 - GENERADOR DE CÓDIGO OBJETO Y REGISTROS SIC/XE                 ?
    /// ??????????????????????????????????????????????????????????????????????????????????
    /// 
    /// RESPONSABILIDADES:
    /// 1. Generar código objeto en 4 formatos (1, 2, 3, 4)
    /// 2. Crear registros de control: H (Header), T (Text), M (Modification), E (End)
    /// 3. Procesar directivas (BYTE, WORD, RESB, RESW, BASE, NOBASE)
    /// 4. Resolver operandos y validar rangos de desplazamientos
    /// 5. Detectar errores semánticos del Paso 2
    /// 
    /// REGISTROS SIC/XE:
    /// ???????????????????????????????????????????????????????????????
    /// ? H RECORD (HEADER):    Encabezado del programa               ?
    /// ?  Formato: H ^ NOMBRE ^ DIR_INICIO ^ LONGITUD_PROGRAMA       ?
    /// ?  Ejemplo: H ^ PROG001 ^ 002048 ^ 008000                     ?
    /// ?  - Marca inicio del programa                                 ?
    /// ?  - Especifica nombre, dirección inicial y longitud           ?
    /// ?                                                              ?
    /// ? T RECORD (TEXT):      Código y datos del programa           ?
    /// ?  Formato: T ^ DIR ^ LONGITUD ^ BYTECODE(S)                  ?
    /// ?  Ejemplo: T ^ 002048 ^ 0F ^ 14202D ^ 69202D ^ ...           ?
    /// ?  - Contiene el código objeto generado                       ?
    /// ?  - Agrupa múltiples bytes (máx 60 bytes/30 bytes hex)        ?
    /// ?  - Dirección es PC-relativo (dirección de inicio del grupo)  ?
    /// ?                                                              ?
    /// ? M RECORD (MODIFICATION): Instrucciones para modificación     ?
    /// ?  Formato: M ^ DIR ^ LONGITUD ^ SÍMBOLO ^ SIGNO              ?
    /// ?  Ejemplo: M ^ 002048 ^ 05 ^ INICIO ^ +                      ?
    /// ?  - Marcas para que el cargador resuelva referencias         ?
    /// ?  - Usado para PC-relativo, BASE-relativo y direccionamientos?
    /// ?  - Signo: + (suma) o - (resta)                              ?
    /// ?                                                              ?
    /// ? E RECORD (END):       Fin del programa                      ?
    /// ?  Formato: E ^ PUNTO_ENTRADA                                 ?
    /// ?  Ejemplo: E ^ 002048                                        ?
    /// ?  - Marca fin del programa                                   ?
    /// ?  - Especifica punto de entrada para ejecución               ?
    /// ???????????????????????????????????????????????????????????????
    /// 
    /// FORMATOS DE INSTRUCCIÓN:
    /// ????????????????????????????????????????????????????????????????
    /// ? FORMATO 1: [opcode 8 bits]                = 1 byte           ?
    /// ?  Ejemplo: 18 (opcode de ADD)                                ?
    /// ?                                                              ?
    /// ? FORMATO 2: [opcode 8][r1 4][r2 4]        = 2 bytes          ?
    /// ?  Ejemplo: 18 A0 (ADD A,0)                                  ?
    /// ?  r1, r2: números de registro (0=A, 1=X, 2=L, 3=B, etc)     ?
    /// ?                                                              ?
    /// ? FORMATO 3: [opcode 6][modes 6][disp 12]  = 3 bytes          ?
    /// ?  Modos: n,i,x,b,p,e  (bits especiales)                     ?
    /// ?  Ejemplo: 4C 0000 (LDA 0 [PC-relativo])                    ?
    /// ?                                                              ?
    /// ? FORMATO 4: [opcode 6][modes 6][addr 20]  = 4 bytes          ?
    /// ?  Usa direccionamiento extendido (e=1)                       ?
    /// ?  Ejemplo: +4C 000000 (LDA [dirección completa])            ?
    /// ????????????????????????????????????????????????????????????????
    /// 
    /// MODOS DE DIRECCIONAMIENTO:
    /// ????????????????????????????????????????????????????????????????
    /// ? n=1, i=1: Indexado simple (símbolo directo)                 ?
    /// ? n=0, i=1: Inmediato (#símbolo)                              ?
    /// ? n=1, i=0: Indirecto (@símbolo)                              ?
    /// ? x=1:      Indexado con registro X                           ?
    /// ? b=1:      BASE-relativo                                     ?
    /// ? p=1:      PC-relativo                                       ?
    /// ? e=1:      Formato extendido (4 bytes)                       ?
    /// ????????????????????????????????????????????????????????????????
    /// </remarks>
    internal class Paso2
    {
        /// DATOS COMPARTIDOS DEL PASO 1:
        private readonly IReadOnlyList<IntermediateLine> _lineas;          // Líneas intermedia del Paso 1
        private readonly SimbolosYExpresiones _tabSimExt;                  // Tabla de símbolos (TABSIM extendida)
        private readonly int _dirInicio;                                   // Dirección inicial del programa
        private readonly int _longPrograma;                                // Longitud total del programa
        private readonly string _nombrePrograma;                           // Nombre del programa

        /// ESTADO DINÁMICO:
        private int? _baseActual;                                          // Valor actual de BASE (actualizado por directives BASE/NOBASE)

        /// RESULTADOS DEL PASO 2:
        public List<SICXEError> Errors { get; } = new();                   // Lista de errores semánticos
        public List<ObjectCodeLine> ObjectCodeLines { get; } = new();      // Líneas con código objeto generado

        /// <summary>
        /// Constructor: recibe datos compartidos del Paso 1
        /// </summary>
        public Paso2(
            IReadOnlyList<IntermediateLine> lineas,
            SimbolosYExpresiones tablaSimExt,
            int dirInicio,
            int longPrograma,
            string nombrePrograma,
            int? valorBase)
        {
            _lineas = lineas;
            _tabSimExt = tablaSimExt;
            _dirInicio = dirInicio;
            _longPrograma = longPrograma;
            _nombrePrograma = nombrePrograma;
            _baseActual = valorBase;
        }

        /// <summary>
        /// ???????????????????????????????????????????????????????????????????????????????
        /// MÉTODO PRINCIPAL: ObjectCodeGeneration() - GENERADOR DE CÓDIGO OBJETO
        /// ???????????????????????????????????????????????????????????????????????????????
        /// 
        /// ALGORITMO PRINCIPAL:
        /// Para cada línea del archivo intermedio:
        ///   1. Si la línea YA TIENE ERROR del Paso 1 ? copiar error, NO generar código
        ///   2. Si es línea de comentario o sin operación ? agregar sin código objeto
        ///   3. Si la operación es DIRECTIVA ? procesar con ProcessDirective()
        ///   4. Si la operación es INSTRUCCIÓN ? generar código según formato (1-4)
        ///   5. Agregar resultado a ObjectCodeLines con código objeto y errores
        /// 
        /// MANEJO DE ERRORES:
        /// - Errores del Paso 1: se heredan sin procesar nuevamente
        /// - Errores del Paso 2: se detectan durante generación de código objeto
        ///   • Instrucción desconocida
        ///   • Símbolo no definido
        ///   • Desplazamiento fuera de rango
        ///   • Registro inválido
        ///   • BASE no definido cuando se necesita
        /// 
        /// DIRECTIVAS SOPORTADAS:
        /// - BYTE: genera código para constante byte
        /// - WORD: genera código para palabra (3 bytes)
        /// - RESB: reserva bytes (sin código objeto)
        /// - RESW: reserva palabras (sin código objeto)
        /// - BASE: define registro base para desplazamientos
        /// - NOBASE: deshabilita BASE-relativo
        /// </summary>
        public void ObjectCodeGeneration()
        {
            foreach (var line in _lineas)
            {
                string codObj = "";
                string errPaso2 = "";

                // si la línea ya tiene error del Paso 1, no generar código objeto 
                if (!string.IsNullOrWhiteSpace(line.Error)) 
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", line.Error));
                    continue;
                }

                //líneas de comentario o sin operación: sin código objeto --
                if (line.Address < 0 || string.IsNullOrWhiteSpace(line.Operation))
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", ""));
                    continue;
                }

                string oper = line.Operation.TrimStart('+').ToUpperInvariant();

                //DIRECTIVAS ,si la operación es una directiva, se procesa con ProcessDirective,
                //  que genera el código objeto correspondiente (si aplica) y 
                // el mensaje de error del Paso 2 (si hubo error).
                //  Luego se actualiza el valor de BASE si la directiva es BASE 
                //  Finalmente, se agrega la línea al resultado con su código objeto y error del Paso 2.
                //  Se continúa al siguiente ciclo sin procesar como instrucción.
                if (Paso1.DIRECTIVES.Contains(oper))
                {
                    (codObj, errPaso2) = ProcessDirective(line, oper);

                    //actualizar el valor de la base 
                    if (oper == "BASE" && !string.IsNullOrEmpty(line.Operand))
                    {
                        if (_tabSimExt.TryGetValue(line.Operand, out var symInfo))
                            _baseActual = symInfo.Value;
                        else if (TryParseNumeric(line.Operand, out int numVal))
                            _baseActual = numVal;
                    }
                    else if (oper == "NOBASE") 
                    {
                        _baseActual = null;
                    }

                    if (!string.IsNullOrEmpty(errPaso2))
                        Errors.Add(new SICXEError(line.SourceLine, 0, errPaso2, SICXEErrorType.Semantico));

                    ObjectCodeLines.Add(new ObjectCodeLine(line, codObj, errPaso2));
                    continue;
                }

                //paso 1, aqui se asume que la operación es una instrucción válida, porque si no lo fuera, ya tendría error del Paso 1 y se saltaría esta parte
                if (!Paso1.OPTAB.TryGetValue(oper, out var opInfo)) //pero va, aqui lo que se hace es si "si la operación no se encuentra en la tabla de opcodes,
                                                                   // entonces es un error de instrucción desconocida"
                {
                    errPaso2 = "Error: Instrucción desconocida";
                    Errors.Add(new SICXEError(line.SourceLine, 0, errPaso2, SICXEErrorType.Semantico)); //agregamos a la lista de errores del Paso 2
                    ObjectCodeLines.Add(new ObjectCodeLine(line, "", errPaso2));
                    continue;
                }
                //Generar código objeto según formato 
                (codObj, errPaso2) = line.Format switch
                {
                    1 => GenerateFormat1(opInfo),
                    2 => GenerateFormat2(line, opInfo),
                    3 => GenerateFormat3(line, opInfo),
                    4 => GenerateFormat4(line, opInfo),
                    _ => ("", "")
                };

                if (!string.IsNullOrEmpty(errPaso2)) //si hubo error en la generación del código objeto, agregarlo a la lista de errores del Paso 2
                    Errors.Add(new SICXEError(line.SourceLine, 0, errPaso2, SICXEErrorType.Semantico));

                ObjectCodeLines.Add(new ObjectCodeLine(line, codObj, errPaso2));
            }
        }

        // FORMATO 1 
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// GENERADOR FORMATO 1: [opcode 8 bits] = 1 byte
        /// 
        /// Usado para instrucciones SIN OPERANDOS:
        /// - ADD, SUB, MUL, DIV (requieren registros en Formato 2)
        /// - RSUB (Return from Subroutine) - no tiene operandos
        /// 
        /// ESTRUCTURA:
        /// Bits 0-7: código de operación (8 bits)
        /// 
        /// EJEMPLO:
        /// Instrucción: RSUB
        /// Opcode:      3C (hexadecimal)
        /// Código obj:  3C
        /// 
        /// PARÁMETROS:
        /// - opInfo: información del opcode (opInfo.Opcode contiene el valor numérico)
        /// 
        /// RETORNA:
        /// - (código_objeto_hex, error)
        ///   • código_objeto_hex: string con 2 dígitos hexadecimales (ej: "3C")
        ///   • error: cadena vacía (nunca hay error en Formato 1)
        /// </summary>
        private (string ObjCode, string Error) GenerateFormat1(OpCodeInfo opInfo) //no hay operandos, solo el opcode
        {
            return (opInfo.Opcode.ToString("X2"), "");
        }

        // FORMATO 2 
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// GENERADOR FORMATO 2: [opcode 8][r1 4][r2 4] = 2 bytes
        /// 
        /// Usado para instrucciones CON REGISTROS:
        /// - ADDR, COMPR, DIVR, MULR, RMO, SUBR (dos registros)
        /// - CLEAR, TIXR (un registro)
        /// - SHIFTL, SHIFTR (registro + número de posiciones)
        /// - SVC (número de servicio inmediato)
        /// 
        /// ESTRUCTURA:
        /// Byte 0 (bits 0-7):  código de operación
        /// Byte 1 (bits 8-11): primer registro (r1)
        ///        (bits 12-15): segundo registro (r2)
        /// 
        /// REGISTROS SIC/XE:
        /// ???????????????????????????????????????????????
        /// ? 0x0  ? 0x1  ? 0x2  ? 0x3  ? 0x4  ?   ...    ?
        /// ?  A   ?  X   ?  L   ?  B   ?  S   ?   más    ?
        /// ???????????????????????????????????????????????
        /// 
        /// EJEMPLOS:
        /// - "ADDR A,X"  ? r1=0, r2=1  ? 90 01 (ADDR opcode=0x90)
        /// - "CLEAR A"   ? r1=0, r2=0  ? B4 00 (CLEAR opcode=0xB4)
        /// - "SVC 5"     ? r1=5, r2=0  ? F0 50 (SVC opcode=0xF0)
        /// 
        /// VALIDACIONES:
        /// - Registros deben ser válidos (0-9, A-F)
        /// - Si registro inválido: usar 0xF y retornar error
        /// - SVC: primer valor es número decimal, no registro
        /// </summary>
        private (string ObjCode, string Error) GenerateFormat2(IntermediateLine line, OpCodeInfo opInfo) //devuelve el código objeto de formato 2, junto con el mensaje de error
        {
            string operand = line.Operand.Trim(); //obtenemos el operando, que puede ser uno o dos registros separados por coma, dependiendo de la instrucción
            string[] parts = operand.Split(',');//separamos por coma para obtener los registros, si es que hay dos, por ejemplo "A,X" se separa en ["A", "X"], pero si es solo un registro como "A", se queda como ["A"]
            string opName = line.Operation.ToUpperInvariant(); //obtenemos el nombre de la operacion

            int r1 = 0, r2 = 0; //r1/r2: identificadores de registro codificados en nibbles dentro de los 16 bits de formato 2.

            //casos especiales: SVC (un solo operando numérico), CLEAR y TIXR (un solo operando registro), 
            // SHIFTL y SHIFTR (registro + número de posiciones)
            if (opName == "SVC")
            {
                if (parts.Length >= 1 && int.TryParse(parts[0].Trim(), out int svcNum))
                    r1 = svcNum;
                r2 = 0;
            }
            else if (opName == "CLEAR" || opName == "TIXR") //checamos que estas intrucciones tengan registros validos 
            {
                if (parts.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))
                {
                    string err = "Error: Registro inválido";
                    r1 = 0xF;
                    r2 = 0xF;
                    int objCodeErr = (opInfo.Opcode << 8) | (r1 << 4) | r2;
                    return (objCodeErr.ToString("X4"), err);
                }
                r2 = 0;
            }
            // SHIFTL, SHIFTR
            else if (opName == "SHIFTL" || opName == "SHIFTR")
            {
                if (parts.Length >= 1 && !Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))//si al menos tiene un registro y ese registro no es válido, entonces error de registro inválido
                {
                    string err = "Error: Registro inválido";
                    // ERROR: Generar código objeto con registro=0xF (todos 1s en 4 bits)
                    r1 = 0xF;
                    r2 = 0xF;
                    int objCodeErr = (opInfo.Opcode << 8) | (r1 << 4) | r2;
                    return (objCodeErr.ToString("X4"), err);
                }
                if (parts.Length >= 2 && int.TryParse(parts[1].Trim(), out int shiftN))
                    r2 = shiftN - 1;  
            }
            // Dos registros: ADDR, COMPR, DIVR, MULR, RMO, SUBR
            else
            {
                string err = ""; //variable para acumular el mensaje de error, si es que hay error en alguno de los registros, se asigna el mensaje de error y se marca el registro inválido con 0xF
                if (parts.Length >= 1)
                    {
                        if (!Paso1.REGISTER_NUMBERS.TryGetValue(parts[0].Trim(), out r1))
                    {
                        err = "Error: Registro inválido";
                        r1 = 0xF;  // ERROR: usar 0xF
                    }
                }
                if (parts.Length >= 2)
                    {
                        if (!Paso1.REGISTER_NUMBERS.TryGetValue(parts[1].Trim(), out r2))
                    {
                        err = "Error: Registro inválido";
                        r2 = 0xF;  // ERROR: usar 0xF
                    }
                }
                if (!string.IsNullOrEmpty(err))
                {
                    int objCodeErr = (opInfo.Opcode << 8) | (r1 << 4) | r2;
                    return (objCodeErr.ToString("X4"), err);
                }
            }

            int objCode = (opInfo.Opcode << 8) | (r1 << 4) | r2;
            return (objCode.ToString("X4"), "");
        }


        // FORMATO 3            
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// GENERADOR FORMATO 3: [opcode 6][modes 6][disp 12 bits] = 3 bytes
        /// 
        /// Usado para instrucciones CON OPERANDO Y DESPLAZAMIENTO (PC o BASE relativo):
        /// - LDA, STA, LDX, STX, etc. (mayoría de instrucciones)
        /// 
        /// ESTRUCTURA DE BITS:
        /// Byte 0 (bits 0-5):  opcode (6 bits)
        ///        (bits 6-7):  modos n,i (2 bits)
        /// Byte 1 (bits 8-10): modos x,b,p (3 bits)
        ///        (bit 11):    e (formato extendido, siempre 0 en Formato 3)
        /// Bytes 1-2 (bits 12-23): desplazamiento (displacement, 12 bits)
        /// 
        /// MODOS DE DIRECCIONAMIENTO (combinaciones de n,i,x,b,p):
        /// ???????????????????????????????????????????????????????????
        /// ? n ? i ? x ? b ? p ? Tipo de Direccionamiento          ?
        /// ???????????????????????????????????????????????????????????
        /// ? 1 ? 1 ? 0 ? 0 ? 0 ? Simple (directo, SIC style)       ?
        /// ? 0 ? 1 ? 0 ? 0 ? 0 ? Inmediato (#valor)                ?
        /// ? 1 ? 0 ? 0 ? 0 ? 0 ? Indirecto (@símbolo)              ?
        /// ? 1 ? 1 ? 1 ? 0 ? 0 ? Indexado (símbolo,X)              ?
        /// ? 1 ? 1 ? 0 ? 1 ? 0 ? BASE-relativo                     ?
        /// ? 1 ? 1 ? 0 ? 0 ? 1 ? PC-relativo                       ?
        /// ? 1 ? 1 ? 1 ? 1 ? 0 ? Indexado + BASE                   ?
        /// ? 1 ? 1 ? 1 ? 0 ? 1 ? Indexado + PC                     ?
        /// ???????????????????????????????????????????????????????????
        /// 
        /// CÁLCULO DE DESPLAZAMIENTO:
        /// - PC-relativo (p=1): disp = target - (dirección_siguiente_instrucción)
        ///   Rango: -2048..2047 (signed 12-bit)
        /// - BASE-relativo (b=1): disp = target - _baseActual
        ///   Rango: 0..4095 (unsigned 12-bit, BASE no puede ser null)
        /// - Simple (b=0, p=0): desplazamiento directo (normalmente 0)
        /// 
        /// RESTRICCIONES:
        /// - Indirecto (@) NO acepta constantes numéricas (solo símbolos/etiquetas)
        /// - Si desplazamiento fuera de rango ? ERROR
        /// - Si se especifica BASE-relativo pero BASE es null ? ERROR
        /// 
        /// EJEMPLOS:
        /// Instrucción: "LDA INICIO,X"
        ///  - Modo: Indexado+PC-relativo (x=1, p=1, n=1, i=1)
        ///  - Cálculo: disp = dirección(INICIO) - PC_siguiente
        ///  - Resultado: T ^ dirección ^ bytes ^ código_objeto_6bytes
        /// 
        /// Instrucción: "#100" (inmediato)
        ///  - Modo: Inmediato (n=0, i=1)
        ///  - Desplazamiento: 100 decimal = 0x064
        ///  - Resultado: código con n=0, i=1
        /// </summary>
        private (string ObjCode, string Error) GenerateFormat3(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string op = line.Operation.TrimStart('+').ToUpperInvariant();

            // Caso especial: RSUB no tiene operando ? n=1,i=1, disp=0 
            if (op == "RSUB")
            {
                int firstByte = (opInfo.Opcode & 0xFC) | 0x03; // n=1, i=1
                int rsub = firstByte << 16; // xbpe=0000, disp=000
                return (rsub.ToString("X6"), "");
            }

            // Determinar bits n, i, x según modo de direccionamiento
            // (El modo ya fue determinado por la gramática en Paso 1)
            int n = 1, i = 1, x = 0;
            string cleanOperand = operand;

            switch (line.AddressingMode)
            {
                case "Inmediato":   //gramática: PREFIX_IMMEDIATE operandValue
                    n = 0; i = 1;
                    cleanOperand = operand.TrimStart('#');
                    break;
                case "Indirecto":  //gramática: PREFIX_INDIRECT operandValue
                    n = 1; i = 0;
                    cleanOperand = operand.TrimStart('@');
                    break;
                case "Indexado": // gramática: operandValue indexing (COMMA IDENT)
                    n = 1; i = 1; x = 1;
                    cleanOperand = operand.Split(',')[0].Trim();
                    break;
                default:            // Simple: operandValue sin prefijo
                    n = 1; i = 1;
                    break;
            }

            bool isImmediate = (n == 0 && i == 1);
            bool isIndirect = (n == 1 && i == 0);
            bool isIndexed = (x == 1);

            // Validar modo de direccionamiento según tabla 
            // Verificar si el operando es constante o etiqueta
            bool isNumericOperand = TryParseNumeric(cleanOperand, out int _);
            
            // FORMATO 3: Validaciones según tabla de modos de direccionamiento
            // Indirecto con constante NO existe en la tabla
            if (isIndirect && isNumericOperand)
            {
                string err = "Error: Modo de direccionamiento no existe";
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF;
                return (objError.ToString("X6"), err);
            }

            // Resolver dirección objetivo
            int targetAddress;
            SymbolType targetType;

            var (evalVal, evalType, evalErr) = _tabSimExt.EvaluateExpression(cleanOperand, line.Address);
            
            if (evalErr != null)
            {
                string err = "Error: Símbolo no encontrado en TABSIM";
                // ERROR 1: Símbolo no encontrado - Generar código objeto con b=1, p=1, disp=0xFFF
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }
            
            targetAddress = evalVal;
            targetType = evalType;
            
            // Si el operando resultó ser una constante absoluta (ej: #3 o #100 o #MAXLEN si MAXLEN es EQU 100 absoluto)
            // Y estamos en Inmediato (n=0, i=1), usamos disp directo al valor:
            if (isImmediate && targetType == SymbolType.Absolute)
            {
                if (targetAddress < 0 || targetAddress > 4095)
                {
                    string err = "Error: Operando fuera de rango";
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
                int fb = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3); // b=0, p=0, e=0
                int obj = (fb << 16) | (xbpe << 12) | (targetAddress & 0xFFF);
                return (obj.ToString("X6"), "");
            }

            // Calcular desplazamiento (PC-relativo o BASE-relativo) 
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
            else if (_baseActual.HasValue)
            {
                displacement = targetAddress - _baseActual.Value;
                if (displacement >= 0 && displacement <= 4095)
                {
                    bBit = 1; pBit = 0;
                }
                else
                {
                    // ERROR 4: desplazamiento no cabe en BASE-relativo ? instrucción no relativa al CP ni a la B
                    string err = "Error: No relativo al CP/B";
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
            }
            else
            {
                // ERROR 4: BASE no definida (NOBASE activo) ? instrucción no relativa al CP ni a la B
                string err = "Error: No relativo al CP/B";
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }

            // Construir código objeto 
            {
                int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | (bBit << 2) | (pBit << 1) | 0; // e=0
                int objCode = (firstByte << 16) | (xbpe << 12) | (displacement & 0xFFF);
                return (objCode.ToString("X6"), "");
            }
        }
 
        // FORMATO 4 gramática: FORMAT4_PREFIX instruction ? +LDA, +JSUB)    
        // Estructura: [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]      
        // FORMATO 4            
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// GENERADOR FORMATO 4: [opcode 6][modes 6][addr 20 bits] = 4 bytes
        /// 
        /// Usado para DIRECCIONAMIENTO EXTENDIDO (sin limitación de rango):
        /// - Instrucciones con prefijo "+" (ej: "+LDA", "+STA")
        /// - Requiere dirección ABSOLUTA de 20 bits (0..1048575)
        /// - NO utiliza PC-relativo ni BASE-relativo
        /// 
        /// ESTRUCTURA DE BITS:
        /// Byte 0 (bits 0-5):  opcode (6 bits)
        ///        (bits 6-7):  modos n,i
        /// Byte 1 (bits 8-10): modos x,b,p
        ///        (bit 11):    e = 1 (formato extendido, siempre 1 en Formato 4)
        /// Bytes 1-3 (bits 12-31): dirección absoluta (20 bits)
        /// 
        /// MODOS VÁLIDOS (sin desplazamiento):
        /// ????????????????????????????????????????????????
        /// ? n ? i ? x ? b ? p ? e ? Tipo                ?
        /// ????????????????????????????????????????????????
        /// ? 1 ? 1 ? 0 ? 0 ? 0 ? 1 ? Simple (directo)    ?
        /// ? 0 ? 1 ? 0 ? 0 ? 0 ? 1 ? Inmediato (#addr)   ?
        /// ? 1 ? 0 ? 0 ? 0 ? 0 ? 1 ? Indirecto (@addr)   ?
        /// ? 1 ? 1 ? 1 ? 0 ? 0 ? 1 ? Indexado (,X)       ?
        /// ????????????????????????????????????????????????
        /// 
        /// RESTRICCIÓN SIC/XE:
        /// ????????????????????????????????????????????????????
        /// ? Formato 4 SOLO acepta SÍMBOLOS (etiquetas)       ?
        /// ? NO acepta CONSTANTES NUMÉRICAS                   ?
        /// ?                                                  ?
        /// ? ? VÁLIDO:   +LDA INICIO        (símbolo)        ?
        /// ? ? VÁLIDO:   +LDA #INICIO       (inmediato)      ?
        /// ? ? INVÁLIDO: +LDA 1024          (constante)      ?
        /// ? ? INVÁLIDO: +LDA #100          (inmediato const)?
        /// ????????????????????????????????????????????????????
        /// 
        /// VENTAJA SOBRE FORMATO 3:
        /// - Rango: 0 a 1048575 (20 bits) vs -2048 a 2047 (Formato 3)
        /// - No requiere PC-relativo ni BASE-relativo
        /// - Se usa cuando el desplazamiento no cabe en Formato 3
        /// 
        /// EJEMPLOS:
        /// Instrucción: "+LDA TABLA"
        ///  - Modo: Simple (n=1, i=1, x=0, b=0, p=0, e=1)
        ///  - Dirección: valor de TABLA (absoluta)
        ///  - Resultado: 8 dígitos hexadecimales (4 bytes)
        ///  - Ejemplo:   048000 (dirección TABLA)
        /// 
        /// Instrucción: "+LDA #100"
        ///  - Modo: Inmediato (n=0, i=1, e=1)
        ///  - Dirección: 100 = 0x064
        ///  - Resultado: 000064
        /// </summary>
        private (string ObjCode, string Error) GenerateFormat4(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string op = line.Operation.TrimStart('+').ToUpperInvariant();

            // Caso especial: +RSUB
            if (op == "RSUB")
            {
                int firstByte = (opInfo.Opcode & 0xFC) | 0x03; // n=1, i=1
                int xbpe = 1; // e=1
                int rsub = (firstByte << 24) | (xbpe << 20); // addr=0
                return (rsub.ToString("X8"), "");
            }

            // Determinar bits n, i, x
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

            bool isImmediate = (n == 0 && i == 1);

            // Verificar si el operando es constante hexadecimal 000H o cualquier otro numérico
            bool isHexConstant = cleanOperand.EndsWith("H", StringComparison.OrdinalIgnoreCase);

            var (evalVal, evalType, evalErr) = _tabSimExt.EvaluateExpression(cleanOperand, line.Address);

            bool isNumericOperand = TryParseNumeric(cleanOperand, out int numericValue);

            if (isImmediate && evalType == SymbolType.Absolute)
            {
                // Constante hex 000H: válida en formato 4 solo si su valor decimal > 4095
                if (isHexConstant && evalVal > 4095)
                {
                    int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                    int objCode = (firstByte << 24) | (xbpe << 20) | (evalVal & 0xFFFFF);
                    return (objCode.ToString("X8"), "");
                }
                else if (isHexConstant) // isHexConstant && evalVal <= 4095
                {
                    // ERROR 2: constante hex = 4095 en formato 4 ? operando fuera de rango
                    string err = "Error: Operando fuera de rango";
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                    int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                    return (objError.ToString("X8"), err);
                }
                else
                {
                    // Cualquier otra constante (decimal, 0x...) ? modo no existe en formato 4
                    // O expresiones evaluadas absolutas:
                    // De hecho, si es expresión absoluta simple #MAXLEN, y MAXLEN=2000, entonces debe generar error de modo no existe
                    string err = "Error: Modo de direccionamiento no existe";
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                    int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                    return (objError.ToString("X8"), err);
                }
            }

            // Operando es etiqueta o expresión (m) ? resolver en TABSIM
            // Se antepone * para indicar registro relocalizable
            int targetAddress;

            if (evalErr != null)
            {
                string err = "Error: Símbolo no encontrado en TABSIM u operando inválido";
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                return (objError.ToString("X8"), err);
            }
            targetAddress = evalVal;

            // Construir código objeto: dirección absoluta 20 bits, e=1
            // * al final indica que la dirección es relocalizable (depende de dónde se cargue el programa y si es relativa)
            {
                int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                int objCode = (firstByte << 24) | (xbpe << 20) | (targetAddress & 0xFFFFF);
                string suffix = (evalType == SymbolType.Relative) ? "*" : "";
                return (objCode.ToString("X8") + suffix, "");
            }
        }

        // DIRECTIVAS CON CÓDIGO OBJETO (Paso1.DIRECTIVES, gramática: directive)
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// PROCESADOR DE DIRECTIVAS: ProcessDirective()
        /// 
        /// Procesa directivas SIC/XE que pueden generar código objeto:
        /// 
        /// DIRECTIVAS QUE GENERAN CÓDIGO OBJETO:
        /// ??????????????????????????????????????????????????????????????
        /// ? BYTE:  Define constante byte (1 byte)                      ?
        /// ?  Formato: BYTE X'FF'  ?  código: FF                       ?
        /// ?           BYTE C'ABC' ?  código: 414243 (ASCII)          ?
        /// ?  Rango: 0-255 (1 byte)                                    ?
        /// ?                                                            ?
        /// ? WORD:  Define palabra SIC (3 bytes)                       ?
        /// ?  Formato: WORD 256    ?  código: 000100                  ?
        /// ?           WORD TABLA  ?  código: [dirección de TABLA]    ?
        /// ?  Rango: 0-16777215 (3 bytes)                             ?
        /// ??????????????????????????????????????????????????????????????
        /// 
        /// DIRECTIVAS SIN CÓDIGO OBJETO:
        /// ??????????????????????????????????????????????????????????????
        /// ? START:   Define inicio del programa (solo en línea 1)     ?
        /// ? END:     Define fin del programa                          ?
        /// ? BASE:    Establece registro base para desplazamientos     ?
        /// ? NOBASE:  Deshabilita direccionamiento BASE-relativo      ?
        /// ? RESB:    Reserva N bytes (sin inicializar)               ?
        /// ? RESW:    Reserva N palabras (sin inicializar)            ?
        /// ??????????????????????????????????????????????????????????????
        /// 
        /// EJEMPLOS:
        /// - "BYTE X'C1'" ? GenerateByteObjCode("X'C1'") ? "C1"
        /// - "WORD 1024" ? GenerateWordObjCode("1024") ? "000400"
        /// - "BASE TABLA" ? ("", "") (sin código objeto, actualiza BASE)
        /// - "RESB 100" ? ("", "") (reserva espacio, sin código)
        /// </summary>
        private (string ObjCode, string Error) ProcessDirective(IntermediateLine line, string op)
        {
            return op switch
            {
                "BYTE" => GenerateByteObjCode(line.Operand),
                "WORD" => GenerateWordObjCode(line.Operand, line.Address),
                _ => ("", "") // START, END, BASE, NOBASE, RESB, RESW, etc.
            };
        }

        
        /// BYTE C'texto' ? código ASCII concatenado (gramática: CHARCONST ? C'...')
        /// BYTE X'hex'   ? valor hexadecimal directo (gramática: HEXCONST ? X'...')
        
        private (string ObjCode, string Error) GenerateByteObjCode(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return ("", "Error: Operando vacío para BYTE");

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

            return ("", "Error: Formato inválido para BYTE");
        }

        /// WORD expresión ? valor entero en 24 bits (6 dígitos hex)
        /// Ejemplo: WORD 3 ? 000003, WORD BUFFER-BUFEND
        private (string ObjCode, string Error) GenerateWordObjCode(string operand, int pc)
        {
            var (val, type, err) = _tabSimExt.EvaluateExpression(operand, pc);
            if (err != null)
                return ("FFFFFF", "Error: " + err); // O reportar algún error base especial

            string code = (val & 0xFFFFFF).ToString("X6");
            if (type == SymbolType.Relative)
                code += "*";

            return (code, "");
        }


        /// Intenta parsear un operando como valor numérico.
        /// Soporta formatos de la gramática:
        ///   NUMBER    : [0-9]+  :decimal
        ///   HEXNUMBER : [0-9][0-9A-Fa-f]* H :hex con sufijo H
        ///   HEXCONST  : X '\'' [0-9A-Fa-f]+ '\'' :X'...'
        
        private bool TryParseNumeric(string operand, out int value)
        {
            value = 0;
            if (string.IsNullOrEmpty(operand)) return false;

            operand = operand.Trim().TrimStart('#', '@');
            if (string.IsNullOrEmpty(operand)) return false;

            // gramática: HEXNUMBER ? sufijo H
            if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = operand[..^1];
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            //prefijo 0x (compatibilidad)
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(operand[2..], NumberStyles.HexNumber, null, out value);

            // gramática: HEXCONST ? X'...'
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string hexPart = operand.Substring(2, operand.Length - 3);
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            // gramática: NUMBER ? decimal
            return int.TryParse(operand, out value);
        }

       
        public string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine("¦              PASO 2 - ENSAMBLADOR SIC/XE                          ¦");
            sb.AppendLine("¦              GENERACIÓN DE CÓDIGO OBJETO                          ¦");
            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine();

            sb.AppendLine($"Programa        : {_nombrePrograma}");
            sb.AppendLine($"Dir. inicio     : {_dirInicio:X4}h  ({_dirInicio})");
            sb.AppendLine($"Long. programa  : {_longPrograma:X4}h  ({_longPrograma} bytes)");
            if (_baseActual.HasValue)
                sb.AppendLine($"Valor BASE      : {_baseActual.Value:X4}h  ({_baseActual.Value})");
            sb.AppendLine();

            sb.AppendLine("ARCHIVO INTERMEDIO CON CÓDIGO OBJETO");
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
            sb.AppendLine($"  * Líneas procesadas        : {ObjectCodeLines.Count}");
            sb.AppendLine($"  * Líneas con código objeto  : {linesWithObj}");
            sb.AppendLine($"  * Líneas con error Paso 2   : {linesWithError}");
            sb.AppendLine($"  * Errores del Paso 2        : {Errors.Count}");

            return sb.ToString();
        }

        //CSV

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
                string codObj = string.IsNullOrEmpty(objLine.ObjectCode) ? "----" : objLine.ObjectCode;

                sb.AppendLine(string.Join(",",
                    line.LineNumber,
                    addressHex,
                    addressDec,
                    FormatearCeldaCSV(line.Label),
                    FormatearCeldaCSV(line.Operation),
                    FormatearCeldaCSV(line.Operand),
                    fmt,
                    FormatearCeldaCSV(line.AddressingMode),
                    FormatearCeldaCSV(codObj),
                    FormatearCeldaCSV(line.Error),
                    FormatearCeldaCSV(objLine.ErrorPaso2),
                    FormatearCeldaCSV(line.Comment)));
            }

            sb.AppendLine();
            sb.AppendLine("=== RESUMEN DEL PASO 2 ===");
            sb.AppendLine("PROPIEDAD,VALOR");
            sb.AppendLine($"\"NOMBRE_PROGRAMA\",\"{_nombrePrograma}\"");
            sb.AppendLine($"\"DIR_INICIO\",\"{_dirInicio:X4}\"");
            sb.AppendLine($"\"LONGITUD_PROGRAMA\",\"{_longPrograma:X4}\"");
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

    
    /// Envuelve una IntermediateLine del Paso 1 con su código objeto
    /// y errores generados en el Paso 2
   
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

