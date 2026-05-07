using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Gestor de memoria del cargador-ligador.
    /// Mantiene: mapa de memoria, tabla de símbolos (TABSE), validación de conflictos.
    /// </summary>
    public class MemoryManager
    {
        private readonly Dictionary<int, byte> _memory = new();
        private readonly Dictionary<string, ExternalSymbolEntry> _tabse = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(int Start, int Length, string Section)> _allocatedRegions = new();

        /// <summary>
        /// Inicializa gestor con tamaño máximo de memoria (por defecto 1MB).
        /// </summary>
        public int MaxMemorySize { get; } = 0x100000;

        /// <summary>
        /// Suma símbolo a TABSE. Valida duplicados.
        /// </summary>
        public bool AddSymbol(string name, int address, string controlSectionName, out LoaderError? error)
        {
            error = null;

            if (_tabse.ContainsKey(name))
            {
                error = new LoaderError
                {
                    Message = $"Símbolo duplicado: {name}",
                    Type = LoaderError.ErrorType.DuplicateSymbol
                };
                return false;
            }

            _tabse[name] = new ExternalSymbolEntry
            {
                Name = name,
                Address = address,
                ControlSectionName = controlSectionName
            };

            return true;
        }

        /// <summary>
        /// Obtiene símbolo de TABSE.
        /// </summary>
        public ExternalSymbolEntry? GetSymbol(string name)
        {
            return _tabse.TryGetValue(name, out var entry) ? entry : null;
        }

        /// <summary>
        /// Retorna TABSE completa.
        /// </summary>
        public Dictionary<string, ExternalSymbolEntry> GetTABSE()
            => new Dictionary<string, ExternalSymbolEntry>(_tabse);

        /// <summary>
        /// Intenta reservar región de memoria.
        /// Valida: rango válido, sin solapamiento, dentro de límite.
        /// </summary>
        public bool AllocateMemory(int address, int size, string sectionName, out LoaderError? error)
        {
            error = null;

            if (address < 0 || address + size > MaxMemorySize)
            {
                error = new LoaderError
                {
                    Message = $"Dirección fuera de rango: 0x{address:X6} + 0x{size:X6}",
                    Type = LoaderError.ErrorType.MemoryConflict
                };
                return false;
            }

            // Validar solapamiento
            foreach (var (start, len, sec) in _allocatedRegions)
            {
                if (!(address + size <= start || address >= start + len))
                {
                    error = new LoaderError
                    {
                        Message = $"Conflicto memoria: [{address:X6}-{address + size:X6}] solapea con sección '{sec}' [{start:X6}-{start + len:X6}]",
                        Type = LoaderError.ErrorType.MemoryConflict
                    };
                    return false;
                }
            }

            _allocatedRegions.Add((address, size, sectionName));
            return true;
        }

        /// <summary>
        /// Escribe bytes en memoria a partir de dirección.
        /// </summary>
        public void WriteMemory(int address, byte[] data)
        {
            for (int i = 0; i < data.Length; i++)
            {
                _memory[address + i] = data[i];
            }
        }

        /// <summary>
        /// Lee bytes desde memoria.
        /// </summary>
        public byte[] ReadMemory(int address, int count)
        {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = _memory.TryGetValue(address + i, out var b) ? b : (byte)0;
            }
            return result;
        }

        /// <summary>
        /// Modifica bytes en memoria (para relocalización con registros M).
        /// Suma/resta offset a bytes existentes.
        /// </summary>
        public void ModifyMemory(int address, int halfBytesLength, char sign, int offset)
        {
            // halfBytesLength es la cantidad de medios bytes (nibbles) a modificar
            // Ej: 0x06 = 6 nibbles = 3 bytes (dirección de 24 bits en SIC/XE)

            int bytesToModify = (halfBytesLength + 1) / 2; // Redondear hacia arriba

            // Leer bytes actuales (big-endian para SIC/XE)
            byte[] currentBytes = ReadMemory(address, bytesToModify);
            int currentValue = 0;
            foreach (byte b in currentBytes)
            {
                currentValue = (currentValue << 8) | b;
            }

            // Aplicar modificación
            int newValue = sign == '+' ? currentValue + offset : currentValue - offset;

            // Escribir bytes modificados
            byte[] modifiedBytes = new byte[bytesToModify];
            for (int i = bytesToModify - 1; i >= 0; i--)
            {
                modifiedBytes[i] = (byte)(newValue & 0xFF);
                newValue >>= 8;
            }

            WriteMemory(address, modifiedBytes);
        }

        /// <summary>
        /// Crea dump legible de región de memoria.
        /// </summary>
        public string DumpMemory(int startAddress, int length)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"\n=== Memory Dump: 0x{startAddress:X6} - 0x{startAddress + length:X6} ===");

            for (int addr = startAddress; addr < startAddress + length; addr += 16)
            {
                sb.Append($"0x{addr:X6}: ");

                // Bytes hex
                for (int i = 0; i < 16 && addr + i < startAddress + length; i++)
                {
                    byte b = ReadMemory(addr + i, 1)[0];
                    sb.Append($"{b:X2} ");
                }

                sb.AppendLine();
            }

            return sb.ToString();
        }

        /// <summary>
        /// Retorna información de regiones asignadas.
        /// </summary>
        public List<(int Address, int Size, string Section)> GetAllocatedRegions()
            => _allocatedRegions.ToList();
    }
}
