using System;
using System.Collections.Generic;
using System.Linq;

namespace laboratorioPractica3.Utils
{
    internal static class ObjectCodeUtils
    {
        // Construye una versión "display" del object code que contiene marcas legibles *SE y *R
        public static string BuildDisplayMarks(string cleanedObjectCode, IReadOnlyList<string> externalSymbols, bool hasInternalRelative)
        {
            if (string.IsNullOrEmpty(cleanedObjectCode))
                return cleanedObjectCode;

            var marks = new List<string>();
            if (externalSymbols != null && externalSymbols.Count > 0)
            {
                foreach (var s in externalSymbols.Distinct(StringComparer.OrdinalIgnoreCase))
                    marks.Add("*SE");
            }

            if (hasInternalRelative)
                marks.Add("*R");

            if (marks.Count == 0)
                return cleanedObjectCode;

            return cleanedObjectCode + string.Join(string.Empty, marks);
        }

        // Normaliza la versión display a la versión que será entregada al builder de módulos.
        // Reemplaza cada "*SE" o "*R" por un único asterisco '*' (o elimina si prefieres).
        public static string NormalizeMarksForModule(string displayMarked)
        {
            if (string.IsNullOrEmpty(displayMarked))
                return displayMarked;

            // Reemplazar etiquetas compuestas por *SE y *R por '*' para que el builder pueda
            // limpiar los '*' y dejar únicamente hex bytes.
            string normalized = displayMarked.Replace("*SE", "*").Replace("*R", "*");
            // En caso de que queden múltiples '*' consecutivos, compactarlos a uno solo.
            while (normalized.Contains("**"))
                normalized = normalized.Replace("**", "*");

            return normalized;
        }

        // Quitar todos los '*' usados como marcas
        public static string StripMarks(string input) => string.IsNullOrEmpty(input) ? input : input.Replace("*", string.Empty);

        // Elimina las marcas de display al final del código objeto.
        // Soporta tanto "*SE" / "*R" como variantes sin asterisco que hayan quedado por compatibilidad.
        public static string StripDisplayMarks(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            string result = input;

            while (true)
            {
                string previous = result;

                if (result.EndsWith("*SE", StringComparison.OrdinalIgnoreCase))
                    result = result[..^3];
                else if (result.EndsWith("*R", StringComparison.OrdinalIgnoreCase))
                    result = result[..^2];
                else if (result.EndsWith("SE", StringComparison.OrdinalIgnoreCase))
                    result = result[..^2];
                else if (result.EndsWith("R", StringComparison.OrdinalIgnoreCase))
                    result = result[..^1];

                if (result.EndsWith("*", StringComparison.OrdinalIgnoreCase))
                    result = result.TrimEnd('*');

                if (result == previous)
                    break;
            }

            return result;
        }
    }
}
