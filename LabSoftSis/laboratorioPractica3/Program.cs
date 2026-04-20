using Antlr4.Runtime;
using Antlr4.Runtime.Tree;
using laboratorioPractica3;
using System.Text;
using System.Windows.Forms;

/// <summary>
/// Punto de entrada del ensamblador SIC/XE.
/// Permite ejecutar analisis completo o ensamblado en dos pasadas.
/// </summary>
class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new Form1());
    }

    static void Main_old(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Determinar archivo de entrada y modo
        string inputFile;
        string mode = "0"; // 0 = preguntar, 1 = analisis completo, 2 = Paso 1
        
        if (args.Length > 0)
        {
            // Si se proporciona archivo por linea de comandos
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
            // Mostrar menu interactivo
            PrintHeader();
            ShowInteractiveMenu();
        }
    }

    /// <summary>
    /// Muestra un menu interactivo para seleccionar archivos
    /// </summary>
    static void ShowInteractiveMenu()
    {
        bool exit = false;

        while (!exit)
        {
            try
            {
                Console.Clear();
            }
            catch
            {
                // En algunos terminales, Clear() puede fallar
                Console.WriteLine("\n\n\n\n\n\n\n\n\n\n");
            }
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

            Console.WriteLine("MENU DE SELECCION DE ARCHIVOS");
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
            Console.Write("Seleccione una opcion (1-{0}): ", allFiles.Count + 1);

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
                    Console.WriteLine("Opcion invalida. Presione cualquier tecla para continuar...");
                    Console.ReadKey();
                }
            }
            else
            {
                Console.WriteLine();
                Console.WriteLine("Entrada invalida. Presione cualquier tecla para continuar...");
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
            Console.WriteLine("Seleccione el tipo de analisis:");
            Console.WriteLine("  1. Analisis completo (analisis semantico actual)");
            Console.WriteLine("  2. PASO 1 - Ensamblador (tabla de simbolos + direcciones + CSV)");
            Console.Write("\nOpcion (1-2): ");
            
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
            Console.WriteLine($"Modo seleccionado: {(option == "2" ? "PASO 1 - Ensamblador" : "Analisis completo")}");

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
                // Analisis completo existente
                var result = AnalyzeFile(inputFile);

                Console.WriteLine(result.Report);

                string outputFile = GenerateOutputFile(inputFile, result);
                Console.WriteLine();
                Console.WriteLine($"Archivo de reporte generado: {outputFile}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error durante el analisis: {ex.Message}");
            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Detalles: {ex.InnerException.Message}");
            }
        }
    }

    /// <summary>
    /// Ejecuta el flujo de ensamblado: Paso 1, Paso 2 y programa objeto.
    /// </summary>
    static void AnalyzePaso1(string inputFile)
    {
        // Método orquestador del ensamblado completo en dos pasadas.
        // Flujo: parseo ANTLR -> Paso 1 (TABSIM/intermedio) -> Paso 2 (código objeto)
        // -> generación de registros H/T/M/E -> exportación de reportes.
        Console.WriteLine("===============================================================");
        Console.WriteLine("            EJECUTANDO PASO 1 DEL ENSAMBLADOR SIC/XE");
        Console.WriteLine("===============================================================");
        Console.WriteLine();

        string input = File.ReadAllText(inputFile);
        
        // Guardar las lineas originales para procesar errores.
        // File.ReadAllLines mantiene correctamente cada linea del archivo fuente.
        string[] sourceLines = File.ReadAllLines(inputFile);
        
        if (!input.EndsWith("\n"))
            input += "\n";

        var inputStream = new AntlrInputStream(input);
        var lexer = new SICXELexer(inputStream);
        
        // Configurar listeners para capturar errores lexicos
        var lexerErrorListener = new SICXEErrorListener();
        lexer.RemoveErrorListeners();
        lexer.AddErrorListener(lexerErrorListener);
        
        var tokenStream = new CommonTokenStream(lexer);
        var parser = new SICXEParser(tokenStream);
        
        // Configurar listeners para capturar errores sintacticos
        var parserErrorListener = new SICXEErrorListener();
        parser.RemoveErrorListeners();
        parser.AddErrorListener(parserErrorListener);

        var tree = parser.program();
        
        // Combinar errores lexicos y sintacticos
        var allExternalErrors = lexerErrorListener.Errors.Concat(parserErrorListener.Errors).ToList();

        // Ejecutar Paso 1 (construccion de TABSIM y calculo de CONTLOC)
        var paso1 = new Paso1();
        
        // Pasar errores externos y lineas del codigo fuente al Paso 1
        paso1.AddExternalErrors(allExternalErrors);
        paso1.SetSourceLines(sourceLines);
        
        var walker = new ParseTreeWalker();
        walker.Walk(paso1, tree);
        
        // Tambien ejecutar analisis semantico para validar operandos
        var semanticAnalyzer = new SICXESemanticAnalyzer();
        semanticAnalyzer.AddExternalErrors(lexerErrorListener.Errors);
        semanticAnalyzer.AddExternalErrors(parserErrorListener.Errors);
        walker.Walk(semanticAnalyzer, tree);
        
        // Combinar errores solo de Paso 1 (se omiten los del analizador semantico por ahora)
        var allErrors = paso1.ErrorList
            .GroupBy(e => new { e.Line, e.Message })  // Evitar duplicados
            .Select(g => g.First())
            .OrderBy(e => e.Line)
            .ThenBy(e => e.Column)
            .ToList();

        // ═══════════════ EJECUTAR PASO 2 ═══════════════
        var paso2 = new Paso2(
            paso1.Lines,
            paso1.SymbolTableExtended,
            paso1.ProgramStartAddress,
            paso1.ProgramSize,
            paso1.ProgramName,
            paso1.BaseValue);

        // Paso 2: resuelvedireccionamiento y genera código objeto por línea.
        paso2.ObjectCodeGeneration();

        // ═══════════════ MOSTRAR REPORTE COMBINADO ═══════════════
        // Se muestra UNA sola vez el archivo intermedio con TABSIM, COD_OBJ y errores fusionados
        var objectCodes = paso2.ObjectCodeLines
            .Where(l => !string.IsNullOrEmpty(l.ObjectCode))
            .ToDictionary(l => l.IntermLine.LineNumber, l => l.ObjectCode);

        // Unificar errores: Paso 1 + Paso 2 (se omiten los del analizador semantico)
        var erroresUnificados = paso1.ErrorList
            .Concat(paso2.Errors)
            .GroupBy(e => new { e.Line, e.Message })
            .Select(g => g.First())
            .OrderBy(e => e.Line)
            .ThenBy(e => e.Column)
            .ToList();

        Console.WriteLine(paso1.GenerateReport(objectCodes, erroresUnificados));

        // Nota: el reporte detallado del Paso 2 se omite para evitar duplicar el archivo intermedio

        string projectDir = GetProjectDirectory();
        string reportesDir = Path.Combine(projectDir, "reportes_paso1");
        
        if (!Directory.Exists(reportesDir))
            Directory.CreateDirectory(reportesDir);

        string baseName = Path.GetFileNameWithoutExtension(inputFile);
        string baseOutputPath = Path.Combine(reportesDir, baseName);

        // Exportar con todos los errores combinados
        ExportPaso1WithValidation(paso1, erroresUnificados, baseOutputPath);

        // Exportar CSV del Paso 2
        string reportesPaso2Dir = Path.Combine(projectDir, "reportes_paso2");
        if (!Directory.Exists(reportesPaso2Dir))
            Directory.CreateDirectory(reportesPaso2Dir);

        string timestamp2 = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string csvPaso2Path = Path.Combine(reportesPaso2Dir, $"{baseName}_PASO2_{timestamp2}.csv");
        paso2.ExportToCSV(csvPaso2Path);
        Console.WriteLine($"  - CSV Paso 2: {Path.GetFileName(csvPaso2Path)}");

        // --------------- EJECUTAR PROGRAMA OBJETO ---------------
        var progObjeto = new ProgramaObjeto(
            paso2.ObjectCodeLines,
            paso1.ProgramName,
            paso1.ProgramStartAddress,
            paso1.ProgramSize,
            paso1.ExecutionEntryPoint);

        // Programa objeto final: registros H/T/M/E según reglas SIC/XE.

        Console.WriteLine("\n-------------------------------------------------------------------");
        Console.WriteLine("                    PROGRAMA OBJETO GENERADO");
        Console.WriteLine("-------------------------------------------------------------------");
        var objRecords = progObjeto.GenerarRegistros();
        const int maxConsoleObjectRecords = 100;
        int recordsToPrint = Math.Min(maxConsoleObjectRecords, objRecords.Count);

        for (int i = 0; i < recordsToPrint; i++)
        {
            Console.WriteLine(objRecords[i]);
        }

        if (objRecords.Count > maxConsoleObjectRecords)
        {
            Console.WriteLine($"... ({objRecords.Count - maxConsoleObjectRecords} registros adicionales omitidos en consola)");
        }

        string reportesObjDir = Path.Combine(projectDir, "reportes_objeto");
        if (!Directory.Exists(reportesObjDir))
            Directory.CreateDirectory(reportesObjDir);

        string csvObjPath = Path.Combine(reportesObjDir, $"{baseName}_OBJETO_{timestamp2}.csv");
        progObjeto.ExportarACSV(csvObjPath);
        Console.WriteLine($"  - Excel/CSV Programa Objeto guardado en: {Path.GetFileName(csvObjPath)}");

        Console.WriteLine($"\nDirectorio de salida Paso 1: {reportesDir}");
        Console.WriteLine($"Directorio de salida Paso 2: {reportesPaso2Dir}");
        Console.WriteLine($"Directorio de salida Programa Objeto: {reportesObjDir}");
        
        if (erroresUnificados.Count == 0)
        {
            Console.WriteLine("\nPaso 1 completado exitosamente sin errores!");
        }
        else
        {
            Console.WriteLine($"\nPaso 1 completado con {erroresUnificados.Count} error(es) detectado(s)");
        }
    }
    
    /// <summary>
    /// Exporta archivo CSV del Paso 1 con validaciones integradas
    /// </summary>
    static void ExportPaso1WithValidation(Paso1 paso1, List<SICXEError> allErrors, string baseOutputPath)
    {
        string directory = Path.GetDirectoryName(baseOutputPath) ?? ".";
        string baseName = Path.GetFileNameWithoutExtension(baseOutputPath);
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Generar UN SOLO archivo CSV con TABSIM + Archivo Intermedio
        string csvPath = Path.Combine(directory, $"{baseName}_PASO1_{timestamp}.csv");
        paso1.ExportToSingleCSV(csvPath, allErrors);

        Console.WriteLine("Archivo generado:");
        Console.WriteLine($"  - CSV: {Path.GetFileName(csvPath)}");
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
        Console.WriteLine("===============================================================");
        Console.WriteLine("   ANALIZADOR DE LENGUAJE ENSAMBLADOR SIC/XE");
        Console.WriteLine("   Laboratorio Practica 3");
        Console.WriteLine("   Fundamentos de Software de Sistemas");
        Console.WriteLine("===============================================================");
        Console.WriteLine();
    }

    /// <summary>
    /// Obtiene el directorio raiz del proyecto
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
        // Análisis "completo" (léxico/sintáctico/semántico) usado fuera del modo Paso 1.
        // No genera objeto; se usa para reporte general del archivo fuente.
        // Leer contenido del archivo
        string input = File.ReadAllText(filePath);
        
        // Asegurar que el archivo termine con nueva linea
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

        // Crear el analizador semantico
        var semanticAnalyzer = new SICXESemanticAnalyzer();
        
        // Procesar tokens para el reporte
        semanticAnalyzer.ProcessTokens(tokenStream, lexer);
        
        // Agregar errores lexicos y sintacticos
        semanticAnalyzer.AddExternalErrors(lexerErrorListener.Errors);
        semanticAnalyzer.AddExternalErrors(parserErrorListener.Errors);

        // Recorrer el arbol para analisis semantico
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

        // Agregar seccion detallada de errores con numero de linea
        if (result.Errors.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("DETALLE DE ERRORES POR LINEA:");
            


            foreach (var error in result.Errors.OrderBy(e => e.Line))
            {
                sb.AppendLine($"Linea {error.Line}: {error.Message}");
            }
        }

        // Agregar arbol sintactico
        sb.AppendLine();
        sb.AppendLine("ARBOL SINTACTICO:");
        sb.AppendLine(FormatParseTree(result.ParseTree));
    

        File.WriteAllText(outputFile, sb.ToString(), Encoding.UTF8);

        return outputFile;
    }

    /// <summary>
    /// Formatea el arbol sintactico para mejor visualizacion
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
/// Resultado del analisis
/// </summary>
class AnalysisResult
{
    public string Report { get; set; } = string.Empty;
    public List<SICXEError> Errors { get; set; } = new List<SICXEError>();
    public List<TokenInfo> Tokens { get; set; } = new List<TokenInfo>();
    public string ParseTree { get; set; } = string.Empty;
}
