using System;
using System.Collections.Generic;
using System.Globalization;

namespace laboratorioPractica3
{
    // Tipos de símbolos en SIC/XE: Absoluto (constante) o Relativo (dirección)
    public enum SymbolType
    {
        Absolute,   // Valor fijo, no depende de dirección de carga
        Relative    // Dirección dentro del programa, depende de dónde se cargue
    }

    // Información de un símbolo: nombre, valor y tipo
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

    // Gestor de símbolos y evaluador de expresiones del ensamblador SIC/XE
    // Permite: operaciones aritméticas (+, -, *, /), paréntesis, símbolos, constantes y * (contador actual)
    // Valida reglas de apareamiento Absoluto-Relativo según especificación SIC/XE
    public class SimbolosYExpresiones
    {
        // Tabla de símbolos: almacena todos los símbolos definidos (case-insensitive)
        private readonly Dictionary<string, SymbolInfo> _symbolTable = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, SymbolInfo> GetAllSymbols() => _symbolTable;

        public bool ContainsKey(string name) => _symbolTable.ContainsKey(name);

        public void AddSymbol(string name, int value, SymbolType type)
        {
            _symbolTable[name] = new SymbolInfo(name, value, type);
        }

        public bool TryGetValue(string name, out SymbolInfo info)
        {
            return _symbolTable.TryGetValue(name, out info!);
        }

        public int Count => _symbolTable.Count;

        public void Clear() => _symbolTable.Clear();

        // Evalúa expresiones aritméticas: +, -, *, /, ()
        // Parámetros:
        //   expression: la expresión a evaluar
        //   currentAddress: valor de * (contador de ubicación actual)
        //   allowUndefinedSymbols: si true, permite símbolos no definidos (útil para Paso 1)
        // Retorna: (valor, tipo de resultado, mensaje de error si hay)
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return (0, SymbolType.Absolute, "Expresión vacía");

            return ParseExpressionTokens(expression, currentAddress, allowUndefinedSymbols);
        }

        // Procesa los tokens de la expresión mediante parser recursivo descendente
        private (int value, SymbolType type, string? error) ParseExpressionTokens(string expression, int currentAddress, bool allowUndefinedSymbols)
        {
            try
            {
                var tokens = Tokenize(expression);
                int pos = 0;

                // El parser usa relCount: cuenta de términos relativos
                // relCount = 0 -> resultado es Absoluto
                // relCount = 1 -> resultado es Relativo
                // relCount != 0,1 -> error (mezcla inválida de relativos)
                var (value, relCount, error) = ParseAddSubInternal(tokens, ref pos, currentAddress, allowUndefinedSymbols);
                if (error != null)
                    return (-1, SymbolType.Absolute, error);

                if (pos < tokens.Count)
                    return (-1, SymbolType.Absolute, "Tokens adicionales inesperados");

                // Determinar tipo final según relCount
                if (relCount == 0)
                    return (value, SymbolType.Absolute, null);
                if (relCount == 1)
                    return (value, SymbolType.Relative, null);
                if (relCount < 0)
                    return (-1, SymbolType.Absolute, "La expresión deja un término relativo negativo sin cancelar");

                return (-1, SymbolType.Absolute, "La expresión contiene más de un término relativo positivo sin cancelar");
            }
            catch (Exception ex)
            {
                return (-1, SymbolType.Absolute, $"Error al evaluar la expresión: {ex.Message}");
            }
        }

        // Divide la expresión en tokens: números, símbolos, operadores
        private List<string> Tokenize(string expression)
        {
            var tokens = new List<string>();
            string current = "";

            foreach (char ch in expression)
            {
                if (char.IsWhiteSpace(ch))
                    continue;

                if ("+-*/()".Contains(ch))
                {
                    if (current.Length > 0)
                        tokens.Add(current);

                    tokens.Add(ch.ToString());
                    current = "";
                }
                else
                {
                    current += ch;
                }
            }

            if (current.Length > 0)
                tokens.Add(current);

            return tokens;
        }

        // Procesa sumas y restas: precedencia más baja
        // Usa método de conteo de relativos:
        //   - Sumar relCount: A + B suma sus contadores relativos
        //   - Restar relCount: A - B resta sus contadores (puede cancelar términos relativos)
        private (int val, int relCount, string? err) ParseAddSubInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            var (leftVal, leftRelCount, err) = ParseMulDivInternal(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null)
                return (leftVal, leftRelCount, err);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                var (rightVal, rightRelCount, rightErr) = ParseMulDivInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (rightErr != null)
                    return (-1, 0, rightErr);

                if (op == "+")
                {
                    // Suma: acumula valores y contadores relativos
                    leftVal += rightVal;
                    leftRelCount += rightRelCount;
                }
                else
                {
                    // Resta: resta valores y contadores (cancela términos relativos si es posible)
                    leftVal -= rightVal;
                    leftRelCount -= rightRelCount;
                }
            }

            return (leftVal, leftRelCount, null);
        }

        // Procesa multiplicaciones y divisiones: precedencia más alta que +/-
        // Restricción SIC/XE: los operandos DEBEN ser absolutos (relCount = 0)
        private (int val, int relCount, string? err) ParseMulDivInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            var (leftVal, leftRelCount, err) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null)
                return (leftVal, leftRelCount, err);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                var (rightVal, rightRelCount, rightErr) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (rightErr != null)
                    return (-1, 0, rightErr);

                // Validar que ambos operandos sean absolutos
                if (leftRelCount != 0 || rightRelCount != 0)
                    return (-1, 0, "Los términos en multiplicaciones y divisiones deben ser absolutos");

                if (op == "*")
                {
                    leftVal *= rightVal;
                }
                else
                {
                    if (rightVal == 0)
                        return (-1, 0, "División por cero");

                    leftVal /= rightVal;
                }
            }

            return (leftVal, leftRelCount, null);
        }

        // Procesa factores: números, símbolos, *, paréntesis y operadores unarios +/-
        private (int val, int relCount, string? err) ParseFactorInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            if (pos >= tokens.Count)
                return (-1, 0, "Expresión mal formada");

            string token = tokens[pos];

            // Manejo de operadores unarios: + y -
            if (token == "+" || token == "-")
            {
                string unary = token;
                pos++;

                var (value, relCount, err) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (err != null)
                    return (value, relCount, err);

                // El - unario niega ambos: valor y conteo de relativos
                if (unary == "-")
                {
                    value = -value;
                    relCount = -relCount;
                }

                return (value, relCount, null);
            }

            pos++;

            // Paréntesis: evaluar subexpresión recursivamente
            if (token == "(")
            {
                var (exprValue, exprRelCount, exprErr) = ParseAddSubInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (exprErr != null)
                    return (exprValue, exprRelCount, exprErr);

                if (pos >= tokens.Count || tokens[pos++] != ")")
                    return (-1, 0, "Se esperaba ')'");

                return (exprValue, exprRelCount, null);
            }

            // Contador de ubicación: * = dirección actual
            if (token == "*")
                return (pc, 1, null);

            // Símbolo conocido
            if (_symbolTable.TryGetValue(token, out var symbolInfo))
                return (symbolInfo.Value, symbolInfo.Type == SymbolType.Relative ? 1 : 0, null);

            // Número (decimal, hexadecimal con H, 0x, X'...')
            if (TryParseTokenAsNumber(token, out int parsed))
                return (parsed, 0, null);

            // Símbolo no definido
            if (allowUndefinedSymbols)
                return (0, 0, null);

            return (-1, 0, $"Símbolo no definido o valor inválido: {token}");
        }

        // Intenta parsear un token como número en diferentes formatos
        private static bool TryParseTokenAsNumber(string token, out int value)
        {
            value = 0;

            // Hexadecimal con sufijo H: 1A2BH
            if (token.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(token.Substring(0, token.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            // Hexadecimal con prefijo 0x: 0x1A2B
            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            // Hexadecimal SIC/XE: X'1A2B'
            if (token.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && token.EndsWith("'"))
            {
                return int.TryParse(token.Substring(2, token.Length - 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            // Número decimal
            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}

