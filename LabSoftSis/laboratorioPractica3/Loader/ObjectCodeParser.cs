using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Parseador de registros objeto (H/D/R/T/M/E) e implementación de Pass 1 y Pass 2.
    /// Sigue los algoritmos del PDF de cargador-ligador.
    /// Soporta: registros de texto directo + archivos CSV con registros entre comillas.
    /// </summary>
    public class ObjectCodeParser
    {
        /// <summary>
        /// Lee archivo CSV con registros objeto y los retorna como lista de módulos parseados.
        /// El CSV contiene líneas con registros entre comillas: "HMODULE000000000100", "DSYMBOL000000", etc.
        /// </summary>
        public List<ObjectModuleParsed> ParseCSV(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"Archivo no encontrado: {csvFilePath}");

            var records = new List<string>();
            foreach (var line in File.ReadLines(csvFilePath))
            {
                // Eliminar comillas al inicio y fin
                string cleaned = line.Trim();
                if (cleaned.StartsWith("\"") && cleaned.EndsWith("\""))
                {
                    cleaned = cleaned.Substring(1, cleaned.Length - 2);
                }

                if (!string.IsNullOrWhiteSpace(cleaned))
                {
                    records.Add(cleaned);
                }
            }

            return ParseRecords(records);
        }

        /// <summary>
        /// Parsea recordset (lista de líneas de registros) y retorna lista de módulos parseados.
        /// </summary>
        public List<ObjectModuleParsed> ParseRecords(List<string> records)
        {
            var modules = new List<ObjectModuleParsed>();
            var currentModule = new ObjectModuleParsed();
            bool inModule = false;

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record))
                    continue;

                char recordType = record[0];

                switch (recordType)
                {
                    case 'H': // Header
                        if (inModule && currentModule.Name != "UNKNOWN")
                            modules.Add(currentModule);

                        currentModule = ParseHeaderRecord(record);
                        inModule = true;
                        break;

                    case 'D': // Definition (external symbols)
                        if (inModule)
                            ParseDefinitionRecord(record, currentModule);
                        break;

                    case 'R': // Reference (external symbols referenced)
                        if (inModule)
                            ParseReferenceRecord(record, currentModule);
                        break;

                    case 'T': // Text (code object)
                        if (inModule)
                            ParseTextRecord(record, currentModule);
                        break;

                    case 'M': // Modification
                        if (inModule)
                            ParseModificationRecord(record, currentModule);
                        break;

                    case 'E': // End
                        if (inModule)
                            ParseEndRecord(record, currentModule);
                        modules.Add(currentModule);
                        inModule = false;
                        currentModule = new ObjectModuleParsed();
                        break;
                }
            }

            return modules;
        }

        private ObjectModuleParsed ParseHeaderRecord(string record)
        {
            // H[name(6)][start(6)][length(6)]
            var module = new ObjectModuleParsed();

            if (record.Length >= 13)
            {
                module.Name = record.Substring(1, 6).Trim();
                module.StartAddress = int.Parse(record.Substring(7, 6), NumberStyles.HexNumber);
                module.Length = int.Parse(record.Substring(13, 6), NumberStyles.HexNumber);
            }

            return module;
        }

        private void ParseDefinitionRecord(string record, ObjectModuleParsed module)
        {
            // D[sym1(6)][addr1(6)][sym2(6)][addr2(6)]...
            // Cada símbolo-dirección par ocupa 12 caracteres después de 'D'

            for (int i = 1; i + 12 <= record.Length; i += 12)
            {
                string symbol = record.Substring(i, 6).Trim();
                int address = int.Parse(record.Substring(i + 6, 6), NumberStyles.HexNumber);

                module.Definitions.Add((symbol, address));
            }
        }

        private void ParseReferenceRecord(string record, ObjectModuleParsed module)
        {
            // R[sym1(6)][sym2(6)]...
            // Cada símbolo ocupa 6 caracteres después de 'R'

            for (int i = 1; i + 6 <= record.Length; i += 6)
            {
                string symbol = record.Substring(i, 6).Trim();
                if (!string.IsNullOrEmpty(symbol))
                    module.References.Add(symbol);
            }
        }

        private void ParseTextRecord(string record, ObjectModuleParsed module)
        {
            // T[addr(6)][length(2)][hex_bytes...]
            if (record.Length < 9)
                return;

            int address = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber);
            int length = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber);

            string hexBytes = record.Length > 9 ? record.Substring(9) : string.Empty;

            module.TextRecords.Add((address, hexBytes));
        }

        private void ParseModificationRecord(string record, ObjectModuleParsed module)
        {
            // M[addr(6)][length(2)][sign(1)][symbol(6)]
            if (record.Length < 16)
                return;

            int address = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber);
            int halfBytesLength = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber);
            char sign = record[9];
            string symbol = record.Substring(10, 6).Trim();

            module.ModificationRecords.Add((address, halfBytesLength, sign, symbol));
        }

        private void ParseEndRecord(string record, ObjectModuleParsed module)
        {
            // E[addr(6)] o E (sin dirección)
            if (record.Length >= 7)
            {
                string addrStr = record.Substring(1, 6).Trim();
                if (!string.IsNullOrEmpty(addrStr) && addrStr != "      ")
                {
                    module.ExecutionAddress = int.Parse(addrStr, NumberStyles.HexNumber);
                }
            }
        }

        /// <summary>
        /// Ejecuta PASO 1 del algoritmo de cargador-ligador.
        /// Asigna direcciones a símbolos externos (construye TABSE).
        /// Entrada: módulos parseados + dirección inicial de carga.
        /// Salida: Pass1Data con TABSE llena.
        /// </summary>
        public Pass1Data ExecutePass1(List<ObjectModuleParsed> modules, int initialLoadAddress)
        {
            var pass1 = new Pass1Data { InitialLoadAddress = initialLoadAddress };

            int dirProg = initialLoadAddress; // Dirección inicial de carga (del SO)
            int dirSc = dirProg;               // Dirección de carga de sección actual

            foreach (var module in modules)
            {
                // Registrar dirección de carga para esta sección
                pass1.SectionLoadAddresses[module.Name] = dirSc;

                // Procesar registros D (definiciones de símbolos externos)
                foreach (var (symbol, relativeAddress) in module.Definitions)
                {
                    int absoluteAddress = dirSc + relativeAddress;

                    if (!pass1.AddSymbol(symbol, absoluteAddress, module.Name, out var error))
                    {
                        pass1.Errors.Add(error!);
                    }
                }

                // Avanzar a dirección de siguiente sección
                dirSc += module.Length;
            }

            return pass1;
        }

        /// <summary>
        /// Ejecuta PASO 2 del algoritmo de cargador-ligador.
        /// Carga módulos en memoria, relocaliza, resuelve referencias.
        /// Entrada: Pass1Data + módulos + MemoryManager.
        /// Salida: MemoryManager con memoria cargada + punto de ejecución.
        /// </summary>
        public void ExecutePass2(Pass1Data pass1, List<ObjectModuleParsed> modules, MemoryManager memoryManager, out int executionEntryPoint)
        {
            executionEntryPoint = 0;
            int dirSc = pass1.InitialLoadAddress;

            foreach (var module in modules)
            {
                // Asignar dirección de carga para esta sección (ya calculada en Pass 1)
                dirSc = pass1.SectionLoadAddresses[module.Name];

                // Validar que la región sea reservable
                if (!memoryManager.AllocateMemory(dirSc, module.Length, module.Name, out var allocError))
                {
                    pass1.Errors.Add(allocError!);
                    continue;
                }

                // Procesar registros T (texto/código objeto)
                foreach (var (tAddr, hexBytes) in module.TextRecords)
                {
                    int absoluteAddress = dirSc + tAddr;

                    // Convertir hex string a bytes
                    byte[] bytes = ConvertHexStringToBytes(hexBytes);
                    memoryManager.WriteMemory(absoluteAddress, bytes);
                }

                // Procesar registros M (modificación/relocalización)
                foreach (var (mAddr, halfBytesLen, sign, symbol) in module.ModificationRecords)
                {
                    int absoluteAddress = dirSc + mAddr;

                    // Buscar símbolo en TABSE
                    var symbolEntry = memoryManager.GetSymbol(symbol);
                    if (symbolEntry == null)
                    {
                        pass1.Errors.Add(new LoaderError
                        {
                            Message = $"Símbolo indefinido en M record: {symbol}",
                            Type = LoaderError.ErrorType.UndefinedSymbol
                        });
                        continue;
                    }

                    // Modificar memoria: suma/resta dirección del símbolo
                    memoryManager.ModifyMemory(absoluteAddress, halfBytesLen, sign, symbolEntry.Address);
                }

                // Procesar registro E (fin de módulo, dirección de ejecución)
                if (module.ExecutionAddress != 0)
                {
                    executionEntryPoint = dirSc + module.ExecutionAddress;
                }
            }
        }

        private byte[] ConvertHexStringToBytes(string hexString)
        {
            // Eliminar espacios
            hexString = hexString.Replace(" ", string.Empty);

            if (hexString.Length % 2 != 0)
                hexString = "0" + hexString;

            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = byte.Parse(hexString.Substring(i * 2, 2), NumberStyles.HexNumber);
            }

            return bytes;
        }
    }

    /// <summary>
    /// Extensión de Pass1Data para acceso en ObjectCodeParser.
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
