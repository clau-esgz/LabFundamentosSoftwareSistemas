using System;
using System.Collections.Generic;

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

    public class SimbolosYExpresiones
    {
        private readonly Dictionary<string, SymbolInfo> _tabsim = new(StringComparer.OrdinalIgnoreCase);
        
        public IReadOnlyDictionary<string, SymbolInfo> GetAllSymbols() => _tabsim;

        public bool ContainsKey(string name) => _tabsim.ContainsKey(name);
        
        public void AddSymbol(string name, int value, SymbolType type)
        {
            _tabsim[name] = new SymbolInfo(name, value, type);
        }

        public bool TryGetValue(string name, out SymbolInfo info)
        {
            return _tabsim.TryGetValue(name, out info);
        }

        public int Count => _tabsim.Count;

        // Limpiar la tabla
        public void Clear() => _tabsim.Clear();

        // Evaluador de expresiones
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress)
        {
            if (string.IsNullOrWhiteSpace(expression)) return (0, SymbolType.Absolute, "Expresión vacía");

            // TODO: Un parser simple de la expresión para calcular su valor evaluando *, símbolos, y constantes
            return ParseExpressionTokens(expression, currentAddress);
        }

        private (int value, SymbolType type, string? error) ParseExpressionTokens(string expr, int currentAddress)
        {
            // Implementa un parser recursivo descendente simple o un evaluador postfix aquí.
            // Para simplificar, tokenize and evaluate.
            try
            {
                var tokens = Tokenize(expr);
                int pos = 0;
                var (val, type, err) = ParseAddSub(tokens, ref pos, currentAddress);
                if (err != null) return (-1, SymbolType.Absolute, err);
                if (pos < tokens.Count) return (-1, SymbolType.Absolute, "Tokens adicionales inesperados");
                return (val, type, null);
            }
            catch (Exception ex)
            {
                return (-1, SymbolType.Absolute, $"Error al evaluar la expresión: {ex.Message}");
            }
        }

        private List<string> Tokenize(string expr)
        {
            var tokens = new List<string>();
            string current = "";
            foreach (char c in expr)
            {
                if (char.IsWhiteSpace(c)) continue;
                if ("+-*/()".Contains(c))
                {
                    if (current != "") tokens.Add(current);
                    tokens.Add(c.ToString());
                    current = "";
                }
                else
                {
                    current += c;
                }
            }
            if (current != "") tokens.Add(current);
            return tokens;
        }

        private (int val, SymbolType type, string? err) ParseAddSub(List<string> tokens, ref int pos, int pc)
        {
            var (leftVal, leftType, err) = ParseMulDiv(tokens, ref pos, pc);
            if (err != null) return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseMulDiv(tokens, ref pos, pc);
                if (rightErr != null) return (-1, SymbolType.Absolute, rightErr);

                // Reglas de apareamiento de expresiones absolutas y relativas
                if (op == "+")
                {
                    if (leftType == SymbolType.Relative && rightType == SymbolType.Relative)
                        return (-1, SymbolType.Absolute, "No se permite sumar dos términos relativos");
                    if (leftType == SymbolType.Absolute && rightType == SymbolType.Absolute)
                    {
                        leftVal += rightVal;
                        leftType = SymbolType.Absolute;
                    }
                    else // Uno absoluto y uno relativo
                    {
                        leftVal += rightVal;
                        leftType = SymbolType.Relative;
                    }
                }
                else if (op == "-")
                {
                    if (leftType == SymbolType.Absolute && rightType == SymbolType.Relative)
                        return (-1, SymbolType.Absolute, "No se permite restar un término relativo de uno absoluto");
                    if (leftType == SymbolType.Relative && rightType == SymbolType.Relative)
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Absolute; // Se cancelan las componentes relativas
                    }
                    else if (leftType == SymbolType.Absolute && rightType == SymbolType.Absolute)
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Absolute;
                    }
                    else // Relativo - Absoluto
                    {
                        leftVal -= rightVal;
                        leftType = SymbolType.Relative;
                    }
                }
            }
            return (leftVal, leftType, null);
        }

        private (int val, SymbolType type, string? err) ParseMulDiv(List<string> tokens, ref int pos, int pc)
        {
            var (leftVal, leftType, err) = ParseFactor(tokens, ref pos, pc);
            if (err != null) return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseFactor(tokens, ref pos, pc);
                if (rightErr != null) return (-1, SymbolType.Absolute, rightErr);

                // En multiplicación/división los operandos DEBEN ser absolutos
                if (leftType == SymbolType.Relative || rightType == SymbolType.Relative)
                {
                    return (-1, SymbolType.Absolute, "Los términos en multiplicaciones y divisiones deben ser absolutos");
                }

                if (op == "*")
                {
                    leftVal *= rightVal;
                }
                else
                {
                    if (rightVal == 0) return (-1, SymbolType.Absolute, "División por cero");
                    leftVal /= rightVal;
                }
            }
            return (leftVal, leftType, null);
        }

        private (int val, SymbolType type, string? err) ParseFactor(List<string> tokens, ref int pos, int pc)
        {
            if (pos >= tokens.Count) return (-1, SymbolType.Absolute, "Expresión mal formada");

            string token = tokens[pos++];

            if (token == "(")
            {
                var (val, type, err) = ParseAddSub(tokens, ref pos, pc);
                if (err != null) return (val, type, err);
                if (pos >= tokens.Count || tokens[pos++] != ")") return (-1, SymbolType.Absolute, "Se esperaba ')'");
                return (val, type, null);
            }

            if (token == "*")
                return (pc, SymbolType.Relative, null);

            if (_tabsim.TryGetValue(token, out var symInfo))
                return (symInfo.Value, symInfo.Type, null);

            // Es un número
            int value;
            if (token.EndsWith("H", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(token.Substring(0, token.Length - 1), System.Globalization.NumberStyles.HexNumber, null, out value))
                    return (value, SymbolType.Absolute, null);
            }
            else if (token.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                if (int.TryParse(token.Substring(2), System.Globalization.NumberStyles.HexNumber, null, out value))
                    return (value, SymbolType.Absolute, null);
            }
            else if (token.StartsWith("X'", StringComparison.OrdinalIgnoreCase) && token.EndsWith("'"))
            {
                if (int.TryParse(token.Substring(2, token.Length - 3), System.Globalization.NumberStyles.HexNumber, null, out value))
                    return (value, SymbolType.Absolute, null);
            }
            else if (int.TryParse(token, out value))
            {
                return (value, SymbolType.Absolute, null);
            }

            return (-1, SymbolType.Absolute, $"Símbolo no definido o valor inválido: {token}");
        }
    }
}
