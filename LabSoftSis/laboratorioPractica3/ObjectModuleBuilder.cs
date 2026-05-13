using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using laboratorioPractica3.Records;

namespace laboratorioPractica3
{
    /// <summary>
    /// Constructor unificado de módulos objeto SIC/XE.
    /// Centraliza la lógica de agrupamiento de líneas de código objeto, creación de registros T/R/D/M/E.
    /// Utilizado por tanto Paso2 como ProgramaObjeto para evitar duplicación.
    /// </summary>
    internal class ObjectModuleBuilder
    {
        private readonly IReadOnlyList<ObjectCodeLine> _objectCodeLines;
        private readonly SimbolosYExpresiones _simbolos;
        private readonly string _nombrePrograma;
        private readonly int _direccionEjecucion;

        public ObjectModuleBuilder(
            IReadOnlyList<ObjectCodeLine> objectCodeLines,
            SimbolosYExpresiones simbolos,
            string nombrePrograma,
            int direccionEjecucion)
        {
            _objectCodeLines = objectCodeLines;
            _simbolos = simbolos;
            _nombrePrograma = nombrePrograma;
            _direccionEjecucion = direccionEjecucion;
        }

        /// <summary>
        /// Construye los módulos objeto a partir de las líneas de código objeto.
        /// Agrupa por sección de control (CSECT) y genera registros D/R/T/M/E para cada una.
        /// </summary>
        public List<ObjectModule> BuildModules()
        {
            var modulos = new List<ObjectModule>();
            foreach (var grupo in AgruparLineasPorSeccion())
            {
                modulos.Add(ConstruirModulo(grupo));
            }
            return modulos;
        }

        private List<IGrouping<(string SectionName, int SectionNumber), ObjectCodeLine>> AgruparLineasPorSeccion()
        {
            return _objectCodeLines
                .GroupBy(l => (l.SectionName ?? "Por Omision", l.SectionNumber))
                .OrderBy(g => g.Key.SectionNumber)
                .ThenBy(g => g.Min(x => x.IntermLine.LineNumber))
                .ToList();
        }

        private ObjectModule ConstruirModulo(IGrouping<(string SectionName, int SectionNumber), ObjectCodeLine> grupo)
        {
            string sectionName = string.IsNullOrWhiteSpace(grupo.Key.SectionName) ? "Por Omision" : grupo.Key.SectionName;
            var lineasSeccion = grupo.OrderBy(l => l.IntermLine.LineNumber).ToList();

            ObtenerRangoSeccion(lineasSeccion, out int sectionStart, out int sectionEnd);

            var modulo = new ObjectModule
            {
                Name = sectionName,
                StartAddress = sectionStart,
                Length = Math.Max(0, sectionEnd - sectionStart)
            };

            AgregarRegistrosDefinicionYReferencia(modulo, lineasSeccion, sectionStart);
            AgregarRegistrosTextoYModificacion(modulo, lineasSeccion);

            // El registro E siempre pertenece al módulo; la serialización decide si imprime dirección.
            bool isPrimarySection = string.Equals(modulo.Name, _nombrePrograma, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(modulo.Name, "Por Omision", StringComparison.OrdinalIgnoreCase);
            int eAddress = isPrimarySection ? _direccionEjecucion : 0;
            modulo.E = new EndRecord { ExecutionAddress = eAddress };

            return modulo;
        }

        private static void ObtenerRangoSeccion(IReadOnlyList<ObjectCodeLine> lineasSeccion, out int sectionStart, out int sectionEnd)
        {
            static int Addr(IntermediateLine l) => l.AbsoluteAddress >= 0 ? l.AbsoluteAddress : l.Address;

            sectionStart = lineasSeccion
                .Where(l => Addr(l.IntermLine) >= 0)
                .Select(l => Addr(l.IntermLine))
                .DefaultIfEmpty(0)
                .Min();

            sectionEnd = lineasSeccion
                .Where(l => Addr(l.IntermLine) >= 0)
                .Select(l => Addr(l.IntermLine) + Math.Max(0, l.IntermLine.Increment))
                .DefaultIfEmpty(sectionStart)
                .Max();
        }

        private void AgregarRegistrosDefinicionYReferencia(ObjectModule modulo, IReadOnlyList<ObjectCodeLine> lineasSeccion, int sectionStart)
        {
            var emittedExtDefSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var emittedExtRefSymbols = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            var defineRecord = new Records.DefineRecord();
            var referRecord = new Records.ReferRecord();

            foreach (var linea in lineasSeccion)
            {
                string op = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? string.Empty;
                if (op == "EXTDEF")
                {
                    var symbolsInLine = ParseSymbolList(linea.IntermLine.Operand)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    
                    foreach (var symbol in symbolsInLine)
                    {
                        if (!emittedExtDefSymbols.Add(symbol))
                            continue;

                        var defLine = lineasSeccion.FirstOrDefault(l =>
                            string.Equals(l.IntermLine.Label, symbol, StringComparison.OrdinalIgnoreCase));
                        int relAddr = defLine == null ? 0 : Math.Max(0, ObtenerDireccionEfectiva(defLine.IntermLine) - sectionStart);
                        
                        defineRecord.Definitions.Add((symbol, relAddr));
                        
                        if (defineRecord.Definitions.Count >= 6) // Limitar a 6 por registro para no exceder longitud razonable
                        {
                            modulo.D.Add(defineRecord);
                            defineRecord = new Records.DefineRecord();
                        }
                    }
                }
                else if (op == "EXTREF")
                {
                    var symbolsInLine = ParseSymbolList(linea.IntermLine.Operand)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(s => emittedExtRefSymbols.Add(s))
                        .ToList();

                    foreach (var symbol in symbolsInLine)
                    {
                        referRecord.References.Add(symbol);
                        
                        if (referRecord.References.Count >= 12) // Limitar a 12 por registro
                        {
                            modulo.R.Add(referRecord);
                            referRecord = new Records.ReferRecord();
                        }
                    }
                }
            }

            if (defineRecord.Definitions.Count > 0)
                modulo.D.Add(defineRecord);
            
            if (referRecord.References.Count > 0)
                modulo.R.Add(referRecord);
        }

        private void AgregarRegistrosTextoYModificacion(ObjectModule modulo, IReadOnlyList<ObjectCodeLine> lineasSeccion)
        {
            int inicioTexto = -1;
            string codigoTexto = string.Empty;
            int conteoBytesTexto = 0;
            var registrosModificacion = new List<Records.ModificationRecord>();

            void VaciarTexto()
            {
                if (conteoBytesTexto <= 0)
                    return;
                modulo.T.Add(new Records.TextRecord
                {
                    StartAddress = inicioTexto,
                    HexBytes = codigoTexto
                });
                inicioTexto = -1;
                codigoTexto = string.Empty;
                conteoBytesTexto = 0;
            }

            foreach (var linea in lineasSeccion)
            {
                if (string.IsNullOrWhiteSpace(linea.ObjectCode))
                {
                    string operacion = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? string.Empty;
                    if (operacion is "RESB" or "RESW" or "ORG" or "END" or "USE" or "CSECT")
                        VaciarTexto();
                    continue;
                }

                string codigoLimpio = laboratorioPractica3.Utils.ObjectCodeUtils.StripDisplayMarks(linea.ObjectCode);
                
                int bytesLinea = codigoLimpio.Length / 2;
                int lineAddress = ObtenerDireccionEfectiva(linea.IntermLine);
                bool esContiguo = (inicioTexto != -1) && ((inicioTexto + conteoBytesTexto) == lineAddress);
                bool excedeLimite = (conteoBytesTexto + bytesLinea) > 30;
                if (inicioTexto != -1 && (!esContiguo || excedeLimite))
                    VaciarTexto();

                if (inicioTexto == -1)
                    inicioTexto = lineAddress;

                codigoTexto += codigoLimpio;
                conteoBytesTexto += bytesLinea;

                // BUG FIX #1: Dirección del registro M debe ser SIEMPRE el primer byte (byte más a la izquierda)
                // Especificación SIC/XE: "La dirección del registro M es la dirección del BYTE MÁS A LA IZQUIERDA del campo a modificar"
                // NO debe haber desplazamiento de +1 para instrucciones que no son WORD
                int direccionMod = lineAddress;

                // BUG FIX #2: Calcular half-bytes (longitud de modificación) según tipo de instrucción
                // - WORD: 24 bits = 6 half-bytes (0x06)
                // - Formato 4: 20 bits = 5 half-bytes (0x05)
                // - Formato 3: 12 bits = 3 half-bytes (0x03)
                // - BYTE: 2 half-bytes por byte (2, 4, 6 depending on length)
                int longitudHalfBytes;
                if (string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase))
                {
                    longitudHalfBytes = 0x06;  // 24-bit = 6 nibbles
                }
                else if (linea.IntermLine.Format == 4)
                {
                    longitudHalfBytes = 0x05;  // 20-bit = 5 nibbles
                }
                else if (linea.IntermLine.Format == 3)
                {
                    longitudHalfBytes = 0x03;  // 12-bit displacement = 3 nibbles
                }
                else if (string.Equals(linea.IntermLine.Operation, "BYTE", StringComparison.OrdinalIgnoreCase))
                {
                    // BYTE: 2 nibbles (half-bytes) por byte
                    longitudHalfBytes = bytesLinea * 2;
                }
                else
                {
                    // Default a formato 4 (20 bits)
                    longitudHalfBytes = 0x05;
                }

                var modificaciones = RecolectarModificaciones(linea);
                foreach (var mod in modificaciones)
                {
                    registrosModificacion.Add(new Records.ModificationRecord
                    {
                        Address = direccionMod,
                        HalfBytesLength = longitudHalfBytes,
                        Sign = mod.Sign,
                        Symbol = mod.Symbol
                    });
                }
            }

            VaciarTexto();
            modulo.M.AddRange(registrosModificacion);
        }

        private List<ModificationRequest> RecolectarModificaciones(ObjectCodeLine linea)
        {
            var modificaciones = new List<ModificationRequest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddMod(ModificationRequest mod)
            {
                string key = $"{mod.Sign}|{mod.Symbol}";
                if (seen.Add(key))
                    modificaciones.Add(mod);
            }

            foreach (var mod in linea.ModificationRequests)
                AddMod(mod);

            foreach (var mod in InferModificationRequests(linea))
                AddMod(mod);

            return modificaciones;
        }

        private static int ObtenerDireccionEfectiva(IntermediateLine linea)
            => linea.AbsoluteAddress >= 0 ? linea.AbsoluteAddress : linea.Address;

        private static List<string> ParseSymbolList(string? operand)
        {
            if (string.IsNullOrWhiteSpace(operand))
                return new List<string>();

            return operand
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
        }

        private List<ModificationRequest> InferModificationRequests(ObjectCodeLine linea)
        {
            var result = new List<ModificationRequest>();
            string operand = linea.IntermLine.Operand ?? string.Empty;

            if (!string.IsNullOrWhiteSpace(operand) && linea.ExternalReferenceSymbols.Count > 0)
            {
                var externalSet = new HashSet<string>(linea.ExternalReferenceSymbols, StringComparer.OrdinalIgnoreCase);
                int pos = 0;
                CollectExternalModificationRequests(TokenizeExpression(operand), ref pos, externalSet, result, +1);
            }

            bool isWordOrFormat4 =
                string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase) ||
                linea.IntermLine.Format == 4;
            if (isWordOrFormat4 && linea.RelativeModuleSign.HasValue)
            {
                result.Add(new ModificationRequest
                {
                    Symbol = string.IsNullOrWhiteSpace(linea.SectionName) ? _nombrePrograma : linea.SectionName,
                    Sign = linea.RelativeModuleSign.Value
                });
            }

            return result;
        }

        private static List<string> TokenizeExpression(string expression)
        {
            var tokens = new List<string>();
            var current = new StringBuilder();

            foreach (char ch in expression)
            {
                if (char.IsWhiteSpace(ch))
                    continue;

                if ("+-*/()".Contains(ch))
                {
                    if (current.Length > 0)
                    {
                        tokens.Add(current.ToString());
                        current.Clear();
                    }
                    tokens.Add(ch.ToString());
                }
                else
                {
                    current.Append(ch);
                }
            }

            if (current.Length > 0)
                tokens.Add(current.ToString());

            return tokens;
        }

        private void CollectExternalModificationRequests(List<string> tokens, ref int pos, HashSet<string> externalSet, List<ModificationRequest> result, int sign)
        {
            while (pos < tokens.Count)
            {
                string token = tokens[pos];

                if (token == ")")
                {
                    pos++;
                    return;
                }

                if (token == "(")
                {
                    pos++;
                    CollectExternalModificationRequests(tokens, ref pos, externalSet, result, sign);
                    continue;
                }

                if (token == "+" || token == "-")
                {
                    int nextSign = token == "+" ? sign : -sign;
                    pos++;
                    if (pos < tokens.Count && tokens[pos] == "(")
                    {
                        pos++;
                        CollectExternalModificationRequests(tokens, ref pos, externalSet, result, nextSign);
                    }
                    else if (pos < tokens.Count)
                    {
                        string next = tokens[pos++];
                        if (externalSet.Contains(next))
                        {
                            result.Add(new ModificationRequest { Symbol = next, Sign = nextSign >= 0 ? '+' : '-' });
                        }
                    }
                    continue;
                }

                pos++;
                if (externalSet.Contains(token))
                {
                    result.Add(new ModificationRequest { Symbol = token, Sign = sign >= 0 ? '+' : '-' });
                }
            }
        }

        private readonly struct RegistroModificacion
        {
            public RegistroModificacion(int direccion, int longitudMediosBytes, char sign, string symbol)
            {
                Direccion = direccion;
                LongitudMediosBytes = longitudMediosBytes;
                Sign = sign;
                Symbol = symbol;
            }

            public int Direccion { get; }
            public int LongitudMediosBytes { get; }
            public char Sign { get; }
            public string Symbol { get; }
        }
    }
}
