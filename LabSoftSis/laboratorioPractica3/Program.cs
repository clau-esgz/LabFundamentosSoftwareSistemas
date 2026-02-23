using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using laboratorioPractica3;
using System.Text;

/// <summary>
/// Analizador de Lenguaje Ensamblador SIC/XE
/// Laboratorio Práctica 3 - Fundamentos de Software de Sistemas
/// </summary>
class Program
{
    static void Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        PrintHeader();

        // Determinar archivo de entrada y modo
        string inputFile;
        string mode = "0"; // 0 = preguntar, 1 = análisis completo, 2 = Paso 1
        
        if (args.Length > 0)
        {
            // Si se proporciona archivo por línea de comandos
            inputFile = args[0];
            
            // Si se proporciona el modo como segundo argumento
            if (args.Length > 1)
            {
                mode = args[1];
            }
            
            if (!File.Exists(inputFile))
            {
                Console.WriteLine($"Error: El archivo '{inputFile}' no existe.");
                return;
            }
            
            // Analizar el archivo proporcionado
            AnalyzeAndShow(inputFile, mode);
        }
        else
        {
            // Mostrar menú interactivo
            ShowInteractiveMenu();
        }
    }

    /// <summary>
    /// Muestra un menú interactivo para seleccionar archivos
    /// </summary>
    static void ShowInteractiveMenu()
    {
        bool exit = false;

        while (!exit)
        {
            Console.Clear();
            PrintHeader();

            // Obtener archivos .asm y .txt del directorio del proyecto
            var searchDir = GetProjectDirectory();
            var asmFiles = Directory.GetFiles(searchDir, "*.asm").Select(f => Path.GetFileName(f)).ToList();
            var txtFiles = Directory.GetFiles(searchDir, "*.txt").Select(f => Path.GetFileName(f)).ToList();
            var allFiles = new List<string>();
            allFiles.AddRange(asmFiles);
            allFiles.AddRange(txtFiles);
            allFiles.Sort();

            if (allFiles.Count == 0)
            {
                Console.WriteLine("No se encontraron archivos .asm o .txt en el directorio del proyecto.");
                Console.WriteLine($"Directorio buscado: {searchDir}");
                Console.WriteLine();
                Console.WriteLine("Presione cualquier tecla para salir...");
                Console.ReadKey();
                return;
            }

            Console.WriteLine("MENÚ DE SELECCIÓN DE ARCHIVOS");
            Console.WriteLine($"Directorio: {searchDir}");
            Console.WriteLine();
            Console.WriteLine("Archivos disponibles:");
            Console.WriteLine();

            // Mostrar archivos numerados
            for (int i = 0; i < allFiles.Count; i++)
            {
                string ext = allFiles[i].EndsWith(".asm") ? "[ASM]" : "[TXT]";
                Console.WriteLine($"  {i + 1,2}. {ext} {allFiles[i]}");
            }

            Console.WriteLine();
            Console.WriteLine($"  {allFiles.Count + 1,2}. [X] Salir");
            Console.WriteLine();
            Console.Write("Seleccione una opción (1-{0}): ", allFiles.Count + 1);

            string? input = Console.ReadLine();
            
            if (int.TryParse(input, out int option))
            {
                if (option >= 1 && option <= allFiles.Count)
                {
                    // Analizar el archivo seleccionado
                    string selectedFile = Path.Combine(searchDir, allFiles[option - 1]);
                    Console.WriteLine();
                    AnalyzeAndShow(selectedFile);
                    
                    Console.WriteLine();
                    Console.WriteLine("Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                }
                else if (option == allFiles.Count + 1)
                {
                    // Salir
                    exit = true;
                    Console.WriteLine();
                }
                else
                {
                    Console.WriteLine();
                    Console.WriteLine("Opción inválida. Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Entrada inválida. Presione cualquier tecla para continuar...");
                Console.ReadKey();
            }
        }
    }

    /// <summary>
    /// Analiza un archivo y muestra el resultado
    /// </summary>
    static void AnalyzeAndShow(string inputFile, string mode = "0")
    {
        Console.WriteLine($"Analizando archivo: {Path.GetFileName(inputFile)}");
        Console.WriteLine();
        
        string option = mode;
        
        if (option == "0")
        {
            Console.WriteLine("Seleccione el tipo de análisis:");
            Console.WriteLine("  1. Análisis completo (análisis semántico actual)");
            Console.WriteLine("  2. PASO 1 - Ensamblador (tabla de símbolos + direcciones + CSV)");
            Console.Write("\nOpción (1-2): ");
            
            if (Console.IsInputRedirected)
            {
                option = Console.ReadLine() ?? "1";
            }
            else
            {
                option = Console.ReadKey().KeyChar.ToString();
            }
            Console.WriteLine();
            Console.WriteLine();
        }
        else
        {
            Console.WriteLine($"Modo seleccionado: {(option == "2" ? "PASO 1 - Ensamblador" : "Análisis completo")}");
            Console.WriteLine();
        }

        try
        {
            if (option == "2")
            {
                // Ejecutar Paso 1 del ensamblador
                AnalyzePaso1(inputFile);
            }
            else
            {
                // Análisis completo existente
                var result = AnalyzeFile(inputFile);

                Console.WriteLine(result.Report);

                Console.WriteLine();
                Console.WriteLine("ARBOL SINTACTICO:");
                Console.WriteLine(FormatParseTree(result.ParseTree));

                string outputFile = GenerateOutputFile(inputFile, result);
                Console.WriteLine();
                Console.WriteLine($"Archivo de reporte generado: {outputFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error durante el análisis: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Detalles: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>
    /// Ejecuta el Paso 1 del ensamblador SIC/XE
    /// </summary>
    static void AnalyzePaso1(string inputFile)
    {
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine("               EJECUTANDO PASO 1 DEL ENSAMBLADOR");
        Console.WriteLine("═══════════════════════════════════════════════════════════════════");
        Console.WriteLine();

        string input = File.ReadAllText(inputFile);
        
        if (!input.EndsWith("\n"))
            input += "\n";

        var inputStream = new AntlrInputStream(input);
        var lexer = new SICXELexer(inputStream);
        
        // Configurar error listeners para capturar errores léxicos
        var lexerErrorListener = new SICXEErrorListener();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);
        
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new SICXEParser(tokenStream);
        
        // Configurar error listeners para capturar errores sintácticos
        var parserErrorListener = new SICXEErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrorListener);

        var tree = parser.program();

        // Ejecutar Paso 1 (construcción de TABSIM y cálculo de CONTLOC)
        var paso1 = new Paso1();
        var walker = new ParseTreeWalker();
        walker.Walk(paso1, tree);
        
        // TAMBIÉN ejecutar análisis semántico para validar operandos
        var semanticAnalyzer = new SICXESemanticAnalyzer();
        semanticAnalyzer.AddExternalErrors(lexerErrorListener.Errors);
        semanticAnalyzer.AddExternalErrors(parserErrorListener.Errors);
        walker.Walk(semanticAnalyzer, tree);
        
        // Combinar errores de ambas fuentes
        var allErrors = paso1.ErrorList
            .Concat(semanticAnalyzer.Errors)
            .GroupBy(e => new { e.Line, e.Message })  // Evitar duplicados
            .Select(g => g.First())
            .OrderBy(e => e.Line)
            .ThenBy(e => e.Column)
            .ToList();

        Console.WriteLine(paso1.GenerateReport());
        
        // Mostrar errores semánticos adicionales si los hay
        var additionalErrors = semanticAnalyzer.Errors
            .Where(e => !paso1.ErrorList.Any(p => p.Line == e.Line && p.Message == e.Message))
            .ToList();
            
        if (additionalErrors.Count > 0)
        {
            Console.WriteLine("═══════════════════ ERRORES DE VALIDACIÓN ADICIONALES ═══════════");
            foreach (var error in additionalErrors.OrderBy(e => e.Line))
            {
                Console.WriteLine($"  • {error}");
            }
            Console.WriteLine();
        }

        string projectDir = GetProjectDirectory();
        string reportesDir = Path.Combine(projectDir, "reportes_paso1");
        
        if (!Directory.Exists(reportesDir))
            Directory.CreateDirectory(reportesDir);

        string baseName = Path.GetFileNameWithoutExtension(inputFile);
        string baseOutputPath = Path.Combine(reportesDir, baseName);

        // Exportar con todos los errores combinados
        ExportPaso1WithValidation(paso1, allErrors, baseOutputPath);
        
        Console.WriteLine($"\n📂 Directorio de salida: {reportesDir}");
        
        if (allErrors.Count == 0)
        {
            Console.WriteLine("\n✅ Paso 1 completado exitosamente sin errores!");
        }
        else
        {
            Console.WriteLine($"\n⚠️ Paso 1 completado con {allErrors.Count} error(es) detectado(s)");
        }
    }
    
    /// <summary>
    /// Exporta archivos CSV del Paso 1 con validaciones integradas
    /// </summary>
    static void ExportPaso1WithValidation(Paso1 paso1, List<SICXEError> allErrors, string baseOutputPath)
    {
        string directory = Path.GetDirectoryName(baseOutputPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(baseOutputPath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        string symtabPath = Path.Combine(directory, $"{baseName}_SYMTAB_{timestamp}.csv");
        string intermediatePath = Path.Combine(directory, $"{baseName}_LISTADO_{timestamp}.csv");
        string summaryPath = Path.Combine(directory, $"{baseName}_RESUMEN_{timestamp}.csv");

        // Exportar SYMTAB
        paso1.ExportSymbolTableToCSV(symtabPath);
        
        // Exportar LISTADO con errores integrados
        var sb = new StringBuilder();
        sb.AppendLine("NL,CONTLOC_HEX,CONTLOC_DEC,ETQ,CODOP,OPR,FMT,MOD,ERR,COMENTARIO");
        
        foreach (var line in paso1.Lines)
        {
            string addressHex = (line.Address >= 0) ? $"{line.Address:X4}" : "";
            string addressDec = (line.Address >= 0) ? $"{line.Address}" : "";
            string fmt = (line.Format > 0) ? $"{line.Format}" : "";
            
            // Buscar TODOS los errores para esta línea
            var lineErrors = allErrors.Where(e => e.Line == line.LineNumber);
            string errorMsg = "";
            
            if (lineErrors.Any())
            {
                errorMsg = string.Join("; ", lineErrors.Select(e => e.Message));
            }
            else if (!string.IsNullOrEmpty(line.Error))
            {
                errorMsg = line.Error;
            }
            
            sb.AppendLine($"{line.LineNumber},{addressHex},{addressDec},{EscapeCSV(line.Label)},{EscapeCSV(line.Operation)},{EscapeCSV(line.Operand)},{fmt},{EscapeCSV(line.AddressingMode)},{EscapeCSV(errorMsg)},{EscapeCSV(line.Comment)}");
        }
        
        File.WriteAllText(intermediatePath, sb.ToString(), Encoding.UTF8);
        
        // Exportar RESUMEN con conteo de errores completo
        var sbSummary = new StringBuilder();
        sbSummary.AppendLine("PROPIEDAD,VALOR_HEX,VALOR_DEC");
        sbSummary.AppendLine($"NOMBRE_PROGRAMA,{paso1.ProgramName},{paso1.ProgramName}");
        sbSummary.AppendLine($"DIRECCION_INICIO,{paso1.ProgramStartAddress:X4},{paso1.ProgramStartAddress}");
        sbSummary.AppendLine($"LONGITUD_PROGRAMA,{paso1.ProgramSize:X4},{paso1.ProgramSize}");
        sbSummary.AppendLine($"TOTAL_SIMBOLOS,,{paso1.SymbolTable.Count}");
        sbSummary.AppendLine($"TOTAL_LINEAS,,{paso1.Lines.Count}");
        sbSummary.AppendLine($"TOTAL_ERRORES,,{allErrors.Count}");
        if (paso1.BaseValue.HasValue)
            sbSummary.AppendLine($"VALOR_BASE,{paso1.BaseValue.Value:X4},{paso1.BaseValue.Value}");
        
        File.WriteAllText(summaryPath, sbSummary.ToString(), Encoding.UTF8);

        Console.WriteLine("📁 Archivos CSV generados:");
        Console.WriteLine($"  ✓ Tabla de símbolos: {Path.GetFileName(symtabPath)}");
        Console.WriteLine($"  ✓ Listado intermedio: {Path.GetFileName(intermediatePath)}");
        Console.WriteLine($"  ✓ Resumen: {Path.GetFileName(summaryPath)}");
    }
    
    /// <summary>
    /// Escapa valores para formato CSV
    /// </summary>
    static string EscapeCSV(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "";

        if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
        {
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        return value;
    }

    /// <summary>
    /// Imprime el encabezado del programa
    /// </summary>
    static void PrintHeader()
    {
        Console.WriteLine();
        Console.WriteLine("╔═══════════════════════════════════════════════════════════════════╗");
        Console.WriteLine("║        ANALIZADOR DE LENGUAJE ENSAMBLADOR SIC/XE                  ║");
        Console.WriteLine("║        Laboratorio Práctica 3                                     ║");
        Console.WriteLine("║        Fundamentos de Software de Sistemas                        ║");
        Console.WriteLine("╚═══════════════════════════════════════════════════════════════════╝");
        Console.WriteLine();
    }

    /// <summary>
    /// Obtiene el directorio raíz del proyecto
    /// Busca hacia arriba desde el directorio actual hasta encontrar el archivo .csproj
    /// </summary>
    static string GetProjectDirectory()
    {
        var currentDir = Directory.GetCurrentDirectory();
        var dir = new DirectoryInfo(currentDir);

        // Buscar hacia arriba hasta encontrar el archivo .csproj
        while (dir != null)
        {
            if (dir.GetFiles("*.csproj").Length > 0)
            {
                return dir.FullName;
            }
            dir = dir.Parent;
        }

        // Si no se encuentra, usar el directorio actual
        return currentDir;
    }

    /// <summary>
    /// Analiza un archivo SIC/XE y retorna el resultado
    /// </summary>
    static AnalysisResult AnalyzeFile(string filePath)
    {
        // Leer contenido del archivo
        string input = File.ReadAllText(filePath);
        
        // Asegurar que el archivo termine con nueva línea
        if (!input.EndsWith("\n"))
        {
            input += "\n";
        }

        // Crear el input stream
        var inputStream = new AntlrInputStream(input);

        // Crear el lexer
        var lexer = new SICXELexer(inputStream);
        
        // Configurar error listener para el lexer
        var lexerErrorListener = new SICXEErrorListener();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);

        // Crear el token stream
        var tokenStream = new CommonTokenStream(lexer);

        // Crear el parser
        var parser = new SICXEParser(tokenStream);
        
        // Configurar error listener para el parser
        var parserErrorListener = new SICXEErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrorListener);

        // Parsear el programa
        var tree = parser.program();

        // Crear el analizador semántico
        var semanticAnalyzer = new SICXESemanticAnalyzer();
        
        // Procesar tokens para el reporte
        semanticAnalyzer.ProcessTokens(tokenStream, lexer);
        
        // Agregar errores léxicos y sintácticos
        semanticAnalyzer.AddExternalErrors(lexerErrorListener.Errors);
        semanticAnalyzer.AddExternalErrors(parserErrorListener.Errors);

        // Recorrer el árbol para análisis semántico
        var walker = new ParseTreeWalker();
        walker.Walk(semanticAnalyzer, tree);

        // Generar reporte
        string report = semanticAnalyzer.GenerateReport(Path.GetFileName(filePath), verbose: true);

        return new AnalysisResult
        {
            Report = report,
            Errors = semanticAnalyzer.Errors.ToList(),
            Tokens = semanticAnalyzer.Tokens.ToList(),
            ParseTree = tree.ToStringTree(parser)
        };
    }

    /// <summary>
    /// Genera el archivo de salida con el reporte
    /// </summary>
    static string GenerateOutputFile(string inputFile, AnalysisResult result)
    {
        // Crear carpeta de reportes si no existe
        string projectDir = GetProjectDirectory();
        string reportesDir = Path.Combine(projectDir, "reportes");

        if (!Directory.Exists(reportesDir))
        {
            Directory.CreateDirectory(reportesDir);
        }

        // Generar nombre del archivo de reporte
        string baseName = Path.GetFileNameWithoutExtension(inputFile);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string outputFile = Path.Combine(reportesDir, $"{baseName}_reporte_{timestamp}.txt");

        var sb = new StringBuilder();

        // Agregar reporte principal
        sb.AppendLine(result.Report);

        // Agregar sección detallada de errores con número de línea
        if (result.Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DETALLE DE ERRORES POR LINEA:");
            

            foreach (var error in result.Errors.OrderBy(e => e.Line))
            {
                sb.AppendLine($"Linea {error.Line}: {error.Message}");
            }
        }

        // Agregar árbol sintáctico
        sb.AppendLine();
        sb.AppendLine("ARBOL SINTACTICO:");
        sb.AppendLine(FormatParseTree(result.ParseTree));
    

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);

        return outputFile;
    }

    /// <summary>
    /// Formatea el árbol sintáctico para mejor visualización
    /// </summary>
    static string FormatParseTree(string tree)
    {
        var sb = new StringBuilder();
        int indent = 0;
        
        foreach (char c in tree)
        {
            if (c == '(')
            {
                sb.AppendLine();
                sb.Append(new string(' ', indent * 2));
                sb.Append(c);
                indent++;
            }
            else if (c == ')')
            {
                indent--;
                sb.Append(c);
            }
            else
            {
                sb.Append(c);
            }
        }
        
        return sb.ToString();
    }
}

/// <summary>
/// Resultado del análisis
/// </summary>
class AnalysisResult
{
    public string Report { get; set; } = string.Empty;
    public List<SICXEError> Errors { get; set; } = new List<SICXEError>();
    public List<TokenInfo> Tokens { get; set; } = new List<TokenInfo>();
    public string ParseTree { get; set; } = string.Empty;
}
