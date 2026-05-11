using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Parser tipado de programa objeto SIC/XE (registros H/D/R/T/M/E).
    /// Solo transforma texto a nodos/estructuras; no aplica lógica de carga.
    /// </summary>
    public class ObjectProgramParser
    {
        public List<ObjectModuleParsed> ParseCSV(string csvFilePath)
        {
            if (!File.Exists(csvFilePath))
                throw new FileNotFoundException($"Archivo no encontrado: {csvFilePath}");

            var records = new List<string>();
            foreach (var line in File.ReadLines(csvFilePath))
            {
                string cleaned = line.Trim();
                if (cleaned.StartsWith("\"") && cleaned.EndsWith("\"") && cleaned.Length >= 2)
                    cleaned = cleaned.Substring(1, cleaned.Length - 2);

                if (!string.IsNullOrWhiteSpace(cleaned))
                    records.Add(cleaned);
            }

            return ParseRecords(records);
        }

        public List<ObjectModuleParsed> ParseRecords(List<string> records)
        {
            var modules = new List<ObjectModuleParsed>();
            ObjectModuleParsed? currentModule = null;

            foreach (var record in records)
            {
                if (string.IsNullOrWhiteSpace(record))
                    continue;

                char recordType = record[0];

                switch (recordType)
                {
                    case 'H':
                        if (currentModule != null)
                            modules.Add(currentModule);
                        currentModule = ParseHeaderRecord(record);
                        break;

                    case 'D':
                        if (currentModule != null)
                            ParseDefinitionRecord(record, currentModule);
                        break;

                    case 'R':
                        if (currentModule != null)
                            ParseReferenceRecord(record, currentModule);
                        break;

                    case 'T':
                        if (currentModule != null)
                            ParseTextRecord(record, currentModule);
                        break;

                    case 'M':
                        if (currentModule != null)
                            ParseModificationRecord(record, currentModule);
                        break;

                    case 'E':
                        if (currentModule != null)
                        {
                            ParseEndRecord(record, currentModule);
                            modules.Add(currentModule);
                            currentModule = null;
                        }
                        break;
                }
            }

            if (currentModule != null)
                modules.Add(currentModule);

            return modules;
        }

        private static ObjectModuleParsed ParseHeaderRecord(string record)
        {
            var module = new ObjectModuleParsed();
            if (record.Length < 19)
                return module;

            module.Name = record.Substring(1, 6).Trim();
            module.StartAddress = int.Parse(record.Substring(7, 6), NumberStyles.HexNumber);
            module.Length = int.Parse(record.Substring(13, 6), NumberStyles.HexNumber);
            return module;
        }

        private static void ParseDefinitionRecord(string record, ObjectModuleParsed module)
        {
            for (int i = 1; i + 12 <= record.Length; i += 12)
            {
                string symbol = record.Substring(i, 6).Trim();
                int address = int.Parse(record.Substring(i + 6, 6), NumberStyles.HexNumber);
                module.Definitions.Add((symbol, address));
            }
        }

        private static void ParseReferenceRecord(string record, ObjectModuleParsed module)
        {
            for (int i = 1; i + 6 <= record.Length; i += 6)
            {
                string symbol = record.Substring(i, 6).Trim();
                if (!string.IsNullOrEmpty(symbol))
                    module.References.Add(symbol);
            }
        }

        private static void ParseTextRecord(string record, ObjectModuleParsed module)
        {
            if (record.Length < 9)
                return;

            int address = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber);
            int length = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber);
            string hexBytes = record.Length > 9 ? record.Substring(9) : string.Empty;

            // Normalizar según longitud declarada para evitar cargas fuera de especificación.
            if (hexBytes.Length > length * 2)
                hexBytes = hexBytes.Substring(0, length * 2);

            module.TextRecords.Add((address, hexBytes));
        }

        private static void ParseModificationRecord(string record, ObjectModuleParsed module)
        {
            if (record.Length < 16)
                return;

            int address = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber);
            int halfBytesLength = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber);
            char sign = record[9];
            string symbol = record.Substring(10, 6).Trim();

            module.ModificationRecords.Add(new ModificationRecord
            {
                Address = address,
                HalfBytesLength = halfBytesLength,
                Sign = sign,
                Symbol = symbol
            });
        }

        private static void ParseEndRecord(string record, ObjectModuleParsed module)
        {
            if (record.Length < 7)
                return;

            string addrStr = record.Substring(1, 6).Trim();
            if (!string.IsNullOrWhiteSpace(addrStr) && addrStr != "FFFFFF")
                module.ExecutionAddress = int.Parse(addrStr, NumberStyles.HexNumber);
        }
    }
}
