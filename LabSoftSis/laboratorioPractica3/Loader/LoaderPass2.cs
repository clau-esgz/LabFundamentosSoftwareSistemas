using System;
using System.Collections.Generic;
using System.Globalization;

namespace laboratorioPractica3.Loader
{
    /// <summary>
    /// Pass 2 del cargador-ligador SIC/XE.
    /// Carga T records en memoria, aplica M records y calcula DIREJ.
    /// </summary>
    public class LoaderPass2
    {
        public int Execute(
            Pass1Data pass1,
            List<ObjectModuleParsed> modules,
            ExternalSymbolTable tabse,
            MemoryManager memoryManager,
            List<LoaderError> errors)
        {
            int executionEntryPoint = 0;

            foreach (var module in modules)
            {
                if (!pass1.SectionLoadAddresses.TryGetValue(module.Name, out int dirSc))
                {
                    errors.Add(new LoaderError
                    {
                        Message = $"No existe dirección de carga para sección: {module.Name}",
                        Type = LoaderError.ErrorType.InvalidRecord
                    });
                    continue;
                }

                if (!memoryManager.AllocateMemory(dirSc, module.Length, module.Name, out var allocError))
                {
                    errors.Add(allocError!);
                    continue;
                }

                foreach (var (tAddr, hexBytes) in module.TextRecords)
                {
                    int absoluteAddress = dirSc + tAddr;
                    if (!memoryManager.WriteHex(absoluteAddress, hexBytes, out var writeError))
                        errors.Add(writeError!);
                }

                foreach (var modRecord in module.ModificationRecords)
                {
                    int absoluteAddress = dirSc + modRecord.Address;
                    if (!tabse.TryGet(modRecord.Symbol, out var symbolEntry) || symbolEntry == null)
                    {
                        errors.Add(new LoaderError
                        {
                            Message = $"Símbolo externo indefinido: {modRecord.Symbol}",
                            Type = LoaderError.ErrorType.UndefinedSymbol
                        });
                        continue;
                    }

                    if (!memoryManager.ModifyMemory(
                        absoluteAddress,
                        modRecord.HalfBytesLength,
                        modRecord.Sign,
                        symbolEntry.Address,
                        out var modifyError))
                    {
                        errors.Add(modifyError!);
                    }
                }

                if (module.ExecutionAddress.HasValue)
                    executionEntryPoint = dirSc + module.ExecutionAddress.Value;
            }

            return executionEntryPoint;
        }

        public static byte[] ConvertHexStringToBytes(string hexString)
        {
            hexString = (hexString ?? string.Empty).Replace(" ", string.Empty);
            if (hexString.Length % 2 != 0)
                hexString = "0" + hexString;

            var bytes = new byte[hexString.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
                bytes[i] = byte.Parse(hexString.Substring(i * 2, 2), NumberStyles.HexNumber);

            return bytes;
        }
    }
}
