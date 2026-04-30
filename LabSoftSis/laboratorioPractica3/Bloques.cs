using System;
using System.Collections.Generic;
using System.Linq;

namespace laboratorioPractica3
{
    public class BloqueInfo
    {
        public string Name { get; set; } = "Por Omision";
        public int Number { get; set; }
        public int StartAddress { get; set; }
        public int LocationCounter { get; set; }
        public int Length { get; set; }
    }

    internal class Bloques
    {
        private readonly Dictionary<string, BloqueInfo> _blocks = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<BloqueInfo> _orderedBlocks = new();
        private BloqueInfo _current;

        public Bloques()
        {
            _current = CreateBlock("Por Omision");
        }

        public string CurrentBlockName => _current.Name;
        public int CurrentBlockNumber => _current.Number;

        public int CurrentLocation
        {
            get => _current.LocationCounter;
            set => _current.LocationCounter = value;
        }

        public BloqueInfo CurrentBlock => _current;

        public BloqueInfo SwitchBlock(string? blockName)
        {
            string normalized = NormalizeBlockName(blockName);
            if (!_blocks.TryGetValue(normalized, out var block))
            {
                block = CreateBlock(normalized);
            }

            _current = block;
            return block;
        }

        public int FinalizeBlocks(int programStartAddress)
        {
            return FinalizarYAsignarDirecciones(programStartAddress);
        }

        public int GetBlockStartAddress(string? blockName)
        {
            string normalized = NormalizeBlockName(blockName);
            return _blocks.TryGetValue(normalized, out var block) ? block.StartAddress : 0;
        }

        public bool TryGetBlockStartAddress(string? blockName, out int startAddress)
        {
            string normalized = NormalizeBlockName(blockName);
            if (_blocks.TryGetValue(normalized, out var block))
            {
                startAddress = block.StartAddress;
                return true;
            }

            startAddress = 0;
            return false;
        }

        public IReadOnlyList<BloqueInfo> GetAllBlocks()
        {
            return _orderedBlocks.OrderBy(b => b.Number).ToList();
        }

        private BloqueInfo CreateBlock(string name)
        {
            var block = new BloqueInfo
            {
                Name = name,
                Number = _orderedBlocks.Count,
                StartAddress = 0,
                LocationCounter = 0,
                Length = 0
            };

            _blocks[name] = block;
            _orderedBlocks.Add(block);
            return block;
        }

        private int FinalizarYAsignarDirecciones(int programStartAddress)
        {
            int runningStart = programStartAddress;
            foreach (var block in _orderedBlocks.OrderBy(b => b.Number))
            {
                AsignarRangoBloque(block, runningStart);
                runningStart += block.Length;
            }

            return _orderedBlocks.Sum(b => b.Length);
        }

        private static void AsignarRangoBloque(BloqueInfo block, int startAddress)
        {
            block.Length = block.LocationCounter;
            block.StartAddress = startAddress;
        }

        private static string NormalizeBlockName(string? blockName)
        {
            return string.IsNullOrWhiteSpace(blockName) ? "Por Omision" : blockName.Trim();
        }
    }
}
