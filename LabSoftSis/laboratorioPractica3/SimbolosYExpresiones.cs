using System;
using System.Collections.Generic;
using System.Globalization;

namespace laboratorioPractica3
{
    public enum SymbolType
    {
        Absolute,
        Relative
    }

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
    /// Gestor de símbolos y evaluador de expresiones del ensamblador SIC/XE.
    /// </summary>
    public class SimbolosYExpresiones
    {
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

        // Evaluador de expresiones aritmeticas (+, -, *, /, ()).
        // Requisitos 5-7: soporta simbolos y constantes, valida reglas de apareamiento
        // y permite EQU con expresion o con * (contador actual).
        // allowUndefinedSymbols: si true, permite simbolos no definidos (para Paso 1)
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return (0, SymbolType.Absolute, "Expresión vacía");

            return ParseExpressionTokens(expression, currentAddress, allowUndefinedSymbols);
        }

        private (int value, SymbolType type, string? error) ParseExpressionTokens(string expression, int currentAddress, bool allowUndefinedSymbols)
        {
            try
            {
                var tokens = Tokenize(expression);
                int pos = 0;

                var (value, relCount, error) = ParseAddSubInternal(tokens, ref pos, currentAddress, allowUndefinedSymbols);
                if (error != null)
                    return (-1, SymbolType.Absolute, error);

                if (pos < tokens.Count)
                    return (-1, SymbolType.Absolute, "Tokens adicionales inesperados");

                // Regla final SIC/XE:
                // relCount == 0 -> Absolute
                // relCount == 1 -> Relative
                // relCount < 0  -> ERROR (relativo negativo sin cancelar)
                // relCount > 1  -> ERROR (múltiples relativos positivos)
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
                    leftVal += rightVal;
                    leftRelCount += rightRelCount;
                }
                else
                {
                    leftVal -= rightVal;
                    leftRelCount -= rightRelCount;
                }
            }

            return (leftVal, leftRelCount, null);
        }

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

                // Regla SIC/XE: relativos no pueden participar en * ni /
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

        private (int val, int relCount, string? err) ParseFactorInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            if (pos >= tokens.Count)
                return (-1, 0, "Expresión mal formada");

            string token = tokens[pos];

            // Procesar unario + o -
            if (token == "+" || token == "-")
            {
                string unary = token;
                pos++;

                var (value, relCount, err) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (err != null)
                    return (value, relCount, err);

                if (unary == "-")
                {
                    value = -value;
                    relCount = -relCount;
                }

                return (value, relCount, null);
            }

            pos++;

            // Paréntesis
            if (token == "(")
            {
                var (exprValue, exprRelCount, exprErr) = ParseAddSubInternal(tokens, ref pos, pc, allowUndefinedSymbols);
                if (exprErr != null)
                    return (exprValue, exprRelCount, exprErr);

                if (pos >= tokens.Count || tokens[pos++] != ")")
                    return (-1, 0, "Se esperaba ')'");

                return (exprValue, exprRelCount, null);
            }

            // Contador de ubicación
            if (token == "*")
                return (pc, 1, null);

            // Símbolo
            if (_symbolTable.TryGetValue(token, out var symbolInfo))
                return (symbolInfo.Value, symbolInfo.Type == SymbolType.Relative ? 1 : 0, null);

            // Número
            if (TryParseTokenAsNumber(token, out int parsed))
                return (parsed, 0, null);

            // Símbolo indefinido
            if (allowUndefinedSymbols)
                return (0, 0, null);

            return (-1, 0, $"Símbolo no definido o valor inválido: {token}");
        }



        private static bool TryParseTokenAsNumber(string token, out int value)
        {
            value = 0;

            if (token.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(token.Substring(0, token.Length - 1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return int.TryParse(token.Substring(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            if (token.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && token.EndsWith("'"))
            {
                return int.TryParse(token.Substring(2, token.Length - 3), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out value);
            }

            return int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
        }
    }
}

