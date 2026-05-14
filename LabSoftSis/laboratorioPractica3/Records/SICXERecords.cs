using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace laboratorioPractica3.Records
{
    /// <summary>
    /// Interfaz base para todos los registros del programa objeto SIC/XE.
    /// </summary>
    public interface ISICXERecord
    {
        string Serialize();
        bool Validate(out string error);
    }

    /// <summary>
    /// Registro de Cabecera (Header).
    /// H[Nombre:6][Dir. Inicio:6][Longitud:6]
    /// </summary>
    public class HeaderRecord : ISICXERecord
    {
        public string ProgramName { get; set; } = string.Empty;
        public int StartAddress { get; set; }
        public int Length { get; set; }

        public string Serialize()
        {
            string name = (ProgramName ?? "").PadRight(6).Substring(0, 6);
            return $"H{name}{StartAddress:X6}{Length:X6}";
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(ProgramName)) error = "Nombre del programa es requerido.";
            else if (ProgramName.Length > 6) error = "Nombre del programa excede 6 caracteres.";
            if (StartAddress < 0) error = "Dirección de inicio negativa.";
            if (Length < 0) error = "Longitud del programa negativa.";
            return string.IsNullOrEmpty(error);
        }

        public static HeaderRecord Parse(string record)
        {
            if (record == null || record.Length < 19 || record[0] != 'H')
                throw new ArgumentException("Registro H inválido.");

            return new HeaderRecord
            {
                ProgramName = record.Substring(1, 6).Trim(),
                StartAddress = int.Parse(record.Substring(7, 6), NumberStyles.HexNumber),
                Length = int.Parse(record.Substring(13, 6), NumberStyles.HexNumber)
            };
        }
    }

    /// <summary>
    /// Registro de Definición Externa (Define).
    /// D[Símbolo:6][Dirección:6]... (múltiples pares)
    /// </summary>
    public class DefineRecord : ISICXERecord
    {
        public List<(string Symbol, int Address)> Definitions { get; } = new();

        public string Serialize()
        {
            var sb = new StringBuilder("D");
            foreach (var (symbol, address) in Definitions)
            {
                sb.Append(symbol.PadRight(6).Substring(0, 6));
                sb.Append(address.ToString("X6"));
            }
            return sb.ToString();
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (Definitions.Count == 0) error = "Registro D requiere al menos una definición.";
            foreach (var (symbol, _) in Definitions)
                if (symbol.Length > 6) error = $"Símbolo '{symbol}' excede 6 caracteres.";
            return string.IsNullOrEmpty(error);
        }

        public static DefineRecord Parse(string record)
        {
            if (record == null || record.Length < 13 || record[0] != 'D')
                throw new ArgumentException("Registro D inválido.");

            var def = new DefineRecord();
            for (int i = 1; i + 12 <= record.Length; i += 12)
            {
                string symbol = record.Substring(i, 6).Trim();
                int address = int.Parse(record.Substring(i + 6, 6), NumberStyles.HexNumber);
                def.Definitions.Add((symbol, address));
            }
            return def;
        }
    }

    /// <summary>
    /// Registro de Referencia Externa (Refer).
    /// R[Símbolo:6]... (múltiples símbolos)
    /// </summary>
    public class ReferRecord : ISICXERecord
    {
        public List<string> References { get; } = new();

        public string Serialize()
        {
            var sb = new StringBuilder("R");
            foreach (var symbol in References)
            {
                sb.Append(symbol.PadRight(6).Substring(0, 6));
            }
            return sb.ToString();
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (References.Count == 0) error = "Registro R requiere al menos una referencia.";
            foreach (var symbol in References)
                if (symbol.Length > 6) error = $"Símbolo '{symbol}' excede 6 caracteres.";
            return string.IsNullOrEmpty(error);
        }

        public static ReferRecord Parse(string record)
        {
            if (record == null || record.Length < 7 || record[0] != 'R')
                throw new ArgumentException("Registro R inválido.");

            var refRec = new ReferRecord();
            for (int i = 1; i + 6 <= record.Length; i += 6)
            {
                string symbol = record.Substring(i, 6).Trim();
                if (!string.IsNullOrEmpty(symbol))
                    refRec.References.Add(symbol);
            }
            return refRec;
        }
    }

    /// <summary>
    /// Registro de Texto (Text).
    /// T[Dir. Inicio:6][Longitud:2][Bytes Hex:...]
    /// </summary>
    public class TextRecord : ISICXERecord
    {
        public int StartAddress { get; set; }
        public string HexBytes { get; set; } = string.Empty;

        public int LengthInBytes => HexBytes.Length / 2;

        public string Serialize()
        {
            return $"T{StartAddress:X6}{LengthInBytes:X2}{HexBytes.ToUpperInvariant()}";
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (StartAddress < 0) error = "Dirección de inicio negativa.";
            if (string.IsNullOrEmpty(HexBytes)) error = "Registro T no tiene contenido.";
            if (HexBytes.Length % 2 != 0) error = "Longitud de bytes hexadecimales impar.";
            if (LengthInBytes > 30) error = "Registro T excede los 30 bytes.";
            return string.IsNullOrEmpty(error);
        }

        public static TextRecord Parse(string record)
        {
            if (record == null || record.Length < 9 || record[0] != 'T')
                throw new ArgumentException("Registro T inválido.");

            int length = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber);
            return new TextRecord
            {
                StartAddress = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber),
                HexBytes = record.Substring(9, Math.Min(length * 2, record.Length - 9))
            };
        }
    }

    /// <summary>
    /// Registro de Modificación (Modification).
    /// M[Dir. Inicio:6][Longitud (half-bytes):2][Signo:1][Símbolo:6]
    /// </summary>
    public class ModificationRecord : ISICXERecord
    {
        public int Address { get; set; }
        public int HalfBytesLength { get; set; }
        public char Sign { get; set; } = '+';
        public string Symbol { get; set; } = string.Empty;

        public string Serialize()
        {
            string sym = (Symbol ?? "").PadRight(6).Substring(0, 6);
            return $"M{Address:X6}{HalfBytesLength:X2}{Sign}{sym}";
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (Address < 0) error = "Dirección negativa.";
            if (Sign != '+' && Sign != '-') error = "Signo de modificación inválido.";
            if (string.IsNullOrWhiteSpace(Symbol)) error = "Símbolo de modificación requerido.";
            return string.IsNullOrEmpty(error);
        }

        public static ModificationRecord Parse(string record)
        {
            if (record == null || record.Length < 16 || record[0] != 'M')
                throw new ArgumentException("Registro M inválido.");

            return new ModificationRecord
            {
                Address = int.Parse(record.Substring(1, 6), NumberStyles.HexNumber),
                HalfBytesLength = int.Parse(record.Substring(7, 2), NumberStyles.HexNumber),
                Sign = record[9],
                Symbol = record.Substring(10, 6).Trim()
            };
        }
    }

    /// <summary>
    /// Registro de Fin (End).
    /// E[Dir. Ejecución:6]
    /// </summary>
    public class EndRecord : ISICXERecord
    {
        public int? ExecutionAddress { get; set; }

        public string Serialize()
        {
            if (ExecutionAddress.HasValue)
                return $"E{ExecutionAddress.Value:X6}";
            return "E      ";
        }

        public bool Validate(out string error)
        {
            error = string.Empty;
            if (ExecutionAddress.HasValue && ExecutionAddress.Value < 0) error = "Dirección de ejecución negativa.";
            return string.IsNullOrEmpty(error);
        }

        public static EndRecord Parse(string record)
        {
            if (record == null || record.Length < 1 || record[0] != 'E')
                throw new ArgumentException("Registro E inválido.");

            var rec = new EndRecord();
            if (record.Length >= 7)
            {
                string addrStr = record.Substring(1, 6).Trim();
                if (!string.IsNullOrWhiteSpace(addrStr) && addrStr != "FFFFFF")
                    rec.ExecutionAddress = int.Parse(addrStr, NumberStyles.HexNumber);
            }
            return rec;
        }
    }
}
