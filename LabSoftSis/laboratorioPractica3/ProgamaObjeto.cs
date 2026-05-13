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

        public List<string> ExportarACSVPorSeccion(string outputDirectory)
        {
            Directory.CreateDirectory(outputDirectory);

            var modulos = GenerarModulos();
            var rutas = new List<string>();
            var usados = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var modulo in modulos)
            {
                bool isPrimarySection = string.Equals(modulo.Name, _nombrePrograma, StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(modulo.Name, "Por Omision", StringComparison.OrdinalIgnoreCase);

                string baseName = SanitizeFileName(string.IsNullOrWhiteSpace(modulo.Name) ? "SECCION" : modulo.Name);
                string fileName = baseName;
                int suffix = 2;

                while (!usados.Add(fileName))
                {
                    fileName = $"{baseName}_{suffix}";
                    suffix++;
                }

                string filePath = Path.Combine(outputDirectory, $"{fileName}.csv");

                using var writer = new StreamWriter(filePath, false, Encoding.UTF8);
                foreach (var registro in modulo.ToRecords(isPrimarySection))
                {
                    writer.WriteLine($"\"{registro}\"");
                }

                rutas.Add(filePath);
            }

            return rutas;
        }

        public List<ObjectModule> GenerarModulos()
        {
            if (_modulosPreconstruidos != null && _modulosPreconstruidos.Count > 0)
                return _modulosPreconstruidos.ToList();

            // Usar ObjectModuleBuilder centralizado en lugar de lógica duplicada
            var builder = new ObjectModuleBuilder(_lineasCodigoObjeto, null!, _nombrePrograma, _direccionEjecucion);
            return builder.BuildModules();
        }















        public void ExportarACSV(string filePath)
        {
            var directory = Path.GetDirectoryName(filePath);
            if (string.IsNullOrWhiteSpace(directory))
                directory = Directory.GetCurrentDirectory();

            ExportarACSVPorSeccion(directory);
        }

        private static string SanitizeFileName(string value)
        {
            foreach (char invalid in Path.GetInvalidFileNameChars())
                value = value.Replace(invalid, '_');

            return value.Trim();
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
    }
}
