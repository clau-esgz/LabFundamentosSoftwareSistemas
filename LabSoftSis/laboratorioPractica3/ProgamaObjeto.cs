using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    public class ProgramaObjeto
    {
        private readonly IReadOnlyList<ObjectCodeLine> _lineas;
        private readonly string _nombrePrograma;
        private readonly int _dirInicio;
        private readonly int _longPrograma;

        public ProgramaObjeto(
            IReadOnlyList<ObjectCodeLine> lineas,
            string nombrePrograma,
            int dirInicio,
            int longPrograma)
        {
            _lineas = lineas;
            _nombrePrograma = nombrePrograma;
            _dirInicio = dirInicio;
            _longPrograma = longPrograma;
        }

        public List<string> GenerarRegistros()
        {
            var registros = new List<string>();
            var mRecords = new List<string>();

            // 1. Registro H (Encabezado)
            // Nombre justificado a la izquierda con espacios (6 caracteres)
            string nombre = (_nombrePrograma ?? "").PadRight(6).Substring(0, 6);
            registros.Add($"H{nombre}{_dirInicio:X6}{_longPrograma:X6}");

            // 2. Registros T (Texto) y M (Modificación)
            int tStart = -1;
            string tCode = "";
            int tByteCount = 0;

            Action flushT = () =>
            {
                if (tByteCount > 0)
                {
                    registros.Add($"T{tStart:X6}{tByteCount:X2}{tCode}");
                    tCode = "";
                    tByteCount = 0;
                    tStart = -1;
                }
            };

            foreach (var line in _lineas)
            {
                if (string.IsNullOrWhiteSpace(line.ObjectCode))
                    continue; // Skip directivas como BASE o líneas sin obj code

                string rawCode = line.ObjectCode;
                bool isRelocatable = rawCode.EndsWith("*");
                string cleanCode = rawCode.Replace("*", "");
                
                int bytesInLine = cleanCode.Length / 2;

                bool isContiguous = (tStart != -1) && ((tStart + tByteCount) == line.IntermLine.Address);
                bool wouldExceedLimit = (tByteCount + bytesInLine) > 30;

                // Si se rompe contigüidad de memoria o excede 30 bytes (60 caracteres hex), cerramos registro
                if (tStart != -1 && (!isContiguous || wouldExceedLimit))
                {
                    flushT();
                }

                if (tStart == -1)
                {
                    tStart = line.IntermLine.Address;
                }

                tCode += cleanCode;
                tByteCount += bytesInLine;

                // Generar registro de Modificación si es relocalizable (*)
                if (isRelocatable)
                {
                    int mAddress;
                    string lengthField;

                    // Si es una directiva WORD, modificamos los 3 bytes completos (06 medios bytes)
                    if (line.IntermLine.Operation.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                    {
                        mAddress = line.IntermLine.Address;
                        lengthField = "06";
                    }
                    else // Si es instrucción Formato 4, modificamos los 20 bits de dirección (05 medios bytes)
                    {
                        mAddress = line.IntermLine.Address + 1;
                        lengthField = "05";
                    }

                    // Símbolo externo (tomamos el nombre del programa inicial, 6 chars)
                    string mName = nombre;
                    mRecords.Add($"M{mAddress:X6}{lengthField}+{mName}");
                }
            }

            flushT(); // Vaciar remanentes

            // Agregar registros M posteriores a los registros T
            registros.AddRange(mRecords);

            // 3. Registro E (Fin)
            registros.Add($"E{_dirInicio:X6}");

            return registros;
        }

        public void ExportarACSV(string filePath)
        {
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
            var sb = new StringBuilder();
            sb.AppendLine("╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                     PROGRAMA OBJETO                                ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝");

            foreach (var r in GenerarRegistros())
            {
                sb.AppendLine(r);
            }

            return sb.ToString();
        }
    }
}
