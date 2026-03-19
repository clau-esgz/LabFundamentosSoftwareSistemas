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
    // - Generar el c�digo objeto para cada l�nea del archivo intermedio
    // - Detectar errores propios del Paso 2:
    //  S�mbolo no definido en TABSIM al resolver operando
    //  Desplazamiento fuera de rango para PC-relativo / BASE-relativo
    //  BASE no definido cuando se necesita BASE-relativo
    //  Registro inv�lido en formato 2
    //  Valor inmediato fuera de rango para formato 3
    //  Modo de direccionamiento no existe seg�n tabla de modos
    // DATOS COMPARTIDOS CON PASO 1 :
    // - Paso1.OPTAB:opcodes y formatos (gram�tica: instruction)
    // - Paso1.DIRECTIVES:directivas (gram�tica: directive)
    // - Paso1.REGISTER_NUMBERS: n�mero de registro SIC/XE
    // - Paso1.REGISTERS :nombres de registros v�lidos
   
    /// Formato � Estructura de bits                                           
   
    ///    1    � [opcode 8 bits] = 1 byte  
    ///    2    � [opcode 8][r1 4][r2 4] = 2 bytes 
    ///    3    � [opcode 6][n][i][x][b][p][e=0][disp 12 bits] = 3 bytes 
    ///    4    � [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]  = 4 bytes 
    
    /// </summary>
    /// <remarks>
    /// ??????????????????????????????????????????????????????????????????????????????????
    /// ? CLASE: Paso2 - GENERADOR DE C�DIGO OBJETO Y REGISTROS SIC/XE                 ?
    /// ??????????????????????????????????????????????????????????????????????????????????
    /// 
    /// RESPONSABILIDADES:
    /// 1. Generar c�digo objeto en 4 formatos (1, 2, 3, 4)
    /// 2. Crear registros de control: H (Header), T (Text), M (Modification), E (End)
    /// 3. Procesar directivas (BYTE, WORD, RESB, RESW, BASE, NOBASE)
    /// 4. Resolver operandos y validar rangos de desplazamientos
    /// 5. Detectar errores sem�nticos del Paso 2
    /// 
    /// REGISTROS SIC/XE:
    /// ???????????????????????????????????????????????????????????????
    /// ? H RECORD (HEADER):    Encabezado del programa               ?
    /// ?  Formato: H ^ NOMBRE ^ DIR_INICIO ^ LONGITUD_PROGRAMA       ?
    /// ?  Ejemplo: H ^ PROG001 ^ 002048 ^ 008000                     ?
    /// ?  - Marca inicio del programa                                 ?
    /// ?  - Especifica nombre, direcci�n inicial y longitud           ?
    /// ?                                                              ?
    /// ? T RECORD (TEXT):      C�digo y datos del programa           ?
    /// ?  Formato: T ^ DIR ^ LONGITUD ^ BYTECODE(S)                  ?
    /// ?  Ejemplo: T ^ 002048 ^ 0F ^ 14202D ^ 69202D ^ ...           ?
    /// ?  - Contiene el c�digo objeto generado                       ?
    /// ?  - Agrupa m�ltiples bytes (m�x 60 bytes/30 bytes hex)        ?
    /// ?  - Direcci�n es PC-relativo (direcci�n de inicio del grupo)  ?
    /// ?                                                              ?
    /// ? M RECORD (MODIFICATION): Instrucciones para modificaci�n     ?
    /// ?  Formato: M ^ DIR ^ LONGITUD ^ S�MBOLO ^ SIGNO              ?
    /// ?  Ejemplo: M ^ 002048 ^ 05 ^ INICIO ^ +                      ?
    /// ?  - Marcas para que el cargador resuelva referencias         ?
    /// ?  - Usado para PC-relativo, BASE-relativo y direccionamientos?
    /// ?  - Signo: + (suma) o - (resta)                              ?
    /// ?                                                              ?
    /// ? E RECORD (END):       Fin del programa                      ?
    /// ?  Formato: E ^ PUNTO_ENTRADA                                 ?
    /// ?  Ejemplo: E ^ 002048                                        ?
    /// ?  - Marca fin del programa                                   ?
    /// ?  - Especifica punto de entrada para ejecuci�n               ?
    /// ???????????????????????????????????????????????????????????????
    /// 
    /// FORMATOS DE INSTRUCCI�N:
    /// ????????????????????????????????????????????????????????????????
    /// ? FORMATO 1: [opcode 8 bits]                = 1 byte           ?
    /// ?  Ejemplo: 18 (opcode de ADD)                                ?
    /// ?                                                              ?
    /// ? FORMATO 2: [opcode 8][r1 4][r2 4]        = 2 bytes          ?
    /// ?  Ejemplo: 18 A0 (ADD A,0)                                  ?
    /// ?  r1, r2: n�meros de registro (0=A, 1=X, 2=L, 3=B, etc)     ?
    /// ?                                                              ?
    /// ? FORMATO 3: [opcode 6][modes 6][disp 12]  = 3 bytes          ?
    /// ?  Modos: n,i,x,b,p,e  (bits especiales)                     ?
    /// ?  Ejemplo: 4C 0000 (LDA 0 [PC-relativo])                    ?
    /// ?                                                              ?
    /// ? FORMATO 4: [opcode 6][modes 6][addr 20]  = 4 bytes          ?
    /// ?  Usa direccionamiento extendido (e=1)                       ?
    /// ?  Ejemplo: +4C 000000 (LDA [direcci�n completa])            ?
    /// ????????????????????????????????????????????????????????????????
    /// 
    /// MODOS DE DIRECCIONAMIENTO:
    /// ????????????????????????????????????????????????????????????????
    /// ? n=1, i=1: Indexado simple (s�mbolo directo)                 ?
    /// ? n=0, i=1: Inmediato (#s�mbolo)                              ?
    /// ? n=1, i=0: Indirecto (@s�mbolo)                              ?
    /// ? x=1:      Indexado con registro X                           ?
    /// ? b=1:      BASE-relativo                                     ?
    /// ? p=1:      PC-relativo                                       ?
    /// ? e=1:      Formato extendido (4 bytes)                       ?
    /// ????????????????????????????????????????????????????????????????
    /// </remarks>
    internal class Paso2
    {
        //paso1
        private readonly IReadOnlyList<IntermediateLine> _lineasIntermedias;
        private readonly SimbolosYExpresiones _tablaSimbolos;
        private readonly int _direccionInicio;
        private readonly int _longitudPrograma;
        private readonly string _nombrePrograma;

        private int? _baseActual; //Valor actual de BASE, se actualiza al procesar directivas BASE/NOBASE

        /// RESULTADOS DEL PASO 2:
        public List<SICXEError> Errors { get; } = new();                   // Lista de errores sem�nticos
        public List<ObjectCodeLine> ObjectCodeLines { get; } = new();      // L�neas con c�digo objeto generado

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
            _lineasIntermedias = lineas;
            _tablaSimbolos = tablaSimExt;
            _direccionInicio = dirInicio;
            _longitudPrograma = longPrograma;
            _nombrePrograma = nombrePrograma;
            _baseActual = valorBase;
        }

        /// <summary>
        /// ???????????????????????????????????????????????????????????????????????????????
        /// M�TODO PRINCIPAL: ObjectCodeGeneration() - GENERADOR DE C�DIGO OBJETO
        /// ???????????????????????????????????????????????????????????????????????????????
        /// 
        /// ALGORITMO PRINCIPAL:
        /// Para cada l�nea del archivo intermedio:
        ///   1. Si la l�nea YA TIENE ERROR del Paso 1 ? copiar error, NO generar c�digo
        ///   2. Si es l�nea de comentario o sin operaci�n ? agregar sin c�digo objeto
        ///   3. Si la operaci�n es DIRECTIVA ? procesar con ProcessDirective()
        ///   4. Si la operaci�n es INSTRUCCI�N ? generar c�digo seg�n formato (1-4)
        ///   5. Agregar resultado a ObjectCodeLines con c�digo objeto y errores
        /// 
        /// MANEJO DE ERRORES:
        /// - Errores del Paso 1: se heredan sin procesar nuevamente
        /// - Errores del Paso 2: se detectan durante generaci�n de c�digo objeto
        ///   � Instrucci�n desconocida
        ///   � S�mbolo no definido
        ///   � Desplazamiento fuera de rango
        ///   � Registro inv�lido
        ///   � BASE no definido cuando se necesita
        /// 
        /// DIRECTIVAS SOPORTADAS:
        /// - BYTE: genera c�digo para constante byte
        /// - WORD: genera c�digo para palabra (3 bytes)
        /// - RESB: reserva bytes (sin c�digo objeto)
        /// - RESW: reserva palabras (sin c�digo objeto)
        /// - BASE: define registro base para desplazamientos
        /// - NOBASE: deshabilita BASE-relativo
        /// </summary>
        public void ObjectCodeGeneration()
        {
            // Motor principal del Paso 2:
            // recorre el intermedio, genera código por formato y conserva trazabilidad
            // de errores semánticos por línea.
            foreach (var linea in _lineasIntermedias)
            {
                string codigoObjeto = "";
                string errorPaso2 = "";

                // si la línea ya tiene error del Paso 1, no generar código objeto 
                if (!string.IsNullOrWhiteSpace(linea.Error)) 
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(linea, "", linea.Error));
                    continue;
                }

                //líneas de comentario o sin operación: sin código objeto ──
                if (linea.Address < 0 || string.IsNullOrWhiteSpace(linea.Operation))
                {
                    ObjectCodeLines.Add(new ObjectCodeLine(linea, "", ""));
                    continue;
                }

                string operacion = linea.Operation.TrimStart('+').ToUpperInvariant();

                //DIRECTIVAS ,si la operaci�n es una directiva, se procesa con ProcessDirective,
                //  que genera el c�digo objeto correspondiente (si aplica) y 
                // el mensaje de error del Paso 2 (si hubo error).
                //  Luego se actualiza el valor de BASE si la directiva es BASE 
                //  Finalmente, se agrega la línea al resultado con su código objeto y error del Paso 2.
                //  Se continúa al siguiente ciclo sin procesar como instrucción.
                if (Paso1.DIRECTIVES.Contains(operacion))
                {
                    (codigoObjeto, errorPaso2) = ProcessDirective(linea, operacion);

                    //actualizar el valor de la base 
                    if (operacion == "BASE" && !string.IsNullOrEmpty(linea.Operand))
                    {
                        if (_tablaSimbolos.TryGetValue(linea.Operand, out var symInfo))
                            _baseActual = symInfo.Value;
                        else if (TryParseNumeric(linea.Operand, out int numVal))
                            _baseActual = numVal;
                    }
                    else if (operacion == "NOBASE") 
                    {
                        _baseActual = null;
                    }

                    if (!string.IsNullOrEmpty(errorPaso2))
                        Errors.Add(new SICXEError(linea.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));

                    ObjectCodeLines.Add(new ObjectCodeLine(linea, codigoObjeto, errorPaso2));
                    continue;
                }

                //paso 1, aqui se asume que la operación es una instrucción válida, porque si no lo fuera, ya tendría error del Paso 1 y se saltaría esta parte
                if (!Paso1.OPTAB.TryGetValue(operacion, out var infoOp)) //pero va, aqui lo que se hace es si "si la operación no se encuentra en la tabla de opcodes,
                                                                   // entonces es un error de instrucción desconocida"
                {
                    errorPaso2 = "Error: Instrucción desconocida";
                    Errors.Add(new SICXEError(linea.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico)); //agregamos a la lista de errores del Paso 2
                    ObjectCodeLines.Add(new ObjectCodeLine(linea, "", errorPaso2));
                    continue;
                }
                //Generar código objeto según formato 
                (codigoObjeto, errorPaso2) = linea.Format switch
                {
                    1 => GenerateFormat1(infoOp),
                    2 => GenerateFormat2(linea, infoOp),
                    3 => GenerateFormat3(linea, infoOp),
                    4 => GenerateFormat4(linea, infoOp),
                    _ => ("", "")
                };

                if (!string.IsNullOrEmpty(errorPaso2)) //si hubo error en la generación del código objeto, agregarlo a la lista de errores del Paso 2
                    Errors.Add(new SICXEError(linea.SourceLine, 0, errorPaso2, SICXEErrorType.Semantico));

                ObjectCodeLines.Add(new ObjectCodeLine(linea, codigoObjeto, errorPaso2));
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
        /// Bits 0-7: c�digo de operaci�n (8 bits)
        /// 
        /// EJEMPLO:
        /// Instrucci�n: RSUB
        /// Opcode:      3C (hexadecimal)
        /// C�digo obj:  3C
        /// 
        /// PAR�METROS:
        /// - opInfo: informaci�n del opcode (opInfo.Opcode contiene el valor num�rico)
        /// 
        /// RETORNA:
        /// - (c�digo_objeto_hex, error)
        ///   � c�digo_objeto_hex: string con 2 d�gitos hexadecimales (ej: "3C")
        ///   � error: cadena vac�a (nunca hay error en Formato 1)
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
                    string err = "Error: Registro inválido";
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
                    string err = "Error: Registro inválido";
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
                        err = "Error: Registro inválido";
                        registro1 = 0xF;  // ERROR: usar 0xF
                    }
                }
                if (partes.Length >= 2)
                {
                    if (!Paso1.REGISTER_NUMBERS.TryGetValue(partes[1].Trim(), out registro2))
                    {
                        err = "Error: Registro inválido";
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

            // Determinar bits n, i, x seg�n modo de direccionamiento
            // (El modo ya fue determinado por la gram�tica en Paso 1)
            int n = 1, i = 1, x = 0;
            string operandoLimpio = operando;

            // 7) Calculo de banderas n/i/x segun el modo de direccionamiento
            switch (linea.AddressingMode)
            {
                case "Inmediato":   //gram�tica: PREFIX_IMMEDIATE operandValue
                    n = 0; i = 1;
                    operandoLimpio = operando.TrimStart('#');
                    break;
                case "Indirecto":  //gram�tica: PREFIX_INDIRECT operandValue
                    n = 1; i = 0;
                    operandoLimpio = operando.TrimStart('@');
                    break;
                case "Indexado": // gram�tica: operandValue indexing (COMMA IDENT)
                    n = 1; i = 1; x = 1;
                    operandoLimpio = operando.Split(',')[0].Trim();
                    break;
                default:            // Simple: operandValue sin prefijo
                    n = 1; i = 1;
                    break;
            }

            bool isImmediate = (n == 0 && i == 1);
            bool isIndirect = (n == 1 && i == 0);
            bool isIndexed = (x == 1);

            // Validar modo de direccionamiento seg�n tabla 
            // Verificar si el operando es constante o etiqueta
            bool esOperandoNumerico = TryParseNumeric(operandoLimpio, out int _);
            
            // FORMATO 3: Validaciones seg�n tabla de modos de direccionamiento
            // Indirecto con constante NO existe en la tabla
            if (isIndirect && esOperandoNumerico)
            {
                string err = "Error: Modo de direccionamiento no existe";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF;
                return (objError.ToString("X6"), err);
            }

            // Resolver direcci�n objetivo
            int targetAddress;
            SymbolType targetType;

            var (evalVal, evalType, evalErr) = _tablaSimbolos.EvaluateExpression(operandoLimpio, linea.Address);
            
            if (evalErr != null)
            {
                string err = "Error: Símbolo no encontrado en TABSIM";
                // 5) Si no existe etiqueta, se marca el error y se usa disp=-1 (0xFFF)
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }
            
            targetAddress = evalVal;
            targetType = evalType;
            
            // Si el operando result� ser una constante absoluta (ej: #3 o #100 o #MAXLEN si MAXLEN es EQU 100 absoluto)
            // Y estamos en Inmediato (n=0, i=1), usamos disp directo al valor:
            if (isImmediate && targetType == SymbolType.Absolute)
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
            int pc = linea.Address + linea.Increment; // PC = dirección actual + tamaño instrucción
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
                    // ERROR 4: desplazamiento no cabe en BASE-relativo ? instrucci�n no relativa al CP ni a la B
                    string err = "Error: No relativo al CP/B";
                    int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
            }
            else
            {
                // ERROR 4: BASE no definida (NOBASE activo) ? instrucci�n no relativa al CP ni a la B
                string err = "Error: No relativo al CP/B";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                return (objError.ToString("X6"), err);
            }

            // Construir c�digo objeto 
            {
                // 8) Con n/i/x/b/p/e y desplazamiento calculados, se ensambla la instruccion
                int firstByte = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | (bBit << 2) | (pBit << 1) | 0; // e=0
                int objCode = (firstByte << 16) | (xbpe << 12) | (displacement & 0xFFF);
                return (objCode.ToString("X6"), "");
            }
        }
 
        // FORMATO 4 gram�tica: FORMAT4_PREFIX instruction ? +LDA, +JSUB)    
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

            bool isImmediate = (n == 0 && i == 1);

            var (evalVal, evalType, evalErr) = _tablaSimbolos.EvaluateExpression(operandoLimpio, linea.Address);

            // Si no se puede resolver el operando, reportar símbolo/operando inválido.
            if (evalErr != null)
            {
                string err = "Error: Símbolo no encontrado en TABSIM u operando inválido";
                int fbError = (infoOp.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                return (objError.ToString("X8"), err);
            }

            // Regla del modelo: si se pasa una constante literal 'c' (0..4095)
            // en formato 4, se marca como operando fuera de rango.
            // (ejemplo: +SUB 350)
            if (TryParseNumeric(operandoLimpio, out int valorConstante) && valorConstante >= 0 && valorConstante <= 4095)
            {
                string err = "Error: Operando fuera de rango";
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

            // Operando es etiqueta o expresi�n (m) ? resolver en TABSIM
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
                string suffix = (evalType == SymbolType.Relative) ? "*" : "";
                return (objCode.ToString("X8") + suffix, "");
            }
        }

        // DIRECTIVAS CON C�DIGO OBJETO (Paso1.DIRECTIVES, gram�tica: directive)
        // ????????????????????????????????????????????????????????????????????????
        /// <summary>
        /// PROCESADOR DE DIRECTIVAS: ProcessDirective()
        /// 
        /// Procesa directivas SIC/XE que pueden generar c�digo objeto:
        /// 
        /// DIRECTIVAS QUE GENERAN C�DIGO OBJETO:
        /// ??????????????????????????????????????????????????????????????
        /// ? BYTE:  Define constante byte (1 byte)                      ?
        /// ?  Formato: BYTE X'FF'  ?  c�digo: FF                       ?
        /// ?           BYTE C'ABC' ?  c�digo: 414243 (ASCII)          ?
        /// ?  Rango: 0-255 (1 byte)                                    ?
        /// ?                                                            ?
        /// ? WORD:  Define palabra SIC (3 bytes)                       ?
        /// ?  Formato: WORD 256    ?  c�digo: 000100                  ?
        /// ?           WORD TABLA  ?  c�digo: [direcci�n de TABLA]    ?
        /// ?  Rango: 0-16777215 (3 bytes)                             ?
        /// ??????????????????????????????????????????????????????????????
        /// 
        /// DIRECTIVAS SIN C�DIGO OBJETO:
        /// ??????????????????????????????????????????????????????????????
        /// ? START:   Define inicio del programa (solo en l�nea 1)     ?
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
        /// - "BASE TABLA" ? ("", "") (sin c�digo objeto, actualiza BASE)
        /// - "RESB 100" ? ("", "") (reserva espacio, sin c�digo)
        /// </summary>
        private (string ObjCode, string Error) ProcessDirective(IntermediateLine line, string op)
        {
            // Directivas tratadas en Paso 2:
            // BYTE/WORD generan objeto, END se valida para punto de entrada.
            return op switch
            {
                "BYTE" => GenerateByteObjCode(line.Operand),
                "WORD" => GenerateWordObjCode(line.Operand, line.Address),
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

            if (_tablaSimbolos.TryGetValue(operando, out _))
                return ("", "");

            return ("", "Error: Símbolo no encontrado en directiva END");
        }
        
        /// BYTE C'texto' → código ASCII concatenado (gramática: CHARCONST → C'...')
        /// BYTE X'hex'   → valor hexadecimal directo (gramática: HEXCONST → X'...')
        
        private (string ObjCode, string Error) GenerateByteObjCode(string operand)
        {
            if (string.IsNullOrEmpty(operand))
                return ("", "Error: Operando vac�o para BYTE");

            // gram�tica: CHARCONST : C '\'' ~[\r\n']+ '\'' ;
            if (operand.StartsWith("C'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3);
                var sb = new StringBuilder();
                foreach (char c in content)
                    sb.Append(((int)c).ToString("X2"));
                return (sb.ToString(), "");
            }

            // gram�tica: HEXCONST : X '\'' [0-9A-Fa-f]+ '\'' ;
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string content = operand.Substring(2, operand.Length - 3).ToUpperInvariant();
                // Asegurar número par de dígitos (múltiplo de 2 para bytes completos)
                if (content.Length % 2 != 0)
                    content = "0" + content;
                return (content, "");
            }

            return ("", "Error: Formato inv�lido para BYTE");
        }

        /// WORD expresi�n ? valor entero en 24 bits (6 d�gitos hex)
        /// Ejemplo: WORD 3 ? 000003, WORD BUFFER-BUFEND
        private (string ObjCode, string Error) GenerateWordObjCode(string operand, int pc)
        {
            // WORD: empaqueta valor en 24 bits.
            // Si la expresión es relativa, agrega '*' para generar registro M en objeto.
            var (val, type, err) = _tablaSimbolos.EvaluateExpression(operand, pc);
            if (err != null)
                return ("FFFFFF", "Error: " + err); // O reportar alg�n error base especial

            string code = (val & 0xFFFFFF).ToString("X6");
            if (type == SymbolType.Relative)
                code += "*";

            return (code, "");
        }


        /// Intenta parsear un operando como valor num�rico.
        /// Soporta formatos de la gram�tica:
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

            // gram�tica: HEXNUMBER ? sufijo H
            if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = operand[..^1];
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            //prefijo 0x (compatibilidad)
            if (operand.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                return int.TryParse(operand[2..], NumberStyles.HexNumber, null, out value);

            // gram�tica: HEXCONST ? X'...'
            if (operand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && operand.EndsWith("'"))
            {
                string hexPart = operand.Substring(2, operand.Length - 3);
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            // gram�tica: NUMBER ? decimal
            return int.TryParse(operand, out value);
        }

       
        public string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine("�              PASO 2 - ENSAMBLADOR SIC/XE                          �");
            sb.AppendLine("�              GENERACI�N DE C�DIGO OBJETO                          �");
            sb.AppendLine("+--------------------------------------------------------------------+");
            sb.AppendLine();

            sb.AppendLine($"Programa        : {_nombrePrograma}");
            sb.AppendLine($"Dir. inicio     : {_direccionInicio:X4}h  ({_direccionInicio})");
            sb.AppendLine($"Long. programa  : {_longitudPrograma:X4}h  ({_longitudPrograma} bytes)");
            if (_baseActual.HasValue)
                sb.AppendLine($"Valor BASE      : {_baseActual.Value:X4}h  ({_baseActual.Value})");
            sb.AppendLine();

            sb.AppendLine("ARCHIVO INTERMEDIO CON C�DIGO OBJETO");
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
            sb.AppendLine($"  * L�neas procesadas        : {ObjectCodeLines.Count}");
            sb.AppendLine($"  * L�neas con c�digo objeto  : {linesWithObj}");
            sb.AppendLine($"  * L�neas con error Paso 2   : {linesWithError}");
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

    
    /// Envuelve una IntermediateLine del Paso 1 con su c�digo objeto
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

