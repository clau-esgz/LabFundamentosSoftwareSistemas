using System.Collections.Generic;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Fachada de compatibilidad.
    /// Mantiene API histórica (ObjectCodeParser) delegando a:
    /// - ObjectProgramParser (parseo)
    /// - LoaderPass1 (TABSE)
    /// - LoaderPass2 (carga/ligado)
    /// </summary>
    public class ObjectCodeParser
    {
        private readonly ObjectProgramParser _programParser = new();
        private readonly LoaderPass1 _pass1 = new();
        private readonly LoaderPass2 _pass2 = new();

        public List<ObjectModuleParsed> ParseCSV(string csvFilePath)
            => _programParser.ParseCSV(csvFilePath);

        public List<ObjectModuleParsed> ParseRecords(List<string> records)
            => _programParser.ParseRecords(records);

        public Pass1Data ExecutePass1(List<ObjectModuleParsed> modules, int initialLoadAddress)
        {
            var tabse = new ExternalSymbolTable();
            return _pass1.Execute(modules, initialLoadAddress, tabse);
        }

        public void ExecutePass2(Pass1Data pass1, List<ObjectModuleParsed> modules, MemoryManager memoryManager, out int executionEntryPoint)
        {
            var tabse = new ExternalSymbolTable();
            foreach (var kvp in pass1.SymbolTable)
            {
                _ = tabse.TryAdd(kvp.Key, kvp.Value.Address, kvp.Value.ControlSectionName, out _);
            }

            executionEntryPoint = _pass2.Execute(pass1, modules, tabse, memoryManager, pass1.Errors);
        }
    }

    /// <summary>
    /// Extensión de compatibilidad para agregar símbolos a Pass1Data.
    /// </summary>
    public static class Pass1DataExtensions
    {
        public static bool AddSymbol(this Pass1Data pass1, string name, int address, string controlSectionName, out LoaderError? error)
        {
            error = null;

            if (pass1.SymbolTable.ContainsKey(name))
            {
                error = new LoaderError
                {
                    Message = $"Símbolo duplicado: {name}",
                    Type = LoaderError.ErrorType.DuplicateSymbol
                };
                return false;
            }

            pass1.SymbolTable[name] = new ExternalSymbolEntry
            {
                Name = name,
                Address = address,
                ControlSectionName = controlSectionName
            };

            return true;
        }
    }
}
