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
        private readonly Dictionary<string, SymbolInfo> _tabsim = new(StringComparer.OrdinalIgnoreCase);
        
        // Tabla de simbolos extendida: guarda valor y tipo (absoluto/relativo)
        // Requisito 1: cada simbolo queda marcado con su tipo.
        public IReadOnlyDictionary<string, SymbolInfo> GetAllSymbols() => _tabsim;

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

        // Evaluador de expresiones aritmeticas (+, -, *, /, ()).
        // Requisitos 5-7: soporta simbolos y constantes, valida reglas de apareamiento
        // y permite EQU con expresion o con * (contador actual).
        // allowUndefinedSymbols: si true, permite simbolos no definidos (para Paso 1)
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            // Punto único para evaluar expresiones SIC/XE.
            // Retorna: valor numérico + tipo del término resultante (Absolute/Relative).
            if (string.IsNullOrWhiteSpace(expression)) return (0, SymbolType.Absolute, "Expresión vacía");

            // TODO: Un parser simple de la expresión para calcular su valor evaluando *, símbolos, y constantes
            return ParseExpressionTokens(expression, currentAddress, allowUndefinedSymbols);
        }

        // Parser de expresiones: devuelve valor + tipo (absoluto/relativo) o error
        private (int value, SymbolType type, string? error) ParseExpressionTokens(string expr, int currentAddress, bool allowUndefinedSymbols = false)
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

        // Suma/Resta: valida reglas SIC/XE de combinacion de simbolos
        private (int val, SymbolType type, string? err) ParseAddSub(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            // Implementa reglas de apareamiento A/R en suma-resta.
            // Aquí se decide si el resultado final es absoluto o relativo.
            var (leftVal, leftType, err) = ParseMulDiv(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null) return (leftVal, leftType, err);

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
            return (leftVal, leftType, null);
        }

        // Multiplicacion/Division: solo valores absolutos (regla SIC/XE)
        private (int val, SymbolType type, string? err) ParseMulDiv(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            // Regla SIC/XE: en * y / solo participan términos absolutos.
            var (leftVal, leftType, err) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null) return (leftVal, leftType, err);

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
                    if (rightVal == 0) return (-1, SymbolType.Absolute, "División por cero");
                    leftVal /= rightVal;
                }
            }
            return (leftVal, leftType, null);
        }

        // Factor: simbolo, constante, paréntesis o '*' (contador actual)
        private (int val, SymbolType type, string? err) ParseFactor(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            // Factor elemental: número, símbolo, '*', paréntesis u operador unario.
            // '*' representa el valor actual del contador de ubicación (relativo).
            if (pos >= tokens.Count) return (-1, SymbolType.Absolute, "Expresión mal formada");

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
