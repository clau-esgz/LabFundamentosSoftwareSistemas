using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace laboratorioPractica3
{
    public class ExpressionEvaluationMetadata
    {
        public List<string> ExternalSymbols { get; } = new();
        public List<ModificationRequest> ModificationRequests { get; } = new();
        public bool HasUnpairedRelative { get; set; }
        public char? RelativeModuleSign { get; set; }
    }

    public sealed class ModificationRequest
    {
        public string Symbol { get; init; } = string.Empty;
        public char Sign { get; init; }
    }

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
        public string ControlSectionName { get; set; }
        public string BlockName { get; set; }
        public int BlockNumber { get; set; }
        public int RelativeValue { get; set; }
        public bool IsExternal { get; set; }

        public SymbolInfo(string name, int value, SymbolType type, string blockName = "Por Omision", int blockNumber = 0, int? relativeValue = null, bool isExternal = false, string controlSectionName = "Por Omision")
        {
            Name = name;
            Value = value;
            Type = type;
            ControlSectionName = string.IsNullOrWhiteSpace(controlSectionName) ? "Por Omision" : controlSectionName.Trim();
            BlockName = blockName;
            BlockNumber = blockNumber;
            RelativeValue = relativeValue ?? value;
            IsExternal = isExternal;
        }

        public override string ToString()
        {
            return $"Nombre: {Name}, Valor: {Value:X4}h, Tipo: {Type}, Bloque: {BlockName}";
        }
    }

    // Gestor de símbolos y evaluador de expresiones del ensamblador SIC/XE
    // Permite: operaciones aritméticas (+, -, *, /), paréntesis, símbolos, constantes y * (contador actual)
    // Valida reglas de apareamiento Absoluto-Relativo según especificación SIC/XE
    public class SimbolosYExpresiones
    {
        // Tabla de símbolos por sección de control (case-insensitive)
        private readonly Dictionary<string, Dictionary<string, SymbolInfo>> _symbolTableBySection = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Por Omision"] = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase)
        };

        private static string NormalizeSectionName(string? sectionName)
            => string.IsNullOrWhiteSpace(sectionName) ? "Por Omision" : sectionName.Trim();

        private Dictionary<string, SymbolInfo> EnsureSection(string? sectionName)
        {
            string normalized = NormalizeSectionName(sectionName);
            if (!_symbolTableBySection.TryGetValue(normalized, out var table))
            {
                table = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);
                _symbolTableBySection[normalized] = table;
            }
            return table;
        }

        private bool TryResolveSymbol(string name, string? controlSectionName, out SymbolInfo info)
        {
            info = null!;

            string section = NormalizeSectionName(controlSectionName);
            if (_symbolTableBySection.TryGetValue(section, out var sectionTable) && sectionTable.TryGetValue(name, out var foundInSection))
            {
                info = foundInSection;
                return true;
            }

            // Fallback: símbolo único en cualquier sección
            SymbolInfo? unique = null;
            int matches = 0;
            foreach (var table in _symbolTableBySection.Values)
            {
                if (table.TryGetValue(name, out var candidate))
                {
                    unique = candidate;
                    matches++;
                    if (matches > 1)
                        return false; // ambiguo entre secciones
                }
            }

            if (matches == 1 && unique != null)
            {
                info = unique;
                return true;
            }

            return false;
        }

        public IReadOnlyDictionary<string, SymbolInfo> GetAllSymbols()
        {
            var merged = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);
            var keyOccurrences = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            foreach (var sec in _symbolTableBySection.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var kv in _symbolTableBySection[sec])
                {
                    if (!keyOccurrences.ContainsKey(kv.Key))
                        keyOccurrences[kv.Key] = 0;
                    keyOccurrences[kv.Key]++;
                }
            }

            foreach (var sec in _symbolTableBySection.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase))
            {
                foreach (var kv in _symbolTableBySection[sec])
                {
                    string outputKey = keyOccurrences[kv.Key] > 1 ? $"{sec}::{kv.Key}" : kv.Key;
                    merged[outputKey] = kv.Value;
                }
            }

            return merged;
        }

        public IReadOnlyDictionary<string, SymbolInfo> GetSymbolsBySection(string? controlSectionName)
        {
            string section = NormalizeSectionName(controlSectionName);
            if (_symbolTableBySection.TryGetValue(section, out var table))
                return table;

            return new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);
        }

        public bool ContainsKey(string name, string? controlSectionName = null)
        {
            if (!string.IsNullOrWhiteSpace(controlSectionName))
            {
                string section = NormalizeSectionName(controlSectionName);
                return _symbolTableBySection.TryGetValue(section, out var table) && table.ContainsKey(name);
            }

            foreach (var table in _symbolTableBySection.Values)
            {
                if (table.ContainsKey(name))
                    return true;
            }

            return false;
        }

        public void AddSymbol(string name, int value, SymbolType type, string blockName = "Por Omision", int blockNumber = 0, bool isExternal = false, string? controlSectionName = null)
        {
            string section = NormalizeSectionName(controlSectionName);
            var sectionTable = EnsureSection(section);
            sectionTable[name] = new SymbolInfo(name, value, type, blockName, blockNumber, value, isExternal, section);
        }

        public void RelocateRelativeSymbols(Func<string, string, int> blockStartResolver)
        {
            foreach (var sectionTable in _symbolTableBySection.Values)
            {
                foreach (var symbol in sectionTable.Values)
                {
                    if (symbol.Type == SymbolType.Relative)
                    {
                        int blockStart = blockStartResolver(symbol.ControlSectionName, symbol.BlockName);
                        symbol.Value = blockStart + symbol.RelativeValue;
                    }
                }
            }
        }

        public bool TryGetValue(string name, out SymbolInfo info, string? controlSectionName = null)
            => TryResolveSymbol(name, controlSectionName, out info!);

        public bool IsExternalSymbol(string symbolName, string? controlSectionName = null)
        {
            return TryResolveSymbol(symbolName, controlSectionName, out var info) && info.IsExternal;
        }

        public bool ContainsExternalSymbol(string? expression, string? controlSectionName = null)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return false;

            foreach (var token in Tokenize(expression))
            {
                if (TryResolveSymbol(token, controlSectionName, out var info) && info.IsExternal)
                    return true;
            }

            return false;
        }

        public (int value, SymbolType type, string? error, ExpressionEvaluationMetadata metadata)
            EvaluateExpressionForObject(string? expression, int currentAddress, bool allowUndefinedSymbols = false, string? controlSectionName = null)
        {
            var metadata = new ExpressionEvaluationMetadata();

            if (string.IsNullOrWhiteSpace(expression))
                return (0, SymbolType.Absolute, "Expresión vacía", metadata);

            CollectModificationRequests(expression, metadata, controlSectionName);
            string normalizedExpression = NormalizeExternalsToZero(expression, metadata, controlSectionName);
            var (value, relCount, error) = EvaluateExpressionWithRelativeCount(normalizedExpression, currentAddress, allowUndefinedSymbols, controlSectionName);

            if (error == null && Math.Abs(relCount) == 1)
            {
                metadata.HasUnpairedRelative = true;
                metadata.RelativeModuleSign = relCount > 0 ? '+' : '-';
            }
            else
            {
                metadata.HasUnpairedRelative = false;
                metadata.RelativeModuleSign = null;
            }

            var type = Math.Abs(relCount) == 1 && error == null
                ? SymbolType.Relative
                : SymbolType.Absolute;

            return (value, type, error, metadata);
        }

        private int CollectModificationRequests(string expression, ExpressionEvaluationMetadata metadata, string? controlSectionName)
        {
            var tokens = Tokenize(expression);
            int pos = 0;
            return CollectAddSubModificationRequests(tokens, ref pos, metadata, controlSectionName, +1);
        }

        private int CollectAddSubModificationRequests(List<string> tokens, ref int pos, ExpressionEvaluationMetadata metadata, string? controlSectionName, int sign)
        {
            int relCount = CollectMulDivModificationRequests(tokens, ref pos, metadata, controlSectionName, sign);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                int nextSign = op == "+" ? sign : -sign;
                relCount += CollectMulDivModificationRequests(tokens, ref pos, metadata, controlSectionName, nextSign);
            }

            return relCount;
        }

        private int CollectMulDivModificationRequests(List<string> tokens, ref int pos, ExpressionEvaluationMetadata metadata, string? controlSectionName, int sign)
        {
            int relCount = CollectFactorModificationRequests(tokens, ref pos, metadata, controlSectionName, sign);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                int rightRel = CollectFactorModificationRequests(tokens, ref pos, metadata, controlSectionName, sign);

                // Las multiplicaciones y divisiones no deben generar registros M propios.
                // Si incluyen símbolos relativos/externos, la expresión será validada por el evaluador.
                if (op == "*" || op == "/")
                {
                    relCount = 0;
                }

                // Consumir el término derecho para no romper el recorrido.
                _ = rightRel;
            }

            return relCount;
        }

        private int CollectFactorModificationRequests(List<string> tokens, ref int pos, ExpressionEvaluationMetadata metadata, string? controlSectionName, int sign)
        {
            if (pos >= tokens.Count)
                return 0;

            string token = tokens[pos];

            if (token == "+" || token == "-")
            {
                pos++;
                int nextSign = token == "+" ? sign : -sign;
                return CollectFactorModificationRequests(tokens, ref pos, metadata, controlSectionName, nextSign);
            }

            if (token == "(")
            {
                pos++;
                int rel = CollectAddSubModificationRequests(tokens, ref pos, metadata, controlSectionName, sign);
                if (pos < tokens.Count && tokens[pos] == ")")
                    pos++;
                return rel;
            }

            if (token == ")")
            {
                pos++;
                return 0;
            }

            pos++;

            if (token == "*")
                return sign;

            if (TryResolveSymbol(token, controlSectionName, out var symbolInfo))
            {
                if (symbolInfo.IsExternal)
                {
                    metadata.ModificationRequests.Add(new ModificationRequest
                    {
                        Symbol = symbolInfo.Name,
                        Sign = sign >= 0 ? '+' : '-'
                    });
                    if (!metadata.ExternalSymbols.Contains(symbolInfo.Name, StringComparer.OrdinalIgnoreCase))
                        metadata.ExternalSymbols.Add(symbolInfo.Name);
                    return 0;
                }

                return symbolInfo.Type == SymbolType.Relative ? sign : 0;
            }

            if (TryParseTokenAsNumber(token, out _))
                return 0;

            return 0;
        }

        private string NormalizeExternalsToZero(string expression, ExpressionEvaluationMetadata metadata, string? controlSectionName)
        {
            var tokens = Tokenize(expression);

            for (int i = 0; i < tokens.Count; i++)
            {
                string token = tokens[i];
                if (TryResolveSymbol(token, controlSectionName, out var symbolInfo) && symbolInfo.IsExternal)
                {
                    if (!metadata.ExternalSymbols.Contains(token, StringComparer.OrdinalIgnoreCase))
                        metadata.ExternalSymbols.Add(token);
                    tokens[i] = "0";
                }
            }

            return string.Concat(tokens);
        }

        public int Count => _symbolTableBySection.Values.Sum(t => t.Count);

        public void Clear()
        {
            _symbolTableBySection.Clear();
            _symbolTableBySection["Por Omision"] = new Dictionary<string, SymbolInfo>(StringComparer.OrdinalIgnoreCase);
        }

        // Evalúa expresiones aritméticas: +, -, *, /, ()
        // Parámetros:
        //   expression: la expresión a evaluar
        //   currentAddress: valor de * (contador de ubicación actual)
        //   allowUndefinedSymbols: si true, permite símbolos no definidos (útil para Paso 1)
        // Retorna: (valor, tipo de resultado, mensaje de error si hay)
        public (int value, SymbolType type, string? error) EvaluateExpression(string? expression, int currentAddress, bool allowUndefinedSymbols = false, string? controlSectionName = null)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return (0, SymbolType.Absolute, "Expresión vacía");

            var (value, relCount, error) = EvaluateExpressionWithRelativeCount(expression, currentAddress, allowUndefinedSymbols, controlSectionName);
            if (error != null)
                return (-1, SymbolType.Absolute, error);

            return (value, Math.Abs(relCount) == 1 ? SymbolType.Relative : SymbolType.Absolute, null);
        }

        // Procesa los tokens de la expresión mediante parser recursivo descendente
        private (int value, int relCount, string? error) EvaluateExpressionWithRelativeCount(string expression, int currentAddress, bool allowUndefinedSymbols, string? controlSectionName)
        {
            try
            {
                var tokens = Tokenize(expression);
                int pos = 0;

                // El parser usa relCount: cuenta de términos relativos
                // relCount = 0 -> resultado es Absoluto
                // relCount = 1 -> resultado es Relativo
                // relCount != 0,1 -> error (mezcla inválida de relativos)
                var (value, relCount, error) = ParseAddSubInternal(tokens, ref pos, currentAddress, allowUndefinedSymbols, controlSectionName);
                if (error != null)
                    return (-1, 0, error);

                if (pos < tokens.Count)
                    return (-1, 0, "Tokens adicionales inesperados");

                // Determinar validez final según la magnitud del término relativo neto.
                // +1 y -1 son válidos: representan una expresión relativa que debe
                // relocalizarse con signo explícito.
                if (relCount == 0 || Math.Abs(relCount) == 1)
                    return (value, relCount, null);

                return (-1, 0, "La expresión contiene más de un término relativo sin cancelar");
            }
            catch (Exception ex)
            {
                return (-1, 0, $"Error al evaluar la expresión: {ex.Message}");
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
        private (int val, int relCount, string? err) ParseAddSubInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols, string? controlSectionName)
        {
            var (leftVal, leftRelCount, err) = ParseMulDivInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
            if (err != null)
                return (leftVal, leftRelCount, err);

            while (pos < tokens.Count && (tokens[pos] == "+" || tokens[pos] == "-"))
            {
                string op = tokens[pos++];
                var (rightVal, rightRelCount, rightErr) = ParseMulDivInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
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
        // Restricción SIC/XE: losoperandos DEBEN ser absolutos (relCount = 0)
        private (int val, int relCount, string? err) ParseMulDivInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols, string? controlSectionName)
        {
            var (leftVal, leftRelCount, err) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
            if (err != null)
                return (leftVal, leftRelCount, err);

            while (pos < tokens.Count && (tokens[pos] == "*" || tokens[pos] == "/"))
            {
                string op = tokens[pos++];
                var (rightVal, rightRelCount, rightErr) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
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
        private (int val, int relCount, string? err) ParseFactorInternal(List<string> tokens, ref int pos, int pc, bool allowUndefinedSymbols, string? controlSectionName)
        {
            if (pos >= tokens.Count)
                return (-1, 0, "Expresión mal formada");

            string token = tokens[pos];

            // Manejo de operadores unarios: + y -
            if (token == "+" || token == "-")
            {
                string unary = token;
                pos++;

                var (value, relCount, err) = ParseFactorInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
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
                var (exprValue, exprRelCount, exprErr) = ParseAddSubInternal(tokens, ref pos, pc, allowUndefinedSymbols, controlSectionName);
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
            if (TryResolveSymbol(token, controlSectionName, out var symbolInfo))
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

