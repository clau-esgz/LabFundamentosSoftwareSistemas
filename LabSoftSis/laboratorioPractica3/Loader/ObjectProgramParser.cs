using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using laboratorioPractica3.Records;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Parser tipado de programa objeto SIC/XE (registros H/D/R/T/M/E).
    /// Utiliza las clases de registros tipados para transformar texto en estructuras.
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
                        
                        var h = HeaderRecord.Parse(record);
                        currentModule = new ObjectModuleParsed
                        {
                            Name = h.ProgramName,
                            StartAddress = h.StartAddress,
                            Length = h.Length
                        };
                        break;

                    case 'D':
                        if (currentModule != null)
                        {
                            var d = DefineRecord.Parse(record);
                            foreach (var def in d.Definitions)
                                currentModule.Definitions.Add((def.Symbol, def.Address));
                        }
                        break;

                    case 'R':
                        if (currentModule != null)
                        {
                            var r = ReferRecord.Parse(record);
                            foreach (var refSym in r.References)
                                currentModule.References.Add(refSym);
                        }
                        break;

                    case 'T':
                        if (currentModule != null)
                        {
                            var t = TextRecord.Parse(record);
                            currentModule.TextRecords.Add((t.StartAddress, t.HexBytes));
                        }
                        break;

                    case 'M':
                        if (currentModule != null)
                        {
                            var m = Records.ModificationRecord.Parse(record);
                            currentModule.ModificationRecords.Add(new Loader.ModificationRecord
                            {
                                Address = m.Address,
                                HalfBytesLength = m.HalfBytesLength,
                                Sign = m.Sign,
                                Symbol = m.Symbol
                            });
                        }
                        break;

                    case 'E':
                        if (currentModule != null)
                        {
                            var e = EndRecord.Parse(record);
                            if (e.ExecutionAddress.HasValue)
                                currentModule.ExecutionAddress = e.ExecutionAddress.Value;
                            
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
    }
}
