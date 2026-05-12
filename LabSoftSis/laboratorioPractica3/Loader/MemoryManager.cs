using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Globalization;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Gestor de memoria del cargador-ligador.
    /// Mantiene: mapa de memoria, tabla de símbolos (TABSE), validación de conflictos.
    /// </summary>
    public class MemoryManager
    {
        private readonly byte[] _memory;
        private readonly Dictionary<string, ExternalSymbolEntry> _tabse = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<(int Start, int Length, string Section)> _allocatedRegions = new();

        /// <summary>
        /// Inicializa gestor con tamaño máximo de memoria (por defecto 1MB).
        /// </summary>
        public int MaxMemorySize { get; } = 0x100000;

        public MemoryManager(int maxMemorySize = 0x100000)
        {
            MaxMemorySize = maxMemorySize;
            _memory = new byte[MaxMemorySize];
            // Inicializar con FF como solicitó el usuario
            for (int i = 0; i < _memory.Length; i++)
                _memory[i] = 0xFF;
        }

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
        public bool WriteMemory(int address, byte[] data, out LoaderError? error)
        {
            error = null;
            if (!ValidateRange(address, data.Length, out error))
                return false;

            for (int i = 0; i < data.Length; i++)
            {
                _memory[address + i] = data[i];
            }

            return true;
        }

        public bool WriteHex(int address, string hexBytes, out LoaderError? error)
        {
            error = null;
            string normalized = (hexBytes ?? string.Empty).Replace(" ", string.Empty);
            if (normalized.Length % 2 != 0)
                normalized = "0" + normalized;

            var data = new byte[normalized.Length / 2];
            for (int i = 0; i < data.Length; i++)
            {
                if (!byte.TryParse(normalized.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out data[i]))
                {
                    error = new LoaderError
                    {
                        Message = $"Byte hexadecimal inválido en T record: '{normalized.Substring(i * 2, 2)}'",
                        Type = LoaderError.ErrorType.InvalidRecord
                    };
                    return false;
                }
            }

            return WriteMemory(address, data, out error);
        }

        /// <summary>
        /// Lee bytes desde memoria.
        /// </summary>
        public byte[] ReadMemory(int address, int count)
        {
            if (address < 0 || count < 0 || address + count > MaxMemorySize)
                return Array.Empty<byte>();

            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = _memory[address + i];
            }
            return result;
        }

        /// <summary>
        /// Modifica bytes en memoria (para relocalización con registros M).
        /// Suma/resta offset a bytes existentes.
        /// </summary>
        public bool ModifyMemory(int address, int halfBytesLength, char sign, int offset, out LoaderError? error)
        {
            error = null;

            if (halfBytesLength <= 0)
            {
                error = new LoaderError
                {
                    Message = "Longitud de modificación inválida (half-bytes <= 0)",
                    Type = LoaderError.ErrorType.InvalidRecord
                };
                return false;
            }

            // En SIC/XE, si la longitud es impar (ej: 5 para formato 4), 
            // se asume que el campo está alineado a la derecha en los bytes que ocupa.
            // Esto significa que si es 5, ocupa el nibble bajo del primer byte y los 2 bytes siguientes.
            int bytesToTouch = (halfBytesLength + 1) / 2;
            if (!ValidateRange(address, bytesToTouch, out error))
                return false;

            // Determinar si empezamos en el primer o segundo nibble del primer byte
            bool startAtSecondNibble = (halfBytesLength % 2 != 0);

            // Extraer campo de nibbles (big-endian) a valor con signo.
            long rawValue = 0;
            for (int i = 0; i < halfBytesLength; i++)
            {
                int currentNibbleIndex = i + (startAtSecondNibble ? 1 : 0);
                int byteIndex = address + (currentNibbleIndex / 2);
                byte current = _memory[byteIndex];
                int nibble = (currentNibbleIndex % 2 == 0) ? ((current >> 4) & 0xF) : (current & 0xF);
                rawValue = (rawValue << 4) | (uint)nibble;
            }

            int bits = halfBytesLength * 4;
            long signMask = 1L << (bits - 1);
            long fullMask = (1L << bits) - 1;

            // Interpretar como valor con signo (aunque SIC/XE suele usar direcciones positivas, 
            // el campo podría ser parte de una expresión)
            long signedValue = (rawValue & signMask) != 0 ? rawValue - (1L << bits) : rawValue;
            long updatedValue = sign == '+' ? signedValue + offset : signedValue - offset;

            // El valor resultante debe caber en el número de medios bytes especificado
            // Para 5 medios bytes (20 bits), el rango es 0..0xFFFFF (o con signo)
            // Aquí simplemente aplicamos la máscara para mantener la longitud.
            long encoded = updatedValue & fullMask;

            // Escribir nibbles de regreso respetando nibbles no tocados
            for (int i = halfBytesLength - 1; i >= 0; i--)
            {
                int nibble = (int)(encoded & 0xF);
                encoded >>= 4;

                int currentNibbleIndex = i + (startAtSecondNibble ? 1 : 0);
                int byteIndex = address + (currentNibbleIndex / 2);
                
                if (currentNibbleIndex % 2 == 0)
                    _memory[byteIndex] = (byte)((_memory[byteIndex] & 0x0F) | (nibble << 4));
                else
                    _memory[byteIndex] = (byte)((_memory[byteIndex] & 0xF0) | nibble);
            }

            return true;
        }

        private bool ValidateRange(int address, int count, out LoaderError? error)
        {
            error = null;
            if (address < 0 || count < 0 || address + count > MaxMemorySize)
            {
                error = new LoaderError
                {
                    Message = $"Dirección inválida: 0x{address:X6}, tamaño=0x{count:X}",
                    Type = LoaderError.ErrorType.MemoryConflict
                };
                return false;
            }

            return true;
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
