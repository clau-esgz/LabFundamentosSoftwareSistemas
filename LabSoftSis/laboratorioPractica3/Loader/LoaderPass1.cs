using System.Collections.Generic;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Pass 1 del cargador-ligador SIC/XE.
    /// Construye TABSE y asigna dirección absoluta por sección de control.
    /// </summary>
    public class LoaderPass1
    {
        public Pass1Data Execute(List<ObjectModuleParsed> modules, int initialLoadAddress, ExternalSymbolTable tabse)
        {
            var pass1 = new Pass1Data { InitialLoadAddress = initialLoadAddress };

            int dirSc = initialLoadAddress; // DIRSC actual

            foreach (var module in modules)
            {
                pass1.SectionLoadAddresses[module.Name] = dirSc;

                // Registrar el nombre de la sección como símbolo de control (útil para M +/- sección).
                if (!tabse.TryAdd(module.Name, dirSc, module.Name, out var sectionError))
                    pass1.Errors.Add(sectionError!);

                foreach (var (symbol, relativeAddress) in module.Definitions)
                {
                    int absoluteAddress = dirSc + relativeAddress;
                    if (!tabse.TryAdd(symbol, absoluteAddress, module.Name, out var error))
                        pass1.Errors.Add(error!);
                }

                dirSc += module.Length; // LONSC
            }

            foreach (var kvp in tabse.Entries)
                pass1.SymbolTable[kvp.Key] = kvp.Value;

            return pass1;
        }
    }
}
