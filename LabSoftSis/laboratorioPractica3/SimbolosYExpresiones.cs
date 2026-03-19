using System;
using System.Collections.Generic;

namespace laboratorioPractica3
{
    public enum SymbolType
    {
        Absolute,
        Relative
    }

    /// <summary>
    /// Almacena información sobre un símbolo (etiqueta) en la tabla de símbolos
    /// Incluye: nombre, valor numérico (dirección o constante), tipo (absoluto o relativo)
    /// </summary>
    public class SymbolInfo
    {
        public string Name { get; set; }
        public int Value { get; set; }
        public SymbolType Type { get; set; }

        public SymbolInfo(string name, int value, SymbolType type)
        {
            Name = name;
            Value = value;
            Type = type;
        }

        public override string ToString()
        {
            return $"Nombre: {Name}, Valor: {Value:X4}h, Tipo: {Type}";
        }
    }

    /// <summary>
    /// ╔═══════════════════════════════════════════════════════════════════════════════╗
    /// ║ CLASE: SimbolosYExpresiones - EVALUADOR DE EXPRESIONES Y GESTOR DE SÍMBOLOS  ║
    /// ╚═══════════════════════════════════════════════════════════════════════════════╝
    /// 
    /// RESPONSABILIDADES:
    /// 1. Almacenar y gestionar tabla de símbolos (etiquetas con valores absolutos/relativos)
    /// 2. Evaluar expresiones aritméticas complejas con operadores: +, -, *, /
    /// 3. Validar reglas SIC/XE para combinaciones de símbolos relativos/absolutos
    /// 4. Soportar operadores unarios (-x, +x) y paréntesis
    /// 5. Reconocer múltiples formatos numéricos (decimal, hexadecimal H, 0x, X'...')
    /// 6. Permitir referencias adelantadas (forward references) en Paso 1
    /// 
    /// TIPOS DE SÍMBOLOS:
    /// - Absoluto (Absolute): Constante con valor fijo (ej: 256, 0FFH)
    /// - Relativo (Relative): Dirección de memoria (ej: etiqueta, símbolo)
    /// 
    /// REGLAS DE OPERACIÓN (SIC/XE):
    /// SUMA:
    ///   • Absoluto + Absoluto → Absoluto (suma de constantes)
    ///   • Relativo + Absoluto → Relativo (desplazar dirección)
    ///   • Relativo + Relativo → ERROR (no se suman direcciones)
    /// 
    /// RESTA:
    ///   • Relativo - Relativo → Absoluto (diferencia de direcciones)
    ///   • Absoluto - Absoluto → Absoluto
    ///   • Relativo - Absoluto → Relativo
    ///   • Absoluto - Relativo → ERROR (excepto si Relativo es negativo)
    /// 
    /// MULTIPLICACIÓN/DIVISIÓN:
    ///   • Requiere: ambos operandos sean Absolutos
    ///   • Restricción: "ETIQ * 2" es INVÁLIDO
    /// </summary>
    public class SimbolosYExpresiones
    {
        /// <summary>
        /// TABLA DE SÍMBOLOS: Dictionary que mapea nombres de etiquetas a su información
        /// Clave: nombre del símbolo (case-insensitive)
        /// Valor: SymbolInfo {Name, Value, Type}
        /// Ejemplo: "INICIO" → SymbolInfo(name="INICIO", value=2048, type=Relative)
        /// </summary>
        private readonly Dictionary<string, SymbolInfo> _tablaSímbolos = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, SymbolInfo> GetAllSymbols() => _tablaSímbolos;

        public bool ContainsKey(string name) => _tablaSímbolos.ContainsKey(name);

        public void AddSymbol(string name, int value, SymbolType type)
        {
            _tablaSímbolos[name] = new SymbolInfo(name, value, type);
        }

        public bool TryGetValue(string name, out SymbolInfo info)
        {
            return _tablaSímbolos.TryGetValue(name, out info);
        }

        public int Count => _tablaSímbolos.Count;

        /// Limpia todos los símbolos de la tabla
        public void Clear() => _tablaSímbolos.Clear();

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// MÉTODO PRINCIPAL: EvaluateExpression
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// Evalúa una expresión aritmética compleja respetando precedencia de operadores.
        /// 
        /// PARÁMETROS:
        /// - expression: Cadena con la expresión a evaluar
        ///   Ejemplos: "ETIQ - 10", "10*NUMERO", "SALTO-TAM+1", "(A+B)*2"
        /// - currentAddress: Dirección actual del location counter (PC)
        ///   Usado cuando la expresión contiene "*" (current location)
        /// - allowUndefinedSymbols: true en Paso 1 (permite símbolos no definidos)
        ///                          false en Paso 2 (símbolos deben estar definidos)
        /// 
        /// RETORNA: Tupla (value, type, error)
        /// - value: Valor numérico evaluado (-1 si error)
        /// - type: Tipo del resultado (Absolute o Relative)
        /// - error: Mensaje de error o null si evaluación exitosa
        /// 
        /// PRECEDENCIA DE OPERADORES (de mayor a menor):
        /// 1. Paréntesis: (expresión)
        /// 2. Operadores unarios: -x, +x
        /// 3. Multiplicación/División: *, /
        /// 4. Suma/Resta: +, -
        /// 
        /// EJEMPLOS:
        /// - "100" → (100, Absolute, null)
        /// - "INICIO+10" → (dirección_INICIO+10, Relative, null)
        /// - "FIN-INICIO" → (diferencia, Absolute, null)
        /// - "*" en dirección 2048 → (2048, Relative, null)
        /// </summary>
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            if (string.IsNullOrWhiteSpace(expression)) 
                return (0, SymbolType.Absolute, "Expresión vacía");

            // Tokeniza y evalúa usando parser recursivo descendente (respeta precedencia: *, /, +, -)
            return AnalizarTokenosExpresión(expression, currentAddress, allowUndefinedSymbols);
        }

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// PARSER RECURSIVO DESCENDENTE: AnalizarTokenosExpresión
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// ARQUITECTURA DEL PARSER (Recursive Descent):
        /// Nivel 1: AnalizarSumResta()     ← precedencia más BAJA (se evalúa última)
        /// Nivel 2: AnalizarMultDiv()      ← precedencia MEDIA
        /// Nivel 3: AnalizarFactor()       ← precedencia más ALTA (se evalúa primera)
        /// 
        /// FLUJO:
        /// 1. Tokenizar: dividir expresión en tokens reconocibles
        ///    "10+ETIQ*2" → ["10", "+", "ETIQ", "*", "2"]
        /// 2. Parsear con precedencia correcta:
        ///    - AnalizarSumResta() intenta consumir "+"
        ///    - Si encuentra "+", pide a AnalizarMultDiv() que evalúe el lado derecho
        ///    - AnalizarMultDiv() intenta consumir "*" o "/"
        ///    - AnalizarFactor() maneja: números, símbolos, paréntesis, unarios
        /// 3. Validar que se consumieron TODOS los tokens (no debe haber tokens sobrantes)
        /// 
        /// VALIDACIÓN SIC/XE:
        /// - Suma: Relativo+Relativo=ERROR
        /// - Resta: Relativo-Relativo=Absoluto (componentes relativas se cancelan)
        /// - Multiplicación: ambos deben ser Absolutos
        /// </summary>
        private (int value, SymbolType type, string? error) AnalizarTokenosExpresión(string expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            try
            {
                // Paso 1: Tokenizar la expresión (dividir en símbolos reconocibles)
                var tokens = Tokenizar(expression);
                int posición = 0;

                // Paso 2: Parsear respetando precedencia de operadores
                var (valor, tipo, error) = AnalizarSumResta(tokens, ref posición, currentAddress, allowUndefinedSymbols);
                if (error != null) return (-1, SymbolType.Absolute, error);

                // Paso 3: Verificar que se consumieron todos los tokens
                if (posición < tokens.Count) 
                    return (-1, SymbolType.Absolute, "Tokens adicionales inesperados");

                return (valor, tipo, null);
            }
            catch (Exception ex)
            {
                return (-1, SymbolType.Absolute, $"Error al evaluar la expresión: {ex.Message}");
            }
        }

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// TOKENIZACIÓN: Tokenizar
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// Divide una expresión en tokens individuales, descartando espacios en blanco.
        /// 
        /// ENTRADA:  "10 + ETIQ * 2"
        /// SALIDA:   ["10", "+", "ETIQ", "*", "2"]
        /// 
        /// REGLAS:
        /// - Los operadores (+, -, *, /, (, )) son tokens separados
        /// - Los espacios en blanco se ignoran
        /// - Las secuencias de dígitos/letras/números forman un único token
        ///   (ej: "ETIQ" es un token, "0FFH" es un token, "100" es un token)
        /// </summary>
        private List<string> Tokenizar(string expression)
        {
            var tokens = new List<string>();
            string actual = "";
            foreach (char carácter in expression)
            {
                if (char.IsWhiteSpace(carácter)) continue;
                if ("+-*/()".Contains(carácter))
                {
                    if (actual != "") tokens.Add(actual);
                    tokens.Add(carácter.ToString());
                    actual = "";
                }
                else
                {
                    actual += carácter;
                }
            }
            if (actual != "") tokens.Add(actual);
            return tokens;
        }

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// PRECEDENCIA BAJA: AnalizarSumResta (+, -)
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// Procesa operadores + y - (precedencia BAJA, se evalúan ÚLTIMOS)
        /// 
        /// ESTRUCTURA:
        ///  suma_resta = mult_div (("+" | "-") mult_div)*
        /// 
        /// FLUJO:
        /// 1. Evalúa lado izquierdo: AnalizarMultDiv()
        /// 2. MIENTRAS haya "+" o "-":
        ///    a. Consume el operador
        ///    b. Evalúa lado derecho: AnalizarMultDiv()
        ///    c. Aplica regla SIC/XE y combina resultados
        /// 3. Retorna valor final y tipo
        /// 
        /// VALIDACIÓN - REGLAS SIC/XE PARA SUMA:
        /// ┌─────────────────────────────────────────────────┐
        /// │ Absoluto + Absoluto   → Absoluto                │
        /// │ Absoluto + Relativo   → Relativo                │
        /// │ Relativo + Absoluto   → Relativo                │
        /// │ Relativo + Relativo   → ERROR ✗                │
        /// └─────────────────────────────────────────────────┘
        /// 
        /// VALIDACIÓN - REGLAS SIC/XE PARA RESTA:
        /// ┌─────────────────────────────────────────────────┐
        /// │ Absoluto - Absoluto   → Absoluto                │
        /// │ Relativo - Absoluto   → Relativo                │
        /// │ Relativo - Relativo   → Absoluto (se cancelan) │
        /// │ Absoluto - Relativo   → ERROR (excepto negativo)│
        /// └─────────────────────────────────────────────────┘
        /// 
        /// EJEMPLO:
        /// "INICIO + 10 - 5" = ((INICIO+10)-5)
        ///                   = (2048+10-5) = 2053 Relativo
        /// </summary>
        private (int value, SymbolType type, string? error) AnalizarSumResta(List<string> tokens, ref int posición, int pc, bool allowUndefinedSymbols = false)
        {
            var (valorIzq, tipoIzq, error) = AnalizarMultDiv(tokens, ref posición, pc, allowUndefinedSymbols);
            if (error != null) return (valorIzq, tipoIzq, error);

            while (posición < tokens.Count && (tokens[posición] == "+" || tokens[posición] == "-"))
            {
                string operador = tokens[posición++];
                var (valorDer, tipoDer, errorDer) = AnalizarMultDiv(tokens, ref posición, pc, allowUndefinedSymbols);
                if (errorDer != null) return (-1, SymbolType.Absolute, errorDer);

                // ════════════════ REGLAS DE SUMA (SIC/XE) ════════════════
                if (operador == "+")
                {
                    // Regla 1: Relative + Relative = ERROR (no se puede sumar dos direcciones relativas)
                    if (tipoIzq == SymbolType.Relative && tipoDer == SymbolType.Relative)
                        return (-1, SymbolType.Absolute, "No se permite sumar dos términos relativos");

                    // Regla 2: Absolute + Absolute = Absolute
                    if (tipoIzq == SymbolType.Absolute && tipoDer == SymbolType.Absolute)
                    {
                        valorIzq += valorDer;
                        tipoIzq = SymbolType.Absolute;
                    }
                    else // Regla 3: Uno absoluto y uno relativo = Relativo (resultado es una dirección)
                    {
                        valorIzq += valorDer;
                        tipoIzq = SymbolType.Relative;
                    }
                }
                // ════════════════ REGLAS DE RESTA (SIC/XE) ════════════════
                else if (operador == "-")
                {
                    // Regla 1: Relative - Relative = Absolute (se cancelan componentes relativas)
                    // Ejemplo: ETIQ1 - ETIQ2 = diferencia en bytes (distancia)
                    if (tipoIzq == SymbolType.Relative && tipoDer == SymbolType.Relative)
                    {
                        valorIzq -= valorDer;
                        tipoIzq = SymbolType.Absolute;
                    }
                    // Regla 2: Absolute - Absolute = Absolute
                    else if (tipoIzq == SymbolType.Absolute && tipoDer == SymbolType.Absolute)
                    {
                        valorIzq -= valorDer;
                        tipoIzq = SymbolType.Absolute;
                    }
                    // Regla 3: Relative - Absolute = Relative
                    // Ejemplo: ETIQ - 10 (mover una dirección 10 bytes atrás)
                    else if (tipoIzq == SymbolType.Relative && tipoDer == SymbolType.Absolute)
                    {
                        valorIzq -= valorDer;
                        tipoIzq = SymbolType.Relative;
                    }
                    // Regla 4: Absolute - Relative solo es válido si es una resta de número negado
                    // A - (-B) = A + B (restar un negativo = sumar)
                    else if (tipoIzq == SymbolType.Absolute && tipoDer == SymbolType.Relative)
                    {
                        if (valorDer < 0)
                        {
                            // Es válido: Absolute - (-Relative) → A + (-B) con B negativo
                            valorIzq -= valorDer;
                            tipoIzq = SymbolType.Relative;
                        }
                        else
                        {
                            // INVÁLIDO: No se puede restar una dirección relativa de un valor absoluto
                            return (-1, SymbolType.Absolute, "No se permite restar un término relativo de uno absoluto (Absoluto - Relativo)");
                        }
                    }
                }
            }
            return (valorIzq, tipoIzq, null);
        }

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// PRECEDENCIA MEDIA: AnalizarMultDiv (*, /)
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// Procesa operadores * y / (precedencia MEDIA, se evalúan ANTES que +/-)
        /// 
        /// ESTRUCTURA:
        ///  mult_div = factor (("*" | "/") factor)*
        /// 
        /// RESTRICCIÓN SIC/XE CRÍTICA:
        /// ┌──────────────────────────────────────────────────────────┐
        /// │ AMBOS OPERANDOS DEBEN SER ABSOLUTOS                      │
        /// │ No se pueden multiplicar/dividir direcciones (símbolos)  │
        /// │                                                          │
        /// │ ✓ VÁLIDO:   10 * 2         (Absoluto * Absoluto)        │
        /// │ ✗ INVÁLIDO: ETIQ * 2       (Relativo * Absoluto)        │
        /// │ ✗ INVÁLIDO: INICIO + FIN * 5  (mezcla de tipos)         │
        /// └──────────────────────────────────────────────────────────┘
        /// 
        /// CASOS ESPECIALES:
        /// - División por cero: Retorna error
        /// - Resultado siempre es Absoluto (si ambos operandos son Absolutos)
        /// </summary>
        private (int value, SymbolType type, string? error) AnalizarMultDiv(List<string> tokens, ref int posición, int pc, bool allowUndefinedSymbols = false)
        {
            var (valorIzq, tipoIzq, error) = AnalizarFactor(tokens, ref posición, pc, allowUndefinedSymbols);
            if (error != null) return (valorIzq, tipoIzq, error);

            while (posición < tokens.Count && (tokens[posición] == "*" || tokens[posición] == "/"))
            {
                string operador = tokens[posición++];
                var (valorDer, tipoDer, errorDer) = AnalizarFactor(tokens, ref posición, pc, allowUndefinedSymbols);
                if (errorDer != null) return (-1, SymbolType.Absolute, errorDer);

                // ════════════════ RESTRICCIÓN MULTIPLICACIÓN/DIVISIÓN ════════════════
                // Ambos operandos DEBEN ser absolutos (constantes o valores definidos como absolutos)
                // No puedes multiplicar una dirección: "ETIQ * 2" es INVÁLIDO en SIC/XE
                if (tipoIzq == SymbolType.Relative || tipoDer == SymbolType.Relative)
                {
                    return (-1, SymbolType.Absolute, "Los términos en multiplicaciones y divisiones deben ser absolutos");
                }

                if (operador == "*")
                {
                    valorIzq *= valorDer;
                }
                else
                {
                    if (valorDer == 0) 
                        return (-1, SymbolType.Absolute, "División por cero");
                    valorIzq /= valorDer;
                }
            }
            return (valorIzq, tipoIzq, null);
        }

        /// <summary>
        /// ═══════════════════════════════════════════════════════════════════════
        /// PRECEDENCIA ALTA: AnalizarFactor (números, símbolos, paréntesis, unarios)
        /// ═══════════════════════════════════════════════════════════════════════
        /// 
        /// Procesa elementos individuales de la expresión (precedencia ALTA, se evalúan PRIMEROS)
        /// 
        /// ESTRUCTURA:
        ///  factor = ("+" | "-")? (
        ///     "(" expresión ")"              [paréntesis con expresión completa]
        ///   | "*"                             [símbolo especial: current location]
        ///   | SÍMBOLO                         [búsqueda en tabla de símbolos]
        ///   | NÚMERO                          [múltiples formatos]
        ///  )
        /// 
        /// OPERADORES UNARIOS:
        /// - "+x" → valor de x (sin cambios)
        /// - "-x" → negación de x
        /// Pueden combinarse: "--x" = x (doble negación)
        /// 
        /// FORMATO ESPECIAL "*" (CURRENT LOCATION):
        /// En SIC/XE: "*" representa la dirección actual del location counter
        /// Ejemplo: "TAM EQU * - INICIO"  (tamaño del programa)
        /// Retorna: (currentAddress, Relativo)
        /// 
        /// BÚSQUEDA DE SÍMBOLOS:
        /// Si el token está en _tablaSímbolos, retorna su valor y tipo
        /// Si no está y allowUndefinedSymbols=false, retorna error
        /// Si no está y allowUndefinedSymbols=true (Paso 1), retorna (0, Relativo) como placeholder
        /// 
        /// FORMATOS NUMÉRICOS SOPORTADOS:
        /// 1. Decimal:       "100", "255", "2048"
        /// 2. Hexadecimal H: "0FFH", "100H", "ABCH"  (sufijo H)
        /// 3. Hexadecimal 0x: "0xFF", "0x100"         (prefijo 0x, case-insensitive)
        /// 4. Hexadecimal X': "X'0F0F'", "X'FF00'"    (formato SIC/XE)
        /// 
        /// EJEMPLOS:
        /// - AnalizarFactor(["100"]) → (100, Absolute, null)
        /// - AnalizarFactor(["INICIO"]) → (2048, Relative, null)
        /// - AnalizarFactor(["("...")"]) → evalúa expresión dentro paréntesis
        /// - AnalizarFactor(["-", "10"]) → (-10, Absolute, null)
        /// </summary>
        private (int value, SymbolType type, string? error) AnalizarFactor(List<string> tokens, ref int posición, int pc, bool allowUndefinedSymbols = false)
        {
            if (posición >= tokens.Count) 
                return (-1, SymbolType.Absolute, "Expresión mal formada");

            string token = tokens[posición];

            // ════════════════ OPERADORES UNARIOS ════════════════
            // Maneja +x y -x (ej: -10, +ETIQ, -(3*2))
            if (token == "+" || token == "-")
            {
                string operadorUnario = token;
                posición++;  // Consumir el operador

                var (valorResultado, tipoResultado, errorResultado) = AnalizarFactor(tokens, ref posición, pc, allowUndefinedSymbols);
                if (errorResultado != null) return (valorResultado, tipoResultado, errorResultado);

                // Aplicar negación si es - unario
                if (operadorUnario == "-")
                    valorResultado = -valorResultado;

                return (valorResultado, tipoResultado, null);
            }

            posición++;  // Consumir el token

            // ════════════════ PARÉNTESIS ════════════════
            // Recursivamente evalúa expresión dentro de paréntesis
            if (token == "(")
            {
                var (valorExpr, tipoExpr, errorExpr) = AnalizarSumResta(tokens, ref posición, pc, allowUndefinedSymbols);
                if (errorExpr != null) return (valorExpr, tipoExpr, errorExpr);
                if (posición >= tokens.Count || tokens[posición++] != ")") 
                    return (-1, SymbolType.Absolute, "Se esperaba ')'");
                return (valorExpr, tipoExpr, null);
            }

            // ════════════════ CONTLOC ACTUAL "*" ════════════════
            // En SIC/XE: "*" significa "dirección actual del location counter"
            // Se usa en expresiones como: TAM EQU * (marca la dirección actual como final)
            if (token == "*")
                return (pc, SymbolType.Relative, null);

            // ════════════════ BÚSQUEDA DE SÍMBOLO EN TABLA ════════════════
            if (_tablaSímbolos.TryGetValue(token, out var infoSímbolo))
                return (infoSímbolo.Value, infoSímbolo.Type, null);

            // ════════════════ PARSING DE NÚMEROS ════════════════
            int valorParsed;

            // Formato hexadecimal con sufijo H: "0FFH", "100H"
            if (token.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(token.Substring(0, token.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out valorParsed))
                    return (valorParsed, SymbolType.Absolute, null);
            }
            // Formato hexadecimal con prefijo 0x: "0x100", "0xFF"
            else if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out valorParsed))
                    return (valorParsed, SymbolType.Absolute, null);
            }
            // Formato SIC/XE hexadecimal: X'0F0F'
            else if (token.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && token.EndsWith("'"))
            {
                if (int.TryParse(token.Substring(2, token.Length - 3), System.Globalization.NumberStyles.HexNumber, null, out valorParsed))
                    return (valorParsed, SymbolType.Absolute, null);
            }
            // Formato decimal: "100", "255"
            else if (int.TryParse(token, out valorParsed))
            {
                return (valorParsed, SymbolType.Absolute, null);
            }

            // ════════════════ SÍMBOLO NO DEFINIDO ════════════════
            if (allowUndefinedSymbols)
            {
                // En Paso 1, permite forward references (referencias a símbolos no definidos aún)
                // Retorna 0 como placeholder relativo (será calculado después en Paso 2)
                return (0, SymbolType.Relative, null);
            }

            return (-1, SymbolType.Absolute, $"Símbolo no definido o valor inválido: {token}");
        }
    }
}
