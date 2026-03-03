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
   
    /// Formato │ Estructura de bits                                           
   
    ///    1    │ [opcode 8 bits] = 1 byte  
    ///    2    │ [opcode 8][r1 4][r2 4] = 2 bytes 
    ///    3    │ [opcode 6][n][i][x][b][p][e=0][disp 12 bits] = 3 bytes 
    ///    4    │ [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]  = 4 bytes 
    
    /// </summary>
    internal class Paso2
    {
        //paso1
        private readonly IReadOnlyList<IntermediateLine> _lineas;
        private readonly IReadOnlyDictionary<string, int> _tablaSim;
        private readonly int _dirInicio;
        private readonly int _longPrograma;
        private readonly string _nombrePrograma;

        private int? _baseActual; //Valor actual de BASE, se actualiza al procesar directivas BASE/NOBASE

        // Resultados PASO 2 
        public List<SICXEError> Errors { get; } = new();
        public List<ObjectCodeLine> ObjectCodeLines { get; } = new();

        //constuctor que recibe los datos compartidos con el Paso 1 
        public Paso2(
            IReadOnlyList<IntermediateLine> lineas,
            IReadOnlyDictionary<string, int> tablaSim,
            int dirInicio,
            int longPrograma,
            string nombrePrograma,
            int? valorBase)
        {
            _lineas = lineas;
            _tablaSim = tablaSim;
            _dirInicio = dirInicio;
            _longPrograma = longPrograma;
            _nombrePrograma = nombrePrograma;
            _baseActual = valorBase;
        }

        //Método principal que       
        //recorre cada línea del archivo intermedio y genera su código objeto.
        //si una línea ya tiene error del Paso 1, no genera código objeto.
        //y por ultimo los errores nuevos del Paso 2 se agregan a la lista Errors.

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

                //líneas de comentario o sin operación: sin código objeto ──
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
                        if (_tablaSim.TryGetValue(line.Operand, out int baseAddr))
                            _baseActual = baseAddr;
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
        // estructura: [opcode 8 bits] = 1 byte                               
        
        private (string ObjCode, string Error) GenerateFormat1(OpCodeInfo opInfo) //no hay operandos, solo el opcode
        {
            return (opInfo.Opcode.ToString("X2"), "");
        }

        // FORMATO 2 
        // Estructura: [opcode 8][r1 4][r2 4] = 2 bytes                      
        // Usa Paso1.REGISTER_NUMBERS para convertir nombres de registros a números              
      
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
    

        private (string ObjCode, string Error) GenerateFormat3(IntermediateLine line, OpCodeInfo opInfo)
        {
            string operand = line.Operand.Trim();
            string op = line.Operation.TrimStart('+').ToUpperInvariant();

            // Caso especial: RSUB no tiene operando → n=1,i=1, disp=0 
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

            if (TryParseNumeric(cleanOperand, out int numericValue))
            {
                // Operando es valor numérico directo
                if (isImmediate)
                {
                    // Inmediato con constante: disp = valor directo, p=0, b=0
                    if (numericValue < 0 || numericValue > 4095)
                    {
                        string err = "Error: Operando fuera de rango";
                        // ERROR: Generar código objeto con b=1, p=1, disp=0xFFF (todos 1s)
                        int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                        int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                        int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                        return (objError.ToString("X6"), err);
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
                // Operando es un símbolo buscar en TABSIM
                if (!_tablaSim.TryGetValue(cleanOperand, out targetAddress))
                {
                    string err = "Error: Símbolo no encontrado en TABSIM";
                    // ERROR 1: Símbolo no encontrado - Generar código objeto con b=1, p=1, disp=0xFFF
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
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
                    // ERROR 2: Desplazamiento fuera de rango - Generar código objeto con b=1, p=1, disp=0xFFF
                    string err = "Error: Operando fuera de rango";
                    int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                    int xbpeError = (x << 3) | (1 << 2) | (1 << 1); // b=1, p=1, e=0
                    int objError = (fbError << 16) | (xbpeError << 12) | 0xFFF; // disp=-1 (complemento a 2)
                    return (objError.ToString("X6"), err);
                }
            }
            else
            {
                // ERROR 4: No es relativa al PC ni a la BASE - Generar código objeto con b=1, p=1, disp=0xFFF
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
 
        // FORMATO 4 gramática: FORMAT4_PREFIX instruction → +LDA, +JSUB)    
        // Estructura: [opcode 6][n][i][x][b=0][p=0][e=1][addr 20 bits]      
        // Dirección ABSOLUTA de 20 bits, sin PC/BASE relativo.                                                                                    
        // VALIDACIÓN: Formato 4 SOLO acepta etiquetas (m), NO constantes (c) 
        // Según tabla de modos: +op m, +op #m, +op @m, +op m,X son válidos  
        // NO válidos: +op c, +op #c, +op c,X (genera error modo no existe)  
        // 

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

            bool isIndexed = (x == 1);

            // Validar modo de direccionamiento según tabla 
            // FORMATO 4: Solo acepta etiquetas (m), NO constantes (c)
            // Verificar si el operando es constante
            bool isNumericOperand = TryParseNumeric(cleanOperand, out int numericValue);
            
            if (isNumericOperand)
            {
                // Formato 4 con constante NO existe en la tabla
                string err = "Error: Modo de direccionamiento no existe";
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF;
                return (objError.ToString("X8"), err);
            }

            // Resolver dirección objetivo 
            // Si llegamos aquí, el operando debe ser una etiqueta (no constante)
            int targetAddress;

            if (!_tablaSim.TryGetValue(cleanOperand, out targetAddress))
            {
                // ERROR 1: Símbolo no encontrado en TABSIM - Generar código objeto con b=1, p=1, addr=0xFFFFF
                string err = "Error: Símbolo no encontrado en TABSIM";
                int fbError = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpeError = (x << 3) | (1 << 2) | (1 << 1) | 1; // b=1, p=1, e=1
                int objError = (fbError << 24) | (xbpeError << 20) | 0xFFFFF; // addr=-1 (complemento a 2)
                return (objError.ToString("X8"), err);
            }

            // Construir código objeto: dirección absoluta 20 bits, e=1 
            {
                int firstByte = (opInfo.Opcode & 0xFC) | (n << 1) | i;
                int xbpe = (x << 3) | 1; // b=0, p=0, e=1
                int objCode = (firstByte << 24) | (xbpe << 20) | (targetAddress & 0xFFFFF);
                return (objCode.ToString("X8"), "");
            }
        }

        // DIRECTIVAS CON CÓDIGO OBJETO (Paso1.DIRECTIVES, gramática: directive)
        // Solo BYTE y WORD generan código objeto                              

        private (string ObjCode, string Error) ProcessDirective(IntermediateLine line, string op)
        {
            return op switch
            {
                "BYTE" => GenerateByteObjCode(line.Operand),
                "WORD" => GenerateWordObjCode(line.Operand),
                _ => ("", "") // START, END, BASE, NOBASE, RESB, RESW, etc.
            };
        }

        
        /// BYTE C'texto' → código ASCII concatenado (gramática: CHARCONST → C'...')
        /// BYTE X'hex'   → valor hexadecimal directo (gramática: HEXCONST → X'...')
        
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

        /// WORD n → valor entero en 24 bits (6 dígitos hex)
        /// Ejemplo: WORD 3 → 000003
        private (string ObjCode, string Error) GenerateWordObjCode(string operand)
        {
            if (TryParseNumeric(operand, out int value))
                return ((value & 0xFFFFFF).ToString("X6"), "");

            return ("", "Error: Operando inválido para WORD");
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

            // gramática: HEXNUMBER → sufijo H
            if (operand.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                string hexPart = operand[..^1];
                return int.TryParse(hexPart, NumberStyles.HexNumber, null, out value);
            }

            //prefijo 0x (compatibilidad)
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

       
        public string GenerateReport()
        {
            var sb = new StringBuilder();

            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              PASO 2 - ENSAMBLADOR SIC/XE                          ║");
            sb.AppendLine("║              GENERACIÓN DE CÓDIGO OBJETO                          ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");
            sb.AppendLine();

            sb.AppendLine($"Programa        : {_nombrePrograma}");
            sb.AppendLine($"Dir. inicio     : {_dirInicio:X4}h  ({_dirInicio})");
            sb.AppendLine($"Long. programa  : {_longPrograma:X4}h  ({_longPrograma} bytes)");
            if (_baseActual.HasValue)
                sb.AppendLine($"Valor BASE      : {_baseActual.Value:X4}h  ({_baseActual.Value})");
            sb.AppendLine();

            sb.AppendLine("ARCHIVO INTERMEDIO CON CÓDIGO OBJETO");
            sb.AppendLine($"{"#",-4} | {"CONTLOC",-8} | {"ETQ",-10} | {"CODOP",-10} | {"OPR",-15} | {"FMT",-4} | {"MOD",-12} | {"COD_OBJ",-12} | {"ERR"}");
            sb.AppendLine(new string('─', 135));

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
