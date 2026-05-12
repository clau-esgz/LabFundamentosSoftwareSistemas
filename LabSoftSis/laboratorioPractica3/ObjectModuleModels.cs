using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using laboratorioPractica3.Records;

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
        public List<Records.DefineRecord> D { get; } = new();
        public List<Records.ReferRecord> R { get; } = new();
        public List<Records.TextRecord> T { get; } = new();
        public List<Records.ModificationRecord> M { get; } = new();
        public Records.EndRecord? E { get; set; }

        public List<string> ToRecords(bool emitEndAddress)
        {
            var output = new List<string>();
            
            var h = new HeaderRecord { ProgramName = Name, StartAddress = StartAddress, Length = Length };
            output.Add(h.Serialize());

            foreach (var d in D) output.Add(d.Serialize());
            foreach (var r in R) output.Add(r.Serialize());
            foreach (var t in T) output.Add(t.Serialize());
            foreach (var m in M) output.Add(m.Serialize());

            if (E != null)
            {
                var e = new Records.EndRecord { ExecutionAddress = emitEndAddress ? E.ExecutionAddress : (int?)null };
                output.Add(e.Serialize());
            }
            else
            {
                output.Add(new Records.EndRecord().Serialize());
            }

            return output;
        }
    }
}
