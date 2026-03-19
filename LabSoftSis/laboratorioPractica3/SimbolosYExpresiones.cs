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

                var (value, type, error) = ParseAddSub(tokens, ref pos, currentAddress, allowUndefinedSymbols);
                if (error != null)
                    return (-1, SymbolType.Absolute, error);

                if (pos < tokens.Count)
                    return (-1, SymbolType.Absolute, "Tokens adicionales inesperados");

                return (value, type, null);
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

        private (int val, SymbolType type, string? err) ParseAddSub(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            var (leftVal, leftType, err) = ParseMulDiv(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null)
                return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseMulDiv(tokens, ref pos, pc, allowUndefinedSymbols);
                if (rightErr != null)
                    return (-1, SymbolType.Absolute, rightErr);

                if (op == "+")
                {
                    if (leftType == SymbolType.Relative && rightType == SymbolType.Relative)
                        return (-1, SymbolType.Absolute, "No se permite sumar dos términos relativos");

                    leftVal += rightVal;
                    leftType = (leftType == SymbolType.Relative || rightType == SymbolType.Relative)
                        ? SymbolType.Relative
                        : SymbolType.Absolute;
                }
                else
                {
                    if (leftType == SymbolType.Relative && rightType == SymbolType.Relative)
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Absolute;
                    }
                    else if (leftType == SymbolType.Absolute && rightType == SymbolType.Absolute)
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Absolute;
                    }
                    else if (leftType == SymbolType.Relative && rightType == SymbolType.Absolute)
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Relative;
                    }
                    else
                    {
                        if (rightVal < 0)
                        {
                            leftVal -= rightVal;
                            leftType = SymbolType.Relative;
                        }
                        else
                        {
                            return (-1, SymbolType.Absolute, "No se permite restar un término relativo de uno absoluto (Absoluto - Relativo)");
                        }
                    }
                }
            }

            return (leftVal, leftType, null);
        }

        private (int val, SymbolType type, string? err) ParseMulDiv(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            var (leftVal, leftType, err) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null)
                return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
                if (rightErr != null)
                    return (-1, SymbolType.Absolute, rightErr);

                if (leftType == SymbolType.Relative || rightType == SymbolType.Relative)
                    return (-1, SymbolType.Absolute, "Los términos en multiplicaciones y divisiones deben ser absolutos");

                if (op == "*")
                {
                    leftVal *= rightVal;
                }
                else
                {
                    if (rightVal == 0)
                        return (-1, SymbolType.Absolute, "División por cero");

                    leftVal /= rightVal;
                }
            }

            return (leftVal, leftType, null);
        }

        private (int val, SymbolType type, string? err) ParseFactor(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols)
        {
            if (pos >= tokens.Count)
                return (-1, SymbolType.Absolute, "Expresión mal formada");

            string token = tokens[pos];

            if (token == "+" || token == "-")
            {
                string unary = token;
                pos++;

                var (value, type, err) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
                if (err != null)
                    return (value, type, err);

                if (unary == "-")
                    value = -value;

                return (value, type, null);
            }

            pos++;

            if (token == "(")
            {
                var (exprValue, exprType, exprErr) = ParseAddSub(tokens, ref pos, pc, allowUndefinedSymbols);
                if (exprErr != null)
                    return (exprValue, exprType, exprErr);

                if (pos >= tokens.Count || tokens[pos++] != ")")
                    return (-1, SymbolType.Absolute, "Se esperaba ')'");

                return (exprValue, exprType, null);
            }

            if (token == "*")
                return (pc, SymbolType.Relative, null);

            if (_symbolTable.TryGetValue(token, out var symbolInfo))
                return (symbolInfo.Value, symbolInfo.Type, null);

            if (TryParseTokenAsNumber(token, out int parsed))
                return (parsed, SymbolType.Absolute, null);

            if (allowUndefinedSymbols)
                return (0, SymbolType.Relative, null);

            return (-1, SymbolType.Absolute, $"Símbolo no definido o valor inválido: {token}");
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

