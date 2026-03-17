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
        // allowUndefinedSymbols: si true, permite símbolos no definidos (útil para Paso 1)
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false)
        {
            if (string.IsNullOrWhiteSpace(expression)) return (0, SymbolType.Absolute, "Expresión vacía");

            // TODO: Un parser simple de la expresión para calcular su valor evaluando *, símbolos, y constantes
            return ParseExpressionTokens(expression, currentAddress, allowUndefinedSymbols);
        }

        private (int value, SymbolType type, string? error) ParseExpressionTokens(string expr, int currentAddress, bool allowUndefinedSymbols = false)
        {
            // Implementa un parser recursivo descendente simple o un evaluador postfix aquí.
            // Para simplificar, tokenize and evaluate.
            try
            {
                var tokens = Tokenize(expr);
                int pos = 0;
                var (val, type, err) = ParseAddSub(tokens, ref pos, currentAddress, allowUndefinedSymbols);
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

        private (int val, SymbolType type, string? err) ParseAddSub(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            var (leftVal, leftType, err) = ParseMulDiv(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null) return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseMulDiv(tokens, ref pos, pc, allowUndefinedSymbols);
                if (rightErr != null) return (-1, SymbolType.Absolute, rightErr);

                // Reglas de apareamiento de expresiones absolutas y relativas según SIC/XE
                // Solo se permiten ciertas combinaciones válidas
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
                    // Reglas válidas de resta en SIC/XE:
                    // Absolute - Absolute = Absolute
                    // Relative - Relative = Absolute (se cancelan las componentes)
                    // Relative - Absolute = Relative (restar offset a dirección)
                    // Absolute - (-Relative) = Relative (restar un relativo negado = sumar, ej: A - (-B) = A + B)
                    // INVÁLIDO: Absolute - Relative (si el relativo es positivo)

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
                    else if (leftType == SymbolType.Relative && rightType == SymbolType.Absolute)
                    {
                        // Relative - Absolute = Relative (restar offset a dirección, ej: ETIQ - 10)
                        leftVal -= rightVal;
                        leftType = SymbolType.Relative;
                    }
                    else if (leftType == SymbolType.Absolute && rightType == SymbolType.Relative)
                    {
                        // Absolute - Relative es válido solo si el relativo es negado
                        // Razón: A - (-B) = A + B (restar un negativo = sumar su opuesto)
                        // Si rightVal < 0, entonces fue negado por operador unario, es válido
                        // Si rightVal >= 0, es un error porque sería Absoluto - Relativo positivo
                        if (rightVal < 0)
                        {
                            // Es Absolute - (-Relative), que es equivalente a Absolute + Relative
                            leftVal -= rightVal;  // Esto realiza la resta correctamente: A - (-B) = A + B
                            leftType = SymbolType.Relative;
                        }
                        else
                        {
                            // INVÁLIDO: Absolute - Relative positivo no está permitido en SIC/XE
                            return (-1, SymbolType.Absolute, "No se permite restar un término relativo de uno absoluto (Absoluto - Relativo)");
                        }
                    }
                }
            }
            return (leftVal, leftType, null);
        }

        private (int val, SymbolType type, string? err) ParseMulDiv(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            var (leftVal, leftType, err) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
            if (err != null) return (leftVal, leftType, err);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                var (rightVal, rightType, rightErr) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
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

        private (int val, SymbolType type, string? err) ParseFactor(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols = false)
        {
            if (pos >= tokens.Count) return (-1, SymbolType.Absolute, "Expresión mal formada");

            string token = tokens[pos];

            // Manejar operadores unarios (+ y -)
            if (token == "+" || token == "-")
            {
                string unaryOp = token;
                pos++;  // Consumir el operador

                // Recursivamente parsear el siguiente factor
                var (val, type, err) = ParseFactor(tokens, ref pos, pc, allowUndefinedSymbols);
                if (err != null) return (val, type, err);

                // Aplicar el operador unario
                if (unaryOp == "-")
                {
                    val = -val;  // Negar el valor
                }
                // El + unario no hace nada, solo retorna el valor

                return (val, type, null);
            }

            pos++;  // Consumir el token (ya no es operador unario)

            if (token == "(")
            {
                var (val, type, err) = ParseAddSub(tokens, ref pos, pc, allowUndefinedSymbols);
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

            // Si allowUndefinedSymbols es true, retorna un valor temporal (0) para símbolos no definidos
            // Esto permite forward references en Paso 1
            if (allowUndefinedSymbols)
            {
                // Retorna 0 como valor placeholder, pero marca como Relative para indicar que puede cambiar
                return (0, SymbolType.Relative, null);
            }

            return (-1, SymbolType.Absolute, $"Símbolo no definido o valor inválido: {token}");
        }
    }
}
