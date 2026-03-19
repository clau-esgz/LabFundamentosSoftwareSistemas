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
            // Ensambla la salida final del módulo objeto:
            // H (header), T (texto), M (modificación) y E (fin/entrada).
            // Aquí se aplican reglas de corte de T y relocalización.
            var registros = new List<string>();
            var registrosModificacion = new List<RegistroModificacion>();

            // 1) Registro H (Encabezado): nombre + direccion inicio + longitud
            // Nombre justificado a la izquierda con espacios (6 caracteres)
            string nombrePrograma = (_nombrePrograma ?? "").PadRight(6).Substring(0, 6);
            registros.Add($"H{nombrePrograma}{_direccionInicio:X6}{_longitudPrograma:X6}");

            // 2) Registros T (Texto) y M (Modificacion)
            int inicioTexto = -1;
            string codigoTexto = "";
            int conteoBytesTexto = 0;

            Action vaciarTexto = () =>
            {
                // Cierra un bloque T en construcción respetando formato:
                // T + dirección inicio + longitud + bytes objeto concatenados.
                if (conteoBytesTexto > 0)
                {
                    registros.Add($"T{inicioTexto:X6}{conteoBytesTexto:X2}{codigoTexto}");
                    codigoTexto = "";
                    conteoBytesTexto = 0;
                    inicioTexto = -1;
                }
            };

            foreach (var linea in _lineasCodigoObjeto)
            {
                if (string.IsNullOrWhiteSpace(linea.ObjectCode))
                {
                    // 3) Si hay error o directiva que no genera código y corta bloque, cerrar T vigente
                    if (!string.IsNullOrWhiteSpace(linea.ErrorPaso2) || !string.IsNullOrWhiteSpace(linea.IntermLine.Error))
                    {
                        vaciarTexto();
                        continue;
                    }

                    string operacion = linea.IntermLine.Operation?.Trim().ToUpperInvariant() ?? "";
                    if (operacion == "RESB" || operacion == "RESW" || operacion == "EQU" || operacion == "ORG" || operacion == "END")
                    {
                        vaciarTexto();
                    }
                    continue; // Directivas sin codigo objeto
                }

                string codigoCrudo = linea.ObjectCode;
                bool esRelocalizable = codigoCrudo.EndsWith("*");
                string codigoLimpio = codigoCrudo.Replace("*", "");

                int bytesLinea = codigoLimpio.Length / 2;

                bool esContiguo = (inicioTexto != -1) && ((inicioTexto + conteoBytesTexto) == linea.IntermLine.Address);
                bool excedeLimite = (conteoBytesTexto + bytesLinea) > 30;

                // 3) Si se rompe contigüidad de memoria o excede 30 bytes, se cierra el registro de texto
                if (inicioTexto != -1 && (!esContiguo || excedeLimite))
                {
                    vaciarTexto();
                }

                if (inicioTexto == -1)
                {
                    inicioTexto = linea.IntermLine.Address;
                }

                // 2) El codigo objeto se envia directamente al registro de texto
                codigoTexto += codigoLimpio;
                conteoBytesTexto += bytesLinea;

                // 6) Estructura para almacenar informacion de registros de modificacion
                if (esRelocalizable)
                {
                    int direccionMod;
                    int longitudMediosBytes;

                    // WORD: 3 bytes completos (06 medios bytes)
                    if (linea.IntermLine.Operation.Equals("WORD", StringComparison.OrdinalIgnoreCase))
                    {
                        direccionMod = linea.IntermLine.Address;
                        longitudMediosBytes = 0x06;
                    }
                    else // Formato 4: modificar 20 bits de direccion (05 medios bytes)
                    {
                        direccionMod = linea.IntermLine.Address + 1;
                        longitudMediosBytes = 0x05;
                    }

                    registrosModificacion.Add(new RegistroModificacion(
                        direccionMod,
                        longitudMediosBytes,
                        nombrePrograma));
                }
            }

            vaciarTexto(); // 9) Al final se escribe el ultimo registro de texto

            // 9) Generar registros de modificacion antes del registro de fin
            foreach (var registro in registrosModificacion)
            {
                registros.Add($"M{registro.Direccion:X6}{registro.LongitudMediosBytes:X2}+{registro.Nombre}");
            }

            // 4) Registro E (Fin)
            registros.Add($"E{_direccionEjecucion:X6}");

            return registros;
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
                Nombre = nombre;
            }

            public int Direccion { get; }
            public int LongitudMediosBytes { get; }
            public string Nombre { get; }
        }
    }
}
