using System;
using System.Collections.Generic;
using System.Linq;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Registro M tipado para el cargador-ligador (dirección relativa, longitud en half-bytes, signo y símbolo).
    /// </summary>
    public sealed class ModificationRecord
    {
        public int Address { get; init; }
        public int HalfBytesLength { get; init; }
        public char Sign { get; init; }
        public string Symbol { get; init; } = string.Empty;
    }

    /// <summary>
    /// Entrada en la tabla de símbolos externos (TABSE).
    /// Contiene: nombre del símbolo, dirección de carga, sección de control donde está definido.
    /// </summary>
    public class ExternalSymbolEntry
    {
        public string Name { get; set; } = string.Empty;
        public int Address { get; set; }
        public string ControlSectionName { get; set; } = "Por Omision";

        public override string ToString()
            => $"{Name.PadRight(8)} | {Address:X6}h | {ControlSectionName}";
    }

    /// <summary>
    /// Error durante carga/ligado. Acumula errores sin parar la ejecución.
    /// </summary>
    public class LoaderError
    {
        public enum ErrorType
        {
            DuplicateSymbol,
            UndefinedSymbol,
            MemoryConflict,
            InvalidRecord,
            General
        }

        public string Message { get; set; } = string.Empty;
        public ErrorType Type { get; set; }
        public int? ModuleIndex { get; set; }

        public override string ToString()
            => $"[{Type}] {Message}";
    }

    /// <summary>
    /// Registros parseados de un módulo objeto (H, D, R, T, M, E).
    /// </summary>
    public class ObjectModuleParsed
    {
        public string Name { get; set; } = "UNKNOWN";
        public int StartAddress { get; set; }
        public int Length { get; set; }

        /// <summary>
        /// D records: símbolo → dirección relativa dentro del módulo.
        /// </summary>
        public List<(string Symbol, int RelativeAddress)> Definitions { get; } = new();

        /// <summary>
        /// R records: símbolos externos referenciados.
        /// </summary>
        public List<string> References { get; } = new();

        /// <summary>
        /// T records: dirección relativa → bytes hex.
        /// </summary>
        public List<(int Address, string HexBytes)> TextRecords { get; } = new();

        /// <summary>
        /// M records: dirección → (longitud en medios bytes, signo +/-, símbolo).
        /// </summary>
        public List<ModificationRecord> ModificationRecords { get; } = new();

        /// <summary>
        /// E record: dirección de ejecución (0 si no especificada).
        /// </summary>
        public int ExecutionAddress { get; set; }
    }

    /// <summary>
    /// Resultado del Paso 1: TABSE lleno, mapa de secciones, errores.
    /// </summary>
    public class Pass1Data
    {
        /// <summary>
        /// Tabla de símbolos externos: nombre → entrada con dirección final.
        /// </summary>
        public Dictionary<string, ExternalSymbolEntry> SymbolTable { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Mapa: nombre de sección → dirección de carga en memoria.
        /// </summary>
        public Dictionary<string, int> SectionLoadAddresses { get; } = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Errores acumulados durante Pass 1.
        /// </summary>
        public List<LoaderError> Errors { get; } = new();

        /// <summary>
        /// Dirección inicial de carga (DIRPROG).
        /// </summary>
        public int InitialLoadAddress { get; set; }
    }

    /// <summary>
    /// TABSE orientada a objetos para el cargador-ligador.
    /// </summary>
    public class ExternalSymbolTable
    {
        private readonly Dictionary<string, ExternalSymbolEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyDictionary<string, ExternalSymbolEntry> Entries => _entries;

        public bool TryAdd(string name, int absoluteAddress, string controlSectionName, out LoaderError? error)
        {
            error = null;
            if (_entries.ContainsKey(name))
            {
                error = new LoaderError
                {
                    Message = $"Símbolo externo duplicado: {name}",
                    Type = LoaderError.ErrorType.DuplicateSymbol
                };
                return false;
            }

            _entries[name] = new ExternalSymbolEntry
            {
                Name = name,
                Address = absoluteAddress,
                ControlSectionName = controlSectionName
            };
            return true;
        }

        public bool TryGet(string symbol, out ExternalSymbolEntry? entry)
        {
            if (_entries.TryGetValue(symbol, out var resolved))
            {
                entry = resolved;
                return true;
            }

            entry = null;
            return false;
        }
    }

    /// <summary>
    /// Resultado final del cargador-ligador.
    /// </summary>
    public class LoaderResult
    {
        public Pass1Data Pass1 { get; set; } = new();
        public List<LoaderError> AllErrors { get; } = new();
        public bool IsSuccessful { get; set; }

        public int ExecutionEntryPoint { get; set; }
        public Dictionary<string, int> FinalSectionLoadAddresses { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Reporte de carga: formateado para terminal/archivo.
    /// </summary>
    public class LoaderReport
    {
        public string TABSEReport { get; set; } = string.Empty;
        public string LoadMapReport { get; set; } = string.Empty;
        public string ErrorReport { get; set; } = string.Empty;
    }
}
