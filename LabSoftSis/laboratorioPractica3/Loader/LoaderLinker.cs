using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Orquestador principal del cargador-ligador.
    /// Coordina: parser → Pass 1 → Pass 2 → reportes.
    /// </summary>
    public class LoaderLinker
    {
        private readonly ObjectProgramParser _parser = new();
        private readonly LoaderPass1 _pass1 = new();
        private readonly LoaderPass2 _pass2 = new();

        /// <summary>
        /// Punto de entrada principal: carga módulos objeto.
        /// </summary>
        public LoaderResult Load(List<string> objectRecords, int loadAddress = 0x000000)
        {
            var result = new LoaderResult { IsSuccessful = false };

            try
            {
                // 1. Parsear registros
                var modules = _parser.ParseRecords(objectRecords);
                if (modules.Count == 0)
                {
                    result.AllErrors.Add(new LoaderError
                    {
                        Message = "No se encontraron módulos objeto válidos",
                        Type = LoaderError.ErrorType.InvalidRecord
                    });
                    return result;
                }

                // 2. Ejecutar PASS 1: construir TABSE
                var tabse = new ExternalSymbolTable();
                var pass1 = _pass1.Execute(modules, loadAddress, tabse);
                result.Pass1 = pass1;
                result.AllErrors.AddRange(pass1.Errors);

                // Si hay errores críticos en Pass 1, abortar
                if (pass1.Errors.Any(e => e.Type == LoaderError.ErrorType.DuplicateSymbol))
                {
                    return result;
                }

                // 3. Ejecutar PASS 2: cargar en memoria
                var memoryManager = new MemoryManager();

                result.ExecutionEntryPoint = _pass2.Execute(pass1, modules, tabse, memoryManager, pass1.Errors);
                foreach (var kvp in pass1.SectionLoadAddresses)
                {
                    result.FinalSectionLoadAddresses[kvp.Key] = kvp.Value;
                }
                result.AllErrors.AddRange(pass1.Errors);

                result.IsSuccessful = result.AllErrors.Count == 0;

                return result;
            }
            catch (Exception ex)
            {
                result.AllErrors.Add(new LoaderError
                {
                    Message = $"Error durante carga: {ex.Message}",
                    Type = LoaderError.ErrorType.General
                });
                return result;
            }
        }

        /// <summary>
        /// Genera reporte de TABSE (tabla de símbolos externos).
        /// </summary>
        public string GenerateTABSEReport(LoaderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║           TABLA DE SÍMBOLOS EXTERNOS (TABSE)                      ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝\n");

            if (result.Pass1.SymbolTable.Count == 0)
            {
                sb.AppendLine("(sin símbolos externos)");
                return sb.ToString();
            }

            sb.AppendLine("Símbolo   | Dirección | Sección de Control");
            sb.AppendLine("━━━━━━━━━━┼───────────┼───────────────────────");

            foreach (var entry in result.Pass1.SymbolTable.Values.OrderBy(e => e.Name))
            {
                sb.AppendLine($"{entry.Name.PadRight(9)} | 0x{entry.Address:X6}  | {entry.ControlSectionName}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Genera reporte de mapeo de carga (secciones en memoria).
        /// </summary>
        public string GenerateLoadMapReport(LoaderResult result)
        {
            var sb = new StringBuilder();
            sb.AppendLine("\n╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║              MAPA DE CARGA DE SECCIONES                           ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝\n");

            sb.AppendLine($"Dirección inicial de carga: 0x{result.Pass1.InitialLoadAddress:X6}");
            sb.AppendLine($"Punto de entrada de ejecución: 0x{result.ExecutionEntryPoint:X6}\n");

            sb.AppendLine("Sección         | Dirección | Longitud");
            sb.AppendLine("────────────────┼───────────┼────────────");

            foreach (var (sectionName, address) in result.FinalSectionLoadAddresses.OrderBy(x => x.Value))
            {
                // Encontrar longitud buscando en Pass1
                // (simplificado: mostrar dirección, para longitud se necesitaría módulo original)
                sb.AppendLine($"{sectionName.PadRight(15)} | 0x{address:X6}  | (módulo)");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Genera reporte de errores.
        /// </summary>
        public string GenerateErrorReport(LoaderResult result)
        {
            if (result.AllErrors.Count == 0)
                return "\n✓ Carga exitosa, sin errores.\n";

            var sb = new StringBuilder();
            sb.AppendLine("\n╔════════════════════════════════════════════════════════════════════╗");
            sb.AppendLine("║                        ERRORES DE CARGA                           ║");
            sb.AppendLine("╚════════════════════════════════════════════════════════════════════╝\n");

            foreach (var error in result.AllErrors)
            {
                sb.AppendLine($"✗ [{error.Type}] {error.Message}");
            }

            return sb.ToString();
        }

        /// <summary>
        /// Imprime TABSE de forma legible en consola.
        /// </summary>
        public void PrintTABSE(LoaderResult result)
        {
            Console.WriteLine(GenerateTABSEReport(result));
        }

        /// <summary>
        /// Imprime mapa de carga.
        /// </summary>
        public void PrintLoadMap(LoaderResult result)
        {
            Console.WriteLine(GenerateLoadMapReport(result));
        }

        /// <summary>
        /// Imprime errores.
        /// </summary>
        public void PrintErrors(LoaderResult result)
        {
            Console.WriteLine(GenerateErrorReport(result));
        }
    }
}
