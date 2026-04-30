using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    public class ProgramaObjeto
    {
        private readonly IReadOnlyList<ObjectCodeLine> _lineasCodigoObjeto;
        private readonly IReadOnlyList<ObjectModule>? _modulosPreconstruidos;
        private readonly string _nombrePrograma;
        private readonly int _direccionInicio;
        private readonly int _direccionEjecucion;
        private readonly int _longitudPrograma;

        public ProgramaObjeto(
            IReadOnlyList<ObjectCodeLine> lineas,
            string nombrePrograma,
            int dirInicio,
            int longPrograma,
            int dirEjecucion)
        {
            _lineasCodigoObjeto = lineas;
            _nombrePrograma = nombrePrograma;
            _direccionInicio = dirInicio;
            _longitudPrograma = longPrograma;
            _direccionEjecucion = dirEjecucion;
            _modulosPreconstruidos = null;
        }

        public ProgramaObjeto(
            IReadOnlyList<ObjectCodeLine> lineas,
            IReadOnlyList<ObjectModule> modulos,
            string nombrePrograma,
            int dirInicio,
            int longPrograma,
            int dirEjecucion)
        {
            _lineasCodigoObjeto = lineas;
            _modulosPreconstruidos = modulos;
            _nombrePrograma = nombrePrograma;
            _direccionInicio = dirInicio;
            _longitudPrograma = longPrograma;
            _direccionEjecucion = dirEjecucion;
        }

        public List<string> GenerarRegistros()
        {
            var modulos = GenerarModulos();
            var registros = new List<string>();

            foreach (var modulo in modulos)
            {
                bool isPrimarySection = string.Equals(modulo.Name, _nombrePrograma, StringComparison.OrdinalIgnoreCase) ||
                                       string.Equals(modulo.Name, "Por Omision", StringComparison.OrdinalIgnoreCase);
                registros.AddRange(modulo.ToRecords(isPrimarySection));
            }

            return registros;
        }

        public List<ObjectModule> GenerarModulos()
        {
            if (_modulosPreconstruidos != null && _modulosPreconstruidos.Count > 0)
                return _modulosPreconstruidos.ToList();

            var modulos = new List<ObjectModule>();
            foreach (var grupo in AgruparLineasPorSeccion())
                modulos.Add(ConstruirModulo(grupo));

            return modulos;
        }

        private List<IGrouping<(string SectionName, int SectionNumber), ObjectCodeLine>> AgruparLineasPorSeccion()
        {
            return _lineasCodigoObjeto
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
            modulo.E = new EndRecord(_direccionEjecucion);
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

            foreach (var linea in lineasSeccion)
            {
                string op = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? string.Empty;
                if (op == "EXTDEF")
                {
                    var symbolsInLine = ParseSymbolList(linea.IntermLine.Operand)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToList();
                    if (symbolsInLine.Count > 0)
                    {
                        foreach (var symbol in symbolsInLine)
                        {
                            if (!emittedExtDefSymbols.Add(symbol))
                                continue;

                            var defLine = lineasSeccion.FirstOrDefault(l =>
                                string.Equals(l.IntermLine.Label, symbol, StringComparison.OrdinalIgnoreCase));
                            int relAddr = defLine == null ? 0 : Math.Max(0, ObtenerDireccionEfectiva(defLine.IntermLine) - sectionStart);
                            modulo.D.Add(new DefineRecord(symbol, relAddr));
                        }
                    }
                }
                else if (op == "EXTREF")
                {
                    var symbolsInLine = ParseSymbolList(linea.IntermLine.Operand)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .Where(s => emittedExtRefSymbols.Add(s))
                        .ToList();

                    if (symbolsInLine.Count > 0)
                    {
                        foreach (var symbol in symbolsInLine)
                            modulo.R.Add(new ReferRecord(symbol));
                    }
                }
            }
        }

        private void AgregarRegistrosTextoYModificacion(ObjectModule modulo, IReadOnlyList<ObjectCodeLine> lineasSeccion)
        {
            int inicioTexto = -1;
            string codigoTexto = string.Empty;
            int conteoBytesTexto = 0;
            var registrosModificacion = new List<RegistroModificacion>();

            void VaciarTexto()
            {
                if (conteoBytesTexto <= 0)
                    return;
                modulo.T.Add(new TextRecord
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

                string codigoLimpio = linea.ObjectCode.Replace("*", "");
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

                int direccionMod = string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase)
                    ? lineAddress
                    : lineAddress + 1;
                int longitudHalfBytes = string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase) ? 0x06 : 0x05;

                var modificaciones = RecolectarModificaciones(linea);
                if (modificaciones.Count == 0)
                    continue;

                foreach (var mod in modificaciones)
                {
                    registrosModificacion.Add(new RegistroModificacion(direccionMod, longitudHalfBytes, mod.Sign, mod.Symbol));
                }
            }

            VaciarTexto();

            foreach (var registro in registrosModificacion)
            {
                modulo.M.Add(new ModificationRecord(
                    registro.Direccion,
                    registro.LongitudMediosBytes,
                    registro.Sign,
                    registro.Symbol));
            }
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

        public void ExportarACSV(string filePath)
        {
            // Exporta los registros objeto a CSV (1 registro por línea)
            // para facilitar inspección y apertura en Excel.
            var registros = GenerarRegistros();
            // Aseguramos que la carpeta exista
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            using (var writer = new StreamWriter(filePath))
            {
                // Un archivo CSV con los registros, cada registro en su propia línea o celda
                foreach (var r in registros)
                {
                    writer.WriteLine($"\"{r}\""); // Agregamos comillas para asegurar el parseo CSV limpio en Excel
                }
            }
        }

        public string ObtenerReporteConsola()
        {
            // Construye un reporte textual simple de los registros objeto generados.
            var sb = new StringBuilder();
            sb.AppendLine("---------------------------------------------------------------");
            sb.AppendLine("                    PROGRAMA OBJETO");
            sb.AppendLine("---------------------------------------------------------------");

            foreach (var r in GenerarRegistros())
            {
                sb.AppendLine(r);
            }

            return sb.ToString();
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
