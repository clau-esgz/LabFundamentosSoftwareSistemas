using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace laboratorioPractica3
{
    public sealed class ControlSection
    {
        public string Name { get; set; } = "Por Omision";
        public int Number { get; set; }
        public int ProgramCounter { get; set; }
        public int Length { get; set; }

        public Dictionary<string, Symbol> SymbolTable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Block> BlockTable { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExternalDefinitions { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExternalReferences { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public class Symbol
    {
        public string Name { get; set; } = string.Empty;
        public int? AddressOrValue { get; set; }
        public char Type { get; set; } = '-'; // A / R / '-'
        public int BlockNumber { get; set; }
        public bool IsExternal { get; set; }
    }

    public sealed class ExternalSymbol
    {
        public string Name { get; set; } = string.Empty;
        public bool IsDefinedInSection { get; set; }
        public int? RelativeAddress { get; set; }
    }

    public sealed class Block
    {
        public string Name { get; set; } = "Por Omision";
        public int Number { get; set; }
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public int LocationCounter { get; set; }
    }

    public sealed class ObjectModule
    {
        public string Name { get; set; } = "NONAME";
        public int StartAddress { get; set; }
        public int Length { get; set; }
        public List<DefineRecord> D { get; } = new();
        public List<ReferRecord> R { get; } = new();
        public List<TextRecord> T { get; } = new();
        public List<ModificationRecord> M { get; } = new();
        public EndRecord? E { get; set; }

        public List<string> ToRecords(bool emitEndAddress)
        {
            var output = new List<string>();
            output.Add(BuildHeaderRecord());
            AddDefinitionRecords(output);
            AddReferenceRecords(output);
            AddTextRecords(output);
            AddModificationRecords(output);
            output.Add(BuildEndRecord(emitEndAddress));

            return output;
        }

        private string BuildHeaderRecord()
            => $"H{Name.PadRight(6).Substring(0, 6)}{StartAddress:X6}{Length:X6}";

        private void AddDefinitionRecords(List<string> output)
        {
            if (D.Count == 0)
                return;

            var chunk = new StringBuilder("D");
            foreach (var d in D)
            {
                chunk.Append(d.Symbol.PadRight(6).Substring(0, 6));
                chunk.Append(d.RelativeAddress.ToString("X6"));
            }
            output.Add(chunk.ToString());
        }

        private void AddReferenceRecords(List<string> output)
        {
            if (R.Count == 0)
                return;

            var chunk = new StringBuilder("R");
            foreach (var r in R)
                chunk.Append(r.Symbol.PadRight(6).Substring(0, 6));
            output.Add(chunk.ToString());
        }

        private void AddTextRecords(List<string> output)
            => output.AddRange(T.Select(t => t.ToRecord()));

        private void AddModificationRecords(List<string> output)
            => output.AddRange(M.Select(m => m.ToRecord()));

        private string BuildEndRecord(bool emitEndAddress)
        {
            if (E != null)
                return emitEndAddress ? $"E{E.Value.ExecutionAddress:X6}" : "E      ";

            return emitEndAddress ? "E000000" : "E      ";
        }
    }

    public readonly struct DefineRecord
    {
        public DefineRecord(string symbol, int relativeAddress)
        {
            Symbol = symbol;
            RelativeAddress = relativeAddress;
        }

        public string Symbol { get; }
        public int RelativeAddress { get; }
    }

    public readonly struct ReferRecord
    {
        public ReferRecord(string symbol)
        {
            Symbol = symbol;
        }

        public string Symbol { get; }
    }

    public sealed class TextRecord
    {
        public int StartAddress { get; set; }
        public string HexBytes { get; set; } = string.Empty;

        public int LengthInBytes => string.IsNullOrEmpty(HexBytes) ? 0 : HexBytes.Length / 2;

        public string ToRecord() => $"T{StartAddress:X6}{LengthInBytes:X2}{HexBytes}";
    }

    public readonly struct ModificationRecord
    {
        public ModificationRecord(int address, int halfBytesLength, char sign, string symbol)
        {
            Address = address;
            HalfBytesLength = halfBytesLength;
            Sign = sign;
            Symbol = symbol;
        }

        public int Address { get; }
        public int HalfBytesLength { get; }
        public char Sign { get; }
        public string Symbol { get; }

        public string ToRecord() => $"M{Address:X6}{HalfBytesLength:X2}{Sign}{Symbol.PadRight(6).Substring(0, 6)}";
    }

    public readonly struct EndRecord
    {
        public EndRecord(int executionAddress)
        {
            ExecutionAddress = executionAddress;
        }

        public int ExecutionAddress { get; }
    }
}
