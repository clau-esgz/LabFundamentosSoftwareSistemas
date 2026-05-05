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

            // Usar ObjectModuleBuilder centralizado en lugar de lógica duplicada
            var builder = new ObjectModuleBuilder(_lineasCodigoObjeto, null!, _nombrePrograma, _direccionEjecucion);
            return builder.BuildModules();
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
    }
}
