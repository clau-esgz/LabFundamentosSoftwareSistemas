using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    public class ProgramaObjeto
    {
        private readonly IReadOnlyList<ObjectCodeLine> _lineasCodigoObjeto;
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
        }

        public List<string> GenerarRegistros()
        {
            var registros = new List<string>();
            var lineasPorSeccion = _lineasCodigoObjeto
                .GroupBy(l => (l.SectionName ?? "Por Omision", l.SectionNumber))
                .OrderBy(g => g.Key.Item2)
                .ThenBy(g => g.Min(x => x.IntermLine.LineNumber))
                .ToList();

            foreach (var grupo in lineasPorSeccion)
            {
                string sectionName = string.IsNullOrWhiteSpace(grupo.Key.Item1) ? "Por Omision" : grupo.Key.Item1;
                var lineasSeccion = grupo.OrderBy(l => l.IntermLine.LineNumber).ToList();

                int sectionStart = lineasSeccion
                    .Where(l => l.IntermLine.Address >= 0)
                    .Select(l => l.IntermLine.Address)
                    .DefaultIfEmpty(_direccionInicio)
                    .Min();

                int sectionEnd = lineasSeccion
                    .Where(l => l.IntermLine.Address >= 0)
                    .Select(l => l.IntermLine.Address + Math.Max(0, l.IntermLine.Increment))
                    .DefaultIfEmpty(sectionStart)
                    .Max();

                int sectionLength = Math.Max(0, sectionEnd - sectionStart);
                string nombreH = sectionName.PadRight(6).Substring(0, 6);
                registros.Add($"H{nombreH}{sectionStart:X6}{sectionLength:X6}");

                var extDefSymbols = new List<string>();
                var extRefSymbols = new List<string>();
                foreach (var linea in lineasSeccion)
                {
                    string op = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? string.Empty;
                    if (op == "EXTDEF")
                    {
                        extDefSymbols.AddRange(ParseSymbolList(linea.IntermLine.Operand));
                    }
                    else if (op == "EXTREF")
                    {
                        extRefSymbols.AddRange(ParseSymbolList(linea.IntermLine.Operand));
                    }
                }

                extDefSymbols = extDefSymbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                extRefSymbols = extRefSymbols.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                if (extDefSymbols.Count > 0)
                {
                    var dBuilder = new StringBuilder("D");
                    foreach (var symbol in extDefSymbols)
                    {
                        var defLine = lineasSeccion.FirstOrDefault(l =>
                            string.Equals(l.IntermLine.Label, symbol, StringComparison.OrdinalIgnoreCase));
                        int relAddr = defLine == null ? 0 : Math.Max(0, defLine.IntermLine.Address - sectionStart);
                        dBuilder.Append(symbol.PadRight(6).Substring(0, 6));
                        dBuilder.Append(relAddr.ToString("X6"));
                    }
                    registros.Add(dBuilder.ToString());
                }

                if (extRefSymbols.Count > 0)
                {
                    var rBuilder = new StringBuilder("R");
                    foreach (var symbol in extRefSymbols)
                    {
                        rBuilder.Append(symbol.PadRight(6).Substring(0, 6));
                    }
                    registros.Add(rBuilder.ToString());
                }

                int inicioTexto = -1;
                string codigoTexto = string.Empty;
                int conteoBytesTexto = 0;

                void VaciarTexto()
                {
                    if (conteoBytesTexto <= 0)
                        return;
                    registros.Add($"T{inicioTexto:X6}{conteoBytesTexto:X2}{codigoTexto}");
                    inicioTexto = -1;
                    codigoTexto = string.Empty;
                    conteoBytesTexto = 0;
                }

                var registrosModificacion = new List<RegistroModificacion>();

                foreach (var linea in lineasSeccion)
                {
                    if (string.IsNullOrWhiteSpace(linea.ObjectCode))
                    {
                        string operacion = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? string.Empty;
                        if (operacion is "RESB" or "RESW" or "ORG" or "END" or "USE" or "CSECT")
                            VaciarTexto();
                        continue;
                    }

                    string codigoCrudo = linea.ObjectCode;
                    string codigoLimpio = codigoCrudo.Replace("*", "");
                    int bytesLinea = codigoLimpio.Length / 2;

                    bool esContiguo = (inicioTexto != -1) && ((inicioTexto + conteoBytesTexto) == linea.IntermLine.Address);
                    bool excedeLimite = (conteoBytesTexto + bytesLinea) > 30;
                    if (inicioTexto != -1 && (!esContiguo || excedeLimite))
                        VaciarTexto();

                    if (inicioTexto == -1)
                        inicioTexto = linea.IntermLine.Address;

                    codigoTexto += codigoLimpio;
                    conteoBytesTexto += bytesLinea;

                    bool requiereM = linea.RequiresModification || codigoCrudo.EndsWith("*", StringComparison.Ordinal);
                    if (!requiereM)
                        continue;

                    int direccionMod = string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase)
                        ? linea.IntermLine.Address
                        : linea.IntermLine.Address + 1;
                    int longitudHalfBytes = string.Equals(linea.IntermLine.Operation, "WORD", StringComparison.OrdinalIgnoreCase) ? 0x06 : 0x05;

                    // Construir marcadores de relocalización
                    var marcadores = new StringBuilder();

                    if (linea.ExternalReferenceSymbols.Count > 0)
                    {
                        // Hay símbolos externos: agregar *SE por cada uno
                        foreach (var ext in linea.ExternalReferenceSymbols.Distinct(StringComparer.OrdinalIgnoreCase))
                        {
                            marcadores.Append("*SE");
                        }
                        registrosModificacion.Add(new RegistroModificacion(direccionMod, longitudHalfBytes, marcadores.ToString()));
                    }
                    else if (linea.RequiresModification)
                    {
                        // Es relativo puro (no es R-R=A porque RequiresModification sería false si lo fuera)
                        marcadores.Append("*R");
                        registrosModificacion.Add(new RegistroModificacion(direccionMod, longitudHalfBytes, marcadores.ToString()));
                    }
                    else
                    {
                        // No requiere relocalización (puede ser absoluto o R-R=A)
                        // No agregar registro M en este caso
                    }
                }

                VaciarTexto();

                foreach (var registro in registrosModificacion)
                {
                    registros.Add($"M{registro.Direccion:X6}{registro.LongitudMediosBytes:X2}+{registro.Nombre}");
                }

                // Emitir registro E para esta sección
                // Sólo la primera sección (PRINCIPAL) usa la dirección de ejecución
                // Las demás secciones emiten E con 6 espacios en blanco
                bool isPrimarySection = (grupo.Key.Item2 == 0) || string.Equals(sectionName, "Por Omision", StringComparison.OrdinalIgnoreCase);
                string eRecord = isPrimarySection
                    ? $"E{_direccionEjecucion:X6}"
                    : "E      "; // 6 espacios: secciones secundarias sin punto de entrada
                registros.Add(eRecord);
            }

            return registros;
        }

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
            public RegistroModificacion(int direccion, int longitudMediosBytes, string nombre)
            {
                Direccion = direccion;
                LongitudMediosBytes = longitudMediosBytes;
                // Nombre ahora contiene los marcadores (*R, *SE*SE, etc.)
                Nombre = nombre;
            }

            public int Direccion { get; }
            public int LongitudMediosBytes { get; }
            public string Nombre { get; }
        }
    }
}
