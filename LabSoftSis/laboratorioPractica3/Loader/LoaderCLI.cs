using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Interfaz de línea de comandos (CLI) para el cargador-ligador.
    /// Proporciona menú interactivo para cargar módulos, ver TABSE, etc.
    /// </summary>
    public class LoaderCLI
    {
        private readonly LoaderLinker _loader = new();
        private LoaderResult? _currentResult;
        private List<string> _currentRecords = new();
        private string? _lastAutoReportPath;

        /// <summary>
        /// Ejecuta el REPL interactivo del cargador-ligador.
        /// </summary>
        public void RunInteractive()
        {
            Console.Clear();
            PrintBanner();

            while (true)
            {
                PrintMenu();
                string option = Console.ReadLine()?.Trim() ?? "";

                switch (option)
                {
                    case "1":
                        LoadObjectFile();
                        break;

                    case "2":
                        DisplayTABSE();
                        break;

                    case "3":
                        DisplayMemoryDump();
                        break;

                    case "4":
                        ExportReport();
                        break;

                    case "5":
                        Console.WriteLine("\n¡Hasta luego!");
                        return;

                    default:
                        Console.WriteLine("✗ Opción no válida.");
                        break;
                }

                Console.WriteLine("\nPresiona ENTER para continuar...");
                Console.ReadLine();
                Console.Clear();
            }
        }

        private void PrintBanner()
        {
            Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
            Console.WriteLine("║   CARGADOR-LIGADOR PARA SIC/XE (Arquitectura Simple)          ║");
            Console.WriteLine("║   Implementación de Dos Pasos según Fundamentos de Sistemas   ║");
            Console.WriteLine("╚════════════════════════════════════════════════════════════════╝\n");
        }

        private void PrintMenu()
        {
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("  MENÚ PRINCIPAL");
            Console.WriteLine("═══════════════════════════════════════");
            Console.WriteLine("1. Cargar archivo .obj");
            Console.WriteLine("2. Mostrar TABSE (Tabla de Símbolos Externos)");
            Console.WriteLine("3. Mostrar dump de memoria");
            Console.WriteLine("4. Exportar reporte");
            Console.WriteLine("5. Salir");
            Console.WriteLine("═══════════════════════════════════════");
            Console.Write("\nSelecciona opción: ");
        }

        private void LoadObjectFile()
        {
            Console.WriteLine("\n--- CARGAR ARCHIVO DE MÓDULO OBJETO ---\n");

            Console.Write("Ruta del archivo (.obj, .txt, o .csv): ");
            string filePath = Console.ReadLine()?.Trim() ?? "";

            if (!File.Exists(filePath))
            {
                Console.WriteLine($"✗ Archivo no encontrado: {filePath}");
                return;
            }

            try
            {
                // Detectar formato del archivo
                bool isCSV = filePath.EndsWith(".csv", StringComparison.OrdinalIgnoreCase);

                if (isCSV)
                {
                    // Leer CSV: cada línea es un registro entre comillas
                    _currentRecords = File.ReadAllLines(filePath)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l =>
                        {
                            // Eliminar comillas al inicio y fin
                            string cleaned = l.Trim();
                            if (cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
                                cleaned = cleaned.Substring(1, cleaned.Length - 2);
                            return cleaned;
                        })
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }
                else
                {
                    // Leer archivo de texto plano
                    _currentRecords = File.ReadAllLines(filePath)
                        .Where(l => !string.IsNullOrWhiteSpace(l))
                        .ToList();
                }

                Console.WriteLine($"✓ Se cargaron {_currentRecords.Count} registros ({(isCSV ? "CSV" : "Texto")}).");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"✗ Error al leer archivo: {ex.Message}");
                return;
            }

            Console.Write("\nDirección de carga inicial (hex, ej: 000000): ");
            string addrStr = Console.ReadLine()?.Trim() ?? "000000";

            int loadAddress = 0;
            try
            {
                loadAddress = int.Parse(addrStr, System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                Console.WriteLine("✗ Dirección inválida, usando 0x000000");
                loadAddress = 0;
            }

            Console.WriteLine("\nEjecutando carga (Pass 1 + Pass 2)...");
            _currentResult = _loader.Load(_currentRecords, loadAddress);

            if (_currentResult.IsSuccessful)
            {
                Console.WriteLine("✓ Carga exitosa!");
            }
            else
            {
                Console.WriteLine("⚠ Carga completada con errores:");
                _loader.PrintErrors(_currentResult);
            }

            _loader.PrintTABSE(_currentResult);
            _loader.PrintLoadMap(_currentResult);

            // Exportación automática de artefactos del linker (TXT + CSV)
            if (TryAutoExportReports(filePath, _currentResult, out var txtPath, out var csvPath, out var exportError))
            {
                _lastAutoReportPath = txtPath;
                Console.WriteLine("\n✓ Reportes del cargador-ligador generados:");
                Console.WriteLine($"  - TXT: {txtPath}");
                Console.WriteLine($"  - CSV: {csvPath}");
            }
            else if (!string.IsNullOrWhiteSpace(exportError))
            {
                Console.WriteLine($"\n⚠ No se pudieron exportar reportes automáticos: {exportError}");
            }
        }

        private void DisplayTABSE()
        {
            if (_currentResult == null)
            {
                Console.WriteLine("\n✗ Primero debes cargar un archivo.");
                return;
            }

            _loader.PrintTABSE(_currentResult);
        }

        private void DisplayMemoryDump()
        {
            if (_currentResult == null)
            {
                Console.WriteLine("\n✗ Primero debes cargar un archivo.");
                return;
            }

            Console.WriteLine("\n--- DUMP DE MEMORIA ---\n");

            Console.Write("Dirección inicial (hex, ej: 000000): ");
            string startStr = Console.ReadLine()?.Trim() ?? "000000";

            int startAddr = 0;
            try
            {
                startAddr = int.Parse(startStr, System.Globalization.NumberStyles.HexNumber);
            }
            catch
            {
                Console.WriteLine("✗ Dirección inválida.");
                return;
            }

            Console.Write("Longitud (bytes, ej: 256): ");
            string lenStr = Console.ReadLine()?.Trim() ?? "256";

            int length = 256;
            try
            {
                length = int.Parse(lenStr);
            }
            catch
            {
                Console.WriteLine("✗ Longitud inválida, usando 256 bytes.");
            }

            Console.WriteLine($"\n(Dump de {length} bytes desde 0x{startAddr:X6})");
            Console.WriteLine("(Nota: La memoria se llena durante Pass 2 - aquí mostramos estructura)");
        }

        private void ExportReport()
        {
            if (_currentResult == null)
            {
                Console.WriteLine("\n✗ Primero debes cargar un archivo.");
                return;
            }

            Console.WriteLine("\n--- EXPORTAR REPORTE ---\n");

            if (TryAutoExportReports("loader_manual_export", _currentResult, out var txtPath, out var csvPath, out var error))
            {
                _lastAutoReportPath = txtPath;
                Console.WriteLine($"✓ TXT exportado: {txtPath}");
                Console.WriteLine($"✓ CSV exportado: {csvPath}");
                return;
            }

            Console.WriteLine($"✗ Error al escribir reporte: {error}");
        }

        private bool TryAutoExportReports(string sourceFilePath, LoaderResult result, out string txtPath, out string csvPath, out string error)
        {
            txtPath = string.Empty;
            csvPath = string.Empty;
            error = string.Empty;

            try
            {
                string projectDir = GetProjectDirectory();
                string reportDir = Path.Combine(projectDir, "reportes_loader");
                Directory.CreateDirectory(reportDir);

                string sourceName = Path.GetFileNameWithoutExtension(sourceFilePath);
                if (string.IsNullOrWhiteSpace(sourceName))
                    sourceName = "loader";

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                txtPath = Path.Combine(reportDir, $"{sourceName}_LINKER_{timestamp}.txt");
                csvPath = Path.Combine(reportDir, $"{sourceName}_LINKER_{timestamp}.csv");

                WriteTxtReport(txtPath, result);
                WriteCsvReport(csvPath, result);
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        private void WriteTxtReport(string outputPath, LoaderResult result)
        {
            using var sw = new StreamWriter(outputPath);
            sw.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
            sw.WriteLine("║          REPORTE DEL CARGADOR-LIGADOR SIC/XE                    ║");
            sw.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
            sw.WriteLine(_loader.GenerateTABSEReport(result));
            sw.WriteLine(_loader.GenerateLoadMapReport(result));
            sw.WriteLine(_loader.GenerateErrorReport(result));
        }

        private void WriteCsvReport(string outputPath, LoaderResult result)
        {
            var lines = new List<string>
            {
                "SECCION,CLAVE,VALOR1,VALOR2,VALOR3"
            };

            lines.Add($"RESUMEN,DIRPROG,{result.Pass1.InitialLoadAddress:X6},," );
            lines.Add($"RESUMEN,DIREJ,{result.ExecutionEntryPoint:X6},," );
            lines.Add($"RESUMEN,ESTADO,{(result.IsSuccessful ? "OK" : "CON_ERRORES")},," );

            foreach (var entry in result.Pass1.SymbolTable.Values.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                lines.Add($"TABSE,{EscapeCsv(entry.Name)},{entry.Address:X6},{EscapeCsv(entry.ControlSectionName)},");
            }

            foreach (var sec in result.FinalSectionLoadAddresses.OrderBy(s => s.Value))
            {
                lines.Add($"SECCIONES,{EscapeCsv(sec.Key)},{sec.Value:X6},,");
            }

            foreach (var err in result.AllErrors)
            {
                lines.Add($"ERRORES,{EscapeCsv(err.Type.ToString())},{EscapeCsv(err.Message)},,");
            }

            File.WriteAllLines(outputPath, lines);
        }

        private static string GetProjectDirectory()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (dir.GetFiles("*.csproj").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }

            return Directory.GetCurrentDirectory();
        }

        private static string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            bool needsQuotes = value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r');
            if (!needsQuotes)
                return value;

            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
    }
}
