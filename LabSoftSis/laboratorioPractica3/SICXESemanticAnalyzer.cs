using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using System.Collections.Generic;
using System.Text;

namespace laboratorioPractica3
{
    /// <summary>
    /// Analizador semántico para programas SIC/XE
    /// Verifica reglas semánticas más allá de la sintaxis
    /// </summary>
    public class SICXESemanticAnalyzer : SICXEBaseListener
    {
        private readonly HashSet<string> _definedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly List<SICXEError> _errors = new List<SICXEError>();//necesitamos una lista de errores para almacenar
                                                                           //los errores encontrados durante el análisis semántico
        private readonly List<TokenInfo> _tokens = new List<TokenInfo>(); //y una lista de tokens para almacenar los tokens reconocidos por el lexer,
                                                                          //lo que nos permitirá generar un reporte detallado de los tokens procesados durante el análisis.

        // Instrucciones que no requieren operandos
        //un hashset es una estructura de datos que almacena elementos únicos
        //y permite búsquedas rápidas. En este caso, se utiliza para almacenar las
        //instrucciones que no requieren operandos, lo que facilita la verificación de
        //si una instrucción pertenece a esta categoría durante el análisis semántico.
        private static readonly HashSet<string> NoOperandInstructions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "FIX", "FLOAT", "HIO", "NORM", "SIO", "TIO", "RSUB"
        };

        // Instrucciones que requieren exactamente 2 registros
        private static readonly HashSet<string> TwoRegisterInstructions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ADDR", "COMPR", "DIVR", "MULR", "RMO", "SUBR"
        };

        // Instrucciones que requieren 1 registro
        private static readonly HashSet<string> OneRegisterInstructions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CLEAR", "TIXR"
        };

        // Instrucciones que requieren registro y número
        private static readonly HashSet<string> RegisterNumberInstructions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "SHIFTL", "SHIFTR", "SVC"
        };

        // Directivas que requieren operando
        private static readonly HashSet<string> RequiredOperandDirectives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "START", "BYTE", "WORD", "RESB", "RESW", "BASE", "EQU", "ORG", "USE", "EXTDEF", "EXTREF"
        };

        // Directivas con operando opcional
        private static readonly HashSet<string> OptionalOperandDirectives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "END"  // END puede tener o no operando: END [Símbolo]
        };

        // Directivas que no requieren operando
        private static readonly HashSet<string> NoOperandDirectives = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "NOBASE", "LTORG", "CSECT"
        };

        // ═══════════════════ REGISTROS VÁLIDOS SIC/XE ═══════════════════
        // Ocho registros de 24 bits cada uno y uno de 48 bits (F)
        // Cada registro tiene un nemónico (nombre), número y uso especial:
        // A(0)=Acumulador, X(1)=Índice, L(2)=Enlace, B(3)=Base, S(4)=General,
        // T(5)=General, F(6)=Punto flotante 48 bits, PC/CP(8)=Contador de programa, SW(9)=Palabra de estado
        private static readonly HashSet<string> ValidRegisters = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "A",    // Registro 0: Acumulador para operaciones aritméticas y lógicas
            "X",    // Registro 1: Registro índice para direccionar
            "L",    // Registro 2: Registro de enlace, para regreso de subrutinas
            "B",    // Registro 3: Registro base, para direccionamiento
            "S",    // Registro 4: Registro de aplicación general
            "T",    // Registro 5: Registro de aplicación general
            "F",    // Registro 6: Acumulador de punto flotante (48 bits)
            "PC",   // Registro 8: Contador de programa - dirección de siguiente instrucción
            "CP",   // Registro 8: Alias de PC (Counter Program = Program Counter)
            "SW"    // Registro 9: Palabra de estado, información de banderas
        };

        
        public IReadOnlyList<SICXEError> Errors => _errors; //Exponer la lista de errores como una propiedad de solo lectura
                                                            //para que otras partes del programa puedan acceder a los errores
                                                            //encontrados durante el análisis semántico sin permitir modificaciones
                                                            //externas a la lista.
        public IReadOnlyList<TokenInfo> Tokens => _tokens;//Exponer la lista de tokens como una propiedad de solo lectura para
                                                          //que otras partes del programa puedan acceder a los tokens
                                                          //reconocidos durante el análisis sin permitir modificaciones externas
                                                          //a la lista.


        /// <summary>
        /// Procesa los tokens del lexer para el reporte
        /// </summary>
        public void ProcessTokens(CommonTokenStream tokenStream, SICXELexer lexer) //recibe como parametros el tokenStream del lexer que
                                                                                   //esto es la fuente de los tokens reconocidos durante
                                                                                   //el análisis léxico, y el lexer mismo para poder acceder a la información y
                                                                                   //el lexer mismo para poder acceder a la información
                                                                                   //de los tokens reconocidos durante el análisis léxico.

        {
            tokenStream.Fill();
            foreach (var token in tokenStream.GetTokens()) //iteramos sobre cada token reconocido
                                                           //por el lexer para procesarlos y almacenarlos
                                                           //en la lista de tokens del analizador semántico.
            {
                if (token.Type != SICXELexer.Eof && token.Type != SICXELexer.WS)
                {
                    string tokenName = lexer.Vocabulary.GetSymbolicName(token.Type) ?? "UNKNOWN"; //obtenemos el nombre simbólico
                                                                                                  //del token a partir de su tipo
                                                                                                  //utilizando el vocabulario del lexer.
                                                                                                  //Si no se encuentra un nombre simbólico,
                                                                                                  //se asigna "UNKNOWN".
                    _tokens.Add(new TokenInfo(
                        token.Line,
                        token.Column,
                        token.Text,
                        tokenName,
                        token.Type
                    ));
                }
            }
        }

        /// <summary>
        /// Verifica la etiqueta en cada statement
        /// </summary>
        public override void EnterStatement(SICXEParser.StatementContext context)
        {
            var labelContext = context.label();
            if (labelContext != null)
            {
                string labelText = labelContext.GetText();
                int line = labelContext.Start.Line;
                int column = labelContext.Start.Column;

                // Verificar longitud máxima de 6 caracteres
                if (labelText.Length > 6)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"Etiqueta '{labelText}' excede el maximo de 6 caracteres (longitud actual: {labelText.Length}). Las etiquetas deben tener entre 1 y 6 caracteres.",
                        SICXEErrorType.Semantico));
                }

                // Verificar que inicia con letra (ya lo hace la gramática, pero verificamos)
                if (!char.IsLetter(labelText[0]))
                {
                    _errors.Add(new SICXEError(line, column,
                        $"Etiqueta '{labelText}' debe iniciar con una letra (A-Z, a-z), pero inicia con '{labelText[0]}'.",
                        SICXEErrorType.Semantico));
                }

                // Verificar etiquetas duplicadas
                if (_definedLabels.Contains(labelText))
                {
                    _errors.Add(new SICXEError(line, column,
                        $"Etiqueta duplicada: '{labelText}'. Esta etiqueta ya fue definida anteriormente en el programa.",
                        SICXEErrorType.Semantico));
                }
                else
                {
                    _definedLabels.Add(labelText);
                }
            }
        }

        /// <summary>
        /// Verifica la operación y sus operandos
        /// </summary>
        public override void ExitStatement(SICXEParser.StatementContext context)
        {
            var operationContext = context.operation();
            if (operationContext == null) return;

            var operandContext = context.operand();
            string operation = GetOperationName(operationContext);
            int line = operationContext.Start.Line;
            int column = operationContext.Start.Column;
            int operandCount = operandContext != null ? operandContext.operandExpr().Length : 0;

            // Verificar instrucciones sin operandos
            if (NoOperandInstructions.Contains(operation))
            {
                if (operandCount > 0)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La instruccion '{operation}' no requiere operandos, pero se encontraron {operandCount}. Esta instruccion debe usarse sin parametros.",
                        SICXEErrorType.Semantico));
                }
            }
            // Verificar instrucciones que requieren 2 registros
            else if (TwoRegisterInstructions.Contains(operation))
            {
                if (operandCount != 2)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La instruccion '{operation}' requiere exactamente 2 registros separados por coma (formato: REG1,REG2), pero se encontraron {operandCount} operando(s).",
                        SICXEErrorType.Semantico));
                }
                else if (operandContext != null)
                {
                    ValidateRegisterOperands(operandContext, operation, 2);
                }
            }
            // Verificar instrucciones que requieren 1 registro
            else if (OneRegisterInstructions.Contains(operation))
            {
                if (operandCount != 1)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La instruccion '{operation}' requiere exactamente 1 registro (formato: REG), pero se encontraron {operandCount} operando(s).",
                        SICXEErrorType.Semantico));
                }
                else if (operandContext != null)
                {
                    ValidateRegisterOperands(operandContext, operation, 1);
                }
            }
            // Verificar instrucciones registro + número
            else if (RegisterNumberInstructions.Contains(operation))
            {
                if (operandCount != 2)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La instruccion '{operation}' requiere un registro y un numero separados por coma (formato: REG,n), pero se encontraron {operandCount} operando(s).",
                        SICXEErrorType.Semantico));
                }
            }
            // Verificar directivas que requieren operando
            else if (RequiredOperandDirectives.Contains(operation))
            {
                if (operandCount == 0)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La directiva '{operation}' requiere al menos un operando. Esta directiva no puede usarse sin parametros.",
                        SICXEErrorType.Semantico));
                }

                // Validaciones específicas por directiva
                if (operandContext != null)
                    ValidateDirectiveOperands(operation, operandContext, line, column);
            }
            // Verificar directivas con operando opcional (no generar error si falta)
            else if (OptionalOperandDirectives.Contains(operation))
            {
                // END puede tener o no operando, ambos son válidos
                // Si tiene operando, validarlo
                if (operandContext != null && operandCount > 0)
                    ValidateDirectiveOperands(operation, operandContext, line, column);
            }
            // Verificar directivas sin operando
            else if (NoOperandDirectives.Contains(operation))
            {
                if (operandCount > 0)
                {
                    _errors.Add(new SICXEError(line, column,
                        $"La directiva '{operation}' no debe tener operandos. Se encontraron {operandCount} operando(s) cuando no se esperaba ninguno.",
                        SICXEErrorType.Semantico));
                }
            }
            // Instrucciones de formato 3/4 que requieren dirección/operando
            else
            {
                // La mayoría de instrucciones formato 3/4 requieren operando
                if (operandCount == 0 && !operation.Equals("RSUB", StringComparison.OrdinalIgnoreCase))
                {
                    // Algunas instrucciones pueden omitir operando en ciertos casos
                    // pero generalmente requieren uno
                }
            }
        }

        /// <summary>
        /// Obtiene el nombre de la operación desde el contexto
        /// </summary>
        /// //este metodo sirve para extraer el nombre de la operación (instrucción o directiva)
        /// a partir del contexto de la operación en el árbol de análisis sintáctico. y funcona de manera que 
        /// si la operación es una instrucción, devuelve el texto de la instrucción en mayúsculas,
        /// si es una directiva, devuelve el texto de la directiva en mayúsculas,
        /// y si no se encuentra ni instrucción ni directiva, devuelve el texto del contexto de la operación 
        /// (que podría ser un formato 4) sin el prefijo '+' y en mayúsculas. Esto facilita las comparaciones posteriores 
        /// durante el análisis semántico 
        /// al tener un formato uniforme para los nombres de las operaciones.
        private string GetOperationName(SICXEParser.OperationContext operationContext) //recibe como parametro el contexto de la operación,
                                                                                       //que puede ser una instrucción o una directiva,
                                                                                       //y devuelve el nombre de la operación en mayúsculas
                                                                                       //para facilitar las comparaciones posteriores durante el análisis semántico.
        {
            var instructionContext = operationContext.instruction();
            if (instructionContext != null)
            {
                return instructionContext.GetText().ToUpper();
            }
            var directiveContext = operationContext.directive();
            if (directiveContext != null)
            {
                return directiveContext.GetText().ToUpper();
            }
            return operationContext.GetText().TrimStart('+').ToUpper();
        }

        /// <summary>
        /// Valida que los operandos sean registros válidos
        /// </summary>
        private void ValidateRegisterOperands(SICXEParser.OperandContext operandContext, string operation, int expectedCount)
        {
            if (operandContext == null) return;

            var operands = operandContext.operandExpr();
            for (int i = 0; i < Math.Min(operands.Length, expectedCount); i++)
            {
                var operand = operands[i];
                var operandValue = operand.operandValue();
                if (operandValue != null)
                {
                    string text = operandValue.GetText();
                    if (!ValidRegisters.Contains(text))
                    {
                        _errors.Add(new SICXEError(operand.Start.Line, operand.Start.Column,
                            $"'{text}' no es un registro valido para la instruccion '{operation}'. Registros validos: A, X, L, B, S, T, F, PC (o CP), SW.",
                            SICXEErrorType.Semantico));
                    }
                }
            }
        }

        /// <summary>
        /// Validaciones específicas para directivas
        /// </summary>
        /// este meotodo sirve para realizar validaciones
        /// específicas para ciertas directivas que requieren operandos, como BYTE, WORD, RESB, RESW, START y END.
        private void ValidateDirectiveOperands(string directive, SICXEParser.OperandContext operandContext, int line, int column)
        {
            if (operandContext == null) return;

            var operands = operandContext.operandExpr();
            if (operands.Length == 0) return;

            switch (directive.ToUpper())
            {
                case "BYTE":
                    // BYTE debe tener constante hexadecimal o de caracteres
                    var byteOperand = operands[0].GetText();
                    if (!byteOperand.StartsWith("X'", StringComparison.OrdinalIgnoreCase) &&
                        !byteOperand.StartsWith("C'", StringComparison.OrdinalIgnoreCase))
                    {
                        _errors.Add(new SICXEError(line, column,
                            $"BYTE requiere constante hexadecimal (formato: X'..') o de caracteres (formato: C'..'). Se encontro: '{byteOperand}'.",
                            SICXEErrorType.Semantico));
                    }
                    break;

                case "WORD":
                    // WORD debe tener valor numérico
                    var wordOperand = operands[0].GetText();
                    if (!int.TryParse(wordOperand, out _) && !wordOperand.StartsWith("#") && !wordOperand.All(c => char.IsLetterOrDigit(c)))
                    {
                        _errors.Add(new SICXEError(line, column,
                            $"WORD requiere un valor numerico o identificador valido. Se encontro: '{wordOperand}'.",
                            SICXEErrorType.Semantico));
                    }
                    break;

                case "RESB":
                case "RESW":
                    // Deben tener valor numérico
                    var resOperand = operands[0].GetText();
                    if (!int.TryParse(resOperand, out _))
                    {
                        _errors.Add(new SICXEError(line, column,
                            $"{directive} requiere un valor numerico que indique la cantidad de bytes/palabras a reservar. Se encontro: '{resOperand}'.",
                            SICXEErrorType.Semantico));
                    }
                    break;

                case "START":
                    // START debe tener una dirección
                    var startOperand = operands[0].GetText();
                    if (!int.TryParse(startOperand, out _) && !startOperand.All(c => char.IsDigit(c) || "0123456789ABCDEFabcdef".Contains(c)))
                    {
                        _errors.Add(new SICXEError(line, column,
                            $"START requiere una direccion de inicio valida (numero decimal o hexadecimal). Se encontro: '{startOperand}'.",
                            SICXEErrorType.Semantico));
                    }
                    break;

                case "END":
                    // END debe tener una etiqueta de inicio
                    var endOperand = operands[0].GetText();
                    if (string.IsNullOrWhiteSpace(endOperand))
                    {
                        _errors.Add(new SICXEError(line, column,
                            "END requiere especificar la etiqueta del punto de inicio del programa.",
                            SICXEErrorType.Semantico));
                    }
                    break;
            }
        }

        /// <summary>
        /// Genera el reporte de análisis
        /// Se geera un reporte detallado que incluye los tokens reconocidos, 
        /// las etiquetas definidas, los errores encontrados y un resumen de la
        /// cantidad de cada tipo de token y error. El reporte se formatea de manera clara 
        /// y legible para facilitar su interpretación,
        /// se incluye una tabla detallada de todos los tokens procesados durante el análisis léxico, 
        /// excluyendo los tokens de nueva línea y espacios en blanco para enfocarse en los tokens relevantes del programa SIC/XE.
        /// </summary>
        /// los errores se obtienen de la lista de errores del analizador semántico, que se ha ido llenando durante el proceso de análisis.
        public string GenerateReport(string fileName, bool verbose = false)
        {
            var sb = new StringBuilder();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine("                    REPORTE DE ANÁLISIS SIC/XE                      ");
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            sb.AppendLine($"Archivo: {fileName}");
            sb.AppendLine($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm:ss}");

            // Reporte de tokens
            if (verbose)
            {
                sb.AppendLine();
                sb.AppendLine("TOKENS RECONOCIDOS:");
                sb.AppendLine($"{"Línea",-8}{"Columna",-10}{"Token",-20}{"Tipo",-20}");
                
                foreach (var token in _tokens.Where(t => t.TypeName != "NEWLINE" && t.TypeName != "WS"))
                {
                    sb.AppendLine($"{token.Line,-8}{token.Column,-10}{token.Text,-20}{token.TypeName,-20}");
                }
            }

            // Estadísticas de tokens
            sb.AppendLine();
            sb.AppendLine("RESUMEN DE TOKENS:");
            var tokenGroups = _tokens
                .Where(t => t.TypeName != "NEWLINE" && t.TypeName != "WS")
                .GroupBy(t => t.TypeName)
                .OrderByDescending(g => g.Count());
            
            foreach (var group in tokenGroups.Take(10))
            {
                sb.AppendLine($"  {group.Key}: {group.Count()}");
            }
            sb.AppendLine($"  Total tokens: {_tokens.Count(t => t.TypeName != "NEWLINE" && t.TypeName != "WS")}");

            // Etiquetas definidas
            sb.AppendLine();
            sb.AppendLine("ETIQUETAS DEFINIDAS:");
            
            if (_definedLabels.Count > 0)
            {
                foreach (var label in _definedLabels.OrderBy(l => l))
                {
                    sb.AppendLine($"  - {label}");
                }
                sb.AppendLine($"  Total etiquetas: {_definedLabels.Count}");
            }
            else
            {
                sb.AppendLine("  (No se encontraron etiquetas)");
            }

            // Errores
            sb.AppendLine();
            sb.AppendLine("ERRORES ENCONTRADOS:");
            
            if (_errors.Count > 0)
            {
                // Agrupar errores por tipo
                var lexicalErrors = _errors.Where(e => e.Type == SICXEErrorType.Lexico).OrderBy(e => e.Line).ThenBy(e => e.Column);
                var syntaxErrors = _errors.Where(e => e.Type == SICXEErrorType.Sintactico).OrderBy(e => e.Line).ThenBy(e => e.Column);
                var semanticErrors = _errors.Where(e => e.Type == SICXEErrorType.Semantico).OrderBy(e => e.Line).ThenBy(e => e.Column);

                if (lexicalErrors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("  ERRORES LEXICOS:");
                    foreach (var error in lexicalErrors)
                    {
                        sb.AppendLine($"    - {error}");
                    }
                }

                if (syntaxErrors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("  ERRORES SINTACTICOS:");
                    foreach (var error in syntaxErrors)
                    {
                        sb.AppendLine($"    - {error}");
                    }
                }

                if (semanticErrors.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine("  ERRORES SEMANTICOS:");
                    foreach (var error in semanticErrors)
                    {
                        sb.AppendLine($"    - {error}");
                    }
                }

                sb.AppendLine();
                sb.AppendLine($"  Total errores: {_errors.Count} (Lexicos: {lexicalErrors.Count()}, Sintacticos: {syntaxErrors.Count()}, Semanticos: {semanticErrors.Count()})");
            }
            else
            {
                sb.AppendLine("  [OK] No se encontraron errores");
            }

            sb.AppendLine();
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");
            if (_errors.Count == 0)
            {
                sb.AppendLine("        [OK] PROGRAMA VALIDO - Analisis completado sin errores       ");
            }
            else
            {
                sb.AppendLine($"        [ERROR] PROGRAMA CON ERRORES - Se encontraron {_errors.Count} error(es)   ");
            }
            sb.AppendLine("═══════════════════════════════════════════════════════════════════");

            return sb.ToString();
        }

        /// <summary>
        /// Agrega errores externos (léxicos/sintácticos)
        /// </summary>
        public void AddExternalErrors(IEnumerable<SICXEError> errors)
        {
            _errors.AddRange(errors);
        }
    }

    /// <summary>
    /// Información de un token para el reporte
    /// esto es para almacenar información relevante de cada token reconocido por el lexer,
    /// como su posición en el código fuente (línea y columna), su texto, el nombre simbólico
    /// de su tipo y su ID numérico. Esta información se utiliza posteriormente para generar un
    /// reporte detallado de los tokens procesados durante el análisis léxico, lo que ayuda a entender 
    /// mejor cómo se ha interpretado el código fuente y a identificar posibles problemas o patrones en los tokens reconocidos.
    /// </summary>
    public class TokenInfo
    {
        public int Line { get; }
        public int Column { get; }
        public string Text { get; }
        public string TypeName { get; }
        public int TypeId { get; }

        public TokenInfo(int line, int column, string text, string typeName, int typeId)
        {
            Line = line;
            Column = column;
            Text = text;
            TypeName = typeName;
            TypeId = typeId;
        }
    }
}
