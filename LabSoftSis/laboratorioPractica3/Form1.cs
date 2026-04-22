using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Antlr4.Runtime;
using Antlr4.Runtime.Tree;

namespace laboratorioPractica3
{
    public partial class Form1 : Form
    {
        private string? _currentFilePath;
        private bool _isDirty;
        private bool _consoleReady;
        private bool _isHighlighting;
        private System.Windows.Forms.Timer? _highlightTimer;

        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern nint GetConsoleWindow();

        public Form1()
        {
            InitializeComponent();
        }

        private void nuevoToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void nUEVOToolStripMenuItem_Click_1(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {
            codigoTextBox1.AcceptsTab = true;
            codigoTextBox1.WordWrap = false;
            codigoTextBox1.Font = new Font("Consolas", 10F);

            RegistrostextBox2.ReadOnly = true;
            RegistrostextBox2.Font = new Font("Consolas", 9F);

            ErrortextBox3.ReadOnly = true;
            ErrortextBox3.Font = new Font("Consolas", 9F);

            ArchivoInterdataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            ArchivoInterdataGridView1.AllowUserToAddRows = false;
            ArchivoInterdataGridView1.AllowUserToDeleteRows = false;
            ArchivoInterdataGridView1.ReadOnly = true;
            ArchivoInterdataGridView1.RowHeadersVisible = true;
            ArchivoInterdataGridView1.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            ArchivoInterdataGridView1.RowHeadersWidth = 40;

            TablaSimdataGridView2.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            TablaSimdataGridView2.AllowUserToAddRows = false;
            TablaSimdataGridView2.AllowUserToDeleteRows = false;
            TablaSimdataGridView2.ReadOnly = true;

            tablaBlqsGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            tablaBlqsGridView1.AllowUserToAddRows = false;
            tablaBlqsGridView1.AllowUserToDeleteRows = false;
            tablaBlqsGridView1.ReadOnly = true;

            codigoTextBox1.TextChanged += (_, __) =>
            {
                _isDirty = true;
                ActualizarTitulo();
                ProgramarResaltado();
            };

            _highlightTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _highlightTimer.Tick += (_, __) =>
            {
                _highlightTimer!.Stop();
                AplicarResaltadoSintaxis();
            };

            NuevoArchivo();
        }

        private void analizarlexicoYSintacticoToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void txtToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void guardarToolStripMenuItem_Click(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox3_Enter(object sender, EventArgs e)
        {

        }

        private void groupBox5_Enter(object sender, EventArgs e)
        {

        }

        private void textBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void Nuevo_Click(object sender, EventArgs e)
        {
            NuevoArchivo();
        }

        private void Abrir_Click(object sender, EventArgs e)
        {
            AbrirArchivo();
        }

        private void Guardar_Click(object sender, EventArgs e)
        {
            GuardarArchivo();
        }

        private void GuardarComoMenu_Click(object sender, EventArgs e)
        {
            GuardarArchivoComo();
        }

        private void salirToolStripMenuItem_Click(object sender, EventArgs e)
        {
            CerrarAplicacion();
        }

        private void toolStripMenuItem1_Click(object sender, EventArgs e)
        {
            EjecutarAnalizadorLexicoSintactico();
        }

        private void toolStripMenuItem2_Click(object sender, EventArgs e)
        {
            EjecutarPaso1();
        }

        private void toolStripMenuItem3_Click(object sender, EventArgs e)
        {
            EjecutarPaso2();
        }

        private void toolStripMenuItem4_Click(object sender, EventArgs e)
        {
            EnsamblarTodo();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            if (!ConfirmarGuardarCambiosAntesDeContinuar())
            {
                e.Cancel = true;
                return;
            }

            base.OnFormClosing(e);
        }

        private void NuevoArchivo()
        {
            if (!ConfirmarGuardarCambiosAntesDeContinuar())
                return;

            _currentFilePath = null;
            _isDirty = false;

            codigoTextBox1.Clear();
            ErrortextBox3.Clear();
            RegistrostextBox2.Clear();
            ArchivoInterdataGridView1.DataSource = null;
            TablaSimdataGridView2.DataSource = null;
            tablaBlqsGridView1.DataSource = null;
            ActualizarTitulo();
            AplicarResaltadoSintaxis();
        }

        private void AbrirArchivo()
        {
            if (!ConfirmarGuardarCambiosAntesDeContinuar())
                return;

            using var ofd = new OpenFileDialog
            {
                Title = "Abrir programa fuente SIC/XE",
                Filter = "Archivos SIC/XE (*.asm;*.txt)|*.asm;*.txt|Todos los archivos (*.*)|*.*",
                Multiselect = false
            };

            if (ofd.ShowDialog(this) != DialogResult.OK)
                return;

            codigoTextBox1.Text = File.ReadAllText(ofd.FileName);
            _currentFilePath = ofd.FileName;
            _isDirty = false;
            ActualizarTitulo();
            AplicarResaltadoSintaxis();
        }

        private bool GuardarArchivo()
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
                return GuardarArchivoComo();

            File.WriteAllText(_currentFilePath!, codigoTextBox1.Text);
            _isDirty = false;
            ActualizarTitulo();
            return true;
        }

        private bool GuardarArchivoComo()
        {
            using var sfd = new SaveFileDialog
            {
                Title = "Guardar programa fuente SIC/XE",
                Filter = "Archivo ASM (*.asm)|*.asm|Archivo TXT (*.txt)|*.txt|Todos los archivos (*.*)|*.*",
                FileName = string.IsNullOrWhiteSpace(_currentFilePath) ? "programa.asm" : Path.GetFileName(_currentFilePath)
            };

            if (sfd.ShowDialog(this) != DialogResult.OK)
                return false;

            _currentFilePath = sfd.FileName;
            File.WriteAllText(_currentFilePath, codigoTextBox1.Text);

            _isDirty = false;
            ActualizarTitulo();
            return true;
        }

        private void CerrarAplicacion()
        {
            if (!ConfirmarGuardarCambiosAntesDeContinuar())
                return;

            Close();
        }

        private bool ConfirmarGuardarCambiosAntesDeContinuar()
        {
            if (!_isDirty)
                return true;

            var result = MessageBox.Show(
                this,
                "Hay cambios sin guardar. ¿Deseas guardarlos antes de continuar?",
                "Cambios pendientes",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.Cancel)
                return false;

            if (result == DialogResult.Yes)
                return GuardarArchivo();

            return true;
        }

        private void ActualizarTitulo()
        {
            string nombre = string.IsNullOrWhiteSpace(_currentFilePath) ? "(sin nombre)" : Path.GetFileName(_currentFilePath);
            string sucio = _isDirty ? " *" : "";
            Text = $"ENSAMBLARODR SICXE - {nombre}{sucio}";
        }

        private void EjecutarAnalizadorLexicoSintactico()
        {
            try
            {
                var parse = PrepararParse();
                var errores = parse.ErroresLexicoSintacticos
                    .OrderBy(e => e.Line)
                    .ThenBy(e => e.Column)
                    .ToList();

                MostrarErroresAnalizador(errores);

                string baseName = ObtenerNombreBaseFuente();
                string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes");
                Directory.CreateDirectory(reportesDir);
                string filePath = Path.Combine(reportesDir, $"{baseName}_ERRORES_SINTACTICOS_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
                File.WriteAllText(filePath, ConstruirContenidoErrores(errores, "ANALIZADOR LÉXICO-SINTÁCTICO"));

                MostrarSalidaAnalizadorEnConsola(errores, filePath);

                MessageBox.Show(this, $"Análisis completado.\nArchivo de errores: {filePath}", "Analizador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                EscribirLineaConsola($"[ERROR Analizador] {ex.Message}");
                MessageBox.Show(this, $"Error al ejecutar analizador: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EjecutarPaso1()
        {
            try
            {
                var resultado = EjecutarPipeline(runPaso2: false);
                RefrescarVista(resultado, incluirCodigoObjeto: false);
                ExportarSalidaPaso1(resultado);
                MostrarSalidaPipelineEnConsola("PASO 1 SIC/XE", resultado, incluirPaso2: false);
                MessageBox.Show(this, "Paso 1 ejecutado correctamente.", "Paso 1 SIC/XE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                EscribirLineaConsola($"[ERROR Paso 1] {ex.Message}");
                MessageBox.Show(this, $"Error en Paso 1: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EjecutarPaso2()
        {
            try
            {
                var resultado = EjecutarPipeline(runPaso2: true);
                RefrescarVista(resultado, incluirCodigoObjeto: true);
                ExportarSalidaPaso1(resultado);
                ExportarSalidaPaso2YObjeto(resultado);
                MostrarSalidaPipelineEnConsola("PASO 2 SIC/XE", resultado, incluirPaso2: true);
                MessageBox.Show(this, "Paso 2 ejecutado correctamente.", "Paso 2 SIC/XE", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                EscribirLineaConsola($"[ERROR Paso 2] {ex.Message}");
                MessageBox.Show(this, $"Error en Paso 2: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void EnsamblarTodo()
        {
            try
            {
                var resultado = EjecutarPipeline(runPaso2: true);
                RefrescarVista(resultado, incluirCodigoObjeto: true);

                ExportarAnalizador(resultado.Parse.ErroresLexicoSintacticos);
                ExportarSalidaPaso1(resultado);
                ExportarSalidaPaso2YObjeto(resultado);
                ExportarTablaBloques(resultado.Paso1.Lines);
                MostrarSalidaPipelineEnConsola("ENSAMBLADO COMPLETO", resultado, incluirPaso2: true);

                MessageBox.Show(this,
                    "Ensamblado completo finalizado.\nSe generaron Errores, Intermedio, TABSIM, Objeto y Bloques.",
                    "Ensamblador",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                EscribirLineaConsola($"[ERROR Ensamblar] {ex.Message}");
                MessageBox.Show(this, $"Error al ensamblar: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private PipelineResult EjecutarPipeline(bool runPaso2)
        {
            var parse = PrepararParse();

            var paso1 = new Paso1();
            paso1.AddExternalErrors(parse.ErroresLexicoSintacticos);
            paso1.SetSourceLines(parse.SourceLines);

            var walker = new ParseTreeWalker();
            walker.Walk(paso1, parse.Tree);

            Paso2? paso2 = null;
            List<ObjectCodeLine>? objectLines = null;
            List<string>? registros = null;

            if (runPaso2)
            {
                paso2 = new Paso2(
                    paso1.Lines,
                    paso1.SymbolTableExtended,
                    paso1.ProgramStartAddress,
                    paso1.ProgramSize,
                    paso1.ProgramName,
                    paso1.BaseValue);

                paso2.ObjectCodeGeneration();
                objectLines = paso2.ObjectCodeLines;

                var programaObjeto = new ProgramaObjeto(
                    objectLines,
                    paso1.ProgramName,
                    paso1.ProgramStartAddress,
                    paso1.ProgramSize,
                    paso1.ExecutionEntryPoint);

                registros = programaObjeto.GenerarRegistros();
            }

            return new PipelineResult(parse, paso1, paso2, objectLines, registros);
        }

        private void RefrescarVista(PipelineResult resultado, bool incluirCodigoObjeto)
        {
            ArchivoInterdataGridView1.DataSource = CrearTablaIntermedio(resultado.Paso1.Lines, incluirCodigoObjeto ? resultado.ObjectLines : null);
            AplicarNumeracionDeRenglones(ArchivoInterdataGridView1);
            TablaSimdataGridView2.DataSource = CrearTablaSimbolos(resultado.Paso1.SymbolTableExtended);

            var erroresAnalizador = resultado.Parse.ErroresLexicoSintacticos
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
            var erroresPaso1 = ObtenerErroresPaso1SinDuplicar(resultado)
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
            var erroresPaso2 = (resultado.Paso2?.Errors ?? new List<SICXEError>())
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();

            MostrarErroresPorEtapa(erroresAnalizador, erroresPaso1, incluirCodigoObjeto ? erroresPaso2 : null);

            var lineasError = erroresAnalizador
                .Concat(erroresPaso1)
                .Concat(erroresPaso2)
                .Select(e => e.Line)
                .Distinct();
            AplicarResaltadoSintaxis(lineasError);

            if (incluirCodigoObjeto)
            {
                var bloques = ConstruirResumenBloques(resultado.Paso1.Lines);
                RegistrostextBox2.Text = ConstruirTextoRegistros(resultado.Registros ?? new List<string>());
                tablaBlqsGridView1.DataSource = CrearTablaBloques(bloques);
            }
            else
            {
                RegistrostextBox2.Text = "";
                tablaBlqsGridView1.DataSource = null;
            }
        }

        private ParseResult PrepararParse()
        {
            string source = codigoTextBox1.Text ?? string.Empty;
            string[] sourceLines = source.Replace("\r\n", "\n").Split('\n');

            if (!source.EndsWith("\n", StringComparison.Ordinal))
                source += "\n";

            var inputStream = new AntlrInputStream(source);
            var lexer = new SICXELexer(inputStream);
            var lexerErr = new SICXEErrorListener();
            lexer.RemoveErrorListeners();
            lexer.AddErrorListener(lexerErr);

            var tokenStream = new CommonTokenStream(lexer);
            var parser = new SICXEParser(tokenStream);
            var parserErr = new SICXEErrorListener();
            parser.RemoveErrorListeners();
            parser.AddErrorListener(parserErr);

            var tree = parser.program();

            var errores = lexerErr.Errors.Concat(parserErr.Errors)
                .GroupBy(e => new { e.Line, e.Column, e.Message, e.Type })
                .Select(g => g.First())
                .ToList();

            AplicarResaltadoSintaxis(errores.Select(e => e.Line).Distinct());

            return new ParseResult(tree, sourceLines, errores);
        }

        private DataTable CrearTablaIntermedio(IReadOnlyList<IntermediateLine> lines, IReadOnlyList<ObjectCodeLine>? objectLines)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("CP", typeof(string));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("NoBloque", typeof(int));
            tabla.Columns.Add("ETQ", typeof(string));
            tabla.Columns.Add("CODOP", typeof(string));
            tabla.Columns.Add("OPR", typeof(string));
            tabla.Columns.Add("VALOR_SEM", typeof(string));
            tabla.Columns.Add("FMT", typeof(string));
            tabla.Columns.Add("MOD", typeof(string));

            if (objectLines != null)
                tabla.Columns.Add("COD_OBJ", typeof(string));

            tabla.Columns.Add("ERR", typeof(string));
            tabla.Columns.Add("COMENTARIO", typeof(string));

            var objByLine = objectLines?.ToDictionary(o => o.IntermLine.LineNumber, o => o.ObjectCode) ?? new Dictionary<int, string>();

            foreach (var l in lines)
            {
                string cp = l.Address >= 0 ? l.Address.ToString("X4") : string.Empty;
                string fmt = l.Format > 0 ? l.Format.ToString() : string.Empty;

                if (objectLines == null)
                {
                    tabla.Rows.Add(cp, l.BlockName, l.BlockNumber, l.Label, l.Operation, l.Operand,
                        l.SemanticValue, fmt, l.AddressingMode, l.Error, l.Comment);
                }
                else
                {
                    objByLine.TryGetValue(l.LineNumber, out string? codObj);
                    tabla.Rows.Add(cp, l.BlockName, l.BlockNumber, l.Label, l.Operation, l.Operand,
                        l.SemanticValue, fmt, l.AddressingMode, codObj ?? string.Empty, l.Error, l.Comment);
                }
            }

            return tabla;
        }

        private DataTable CrearTablaSimbolos(SimbolosYExpresiones simbolos)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("Simbolo", typeof(string));
            tabla.Columns.Add("Direccion/Valor", typeof(string));
            tabla.Columns.Add("Tipo", typeof(string));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("NoBloque", typeof(int));

            foreach (var kv in simbolos.GetAllSymbols().OrderBy(k => k.Value.Value))
            {
                var sym = kv.Value;
                string tipo = sym.Type == SymbolType.Relative ? "R" : "A";
                string valor = sym.Value.ToString("X4");
                tabla.Rows.Add(sym.Name, valor, tipo, sym.BlockName, sym.BlockNumber);
            }

            return tabla;
        }

        private List<SICXEError> UnificarErrores(PipelineResult resultado)
        {
            var errores = new List<SICXEError>();
            errores.AddRange(resultado.Parse.ErroresLexicoSintacticos);
            errores.AddRange(ObtenerErroresPaso1SinDuplicar(resultado));

            if (resultado.Paso2 != null)
                errores.AddRange(resultado.Paso2.Errors);

            return errores
                .GroupBy(e => new { e.Line, e.Column, e.Message, e.Type })
                .Select(g => g.First())
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
        }

        private List<SICXEError> ObtenerErroresPaso1SinDuplicar(PipelineResult resultado)
        {
            var setAnalizador = new HashSet<string>(
                resultado.Parse.ErroresLexicoSintacticos.Select(ClaveError),
                StringComparer.Ordinal);

            return resultado.Paso1.ErrorList
                .Where(e => !setAnalizador.Contains(ClaveError(e)))
                .GroupBy(ClaveError, StringComparer.Ordinal)
                .Select(g => g.First())
                .ToList();
        }

        private static string ClaveError(SICXEError e)
            => $"{e.Line}|{e.Column}|{e.Type}|{e.Message}";

        private void MostrarErroresAnalizador(IReadOnlyList<SICXEError> erroresAnalizador)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ANALIZADOR LÉXICO-SINTÁCTICO ===");

            if (erroresAnalizador.Count == 0)
            {
                sb.AppendLine("Sin errores.");
            }
            else
            {
                foreach (var err in erroresAnalizador)
                    sb.AppendLine(err.ToString());
            }

            ErrortextBox3.Text = sb.ToString();
        }

        private void MostrarErroresPorEtapa(
            IReadOnlyList<SICXEError> erroresAnalizador,
            IReadOnlyList<SICXEError> erroresPaso1,
            IReadOnlyList<SICXEError>? erroresPaso2)
        {
            var sb = new StringBuilder();

            sb.AppendLine("=== ANALIZADOR LÉXICO-SINTÁCTICO ===");
            if (erroresAnalizador.Count == 0)
                sb.AppendLine("Sin errores.");
            else
                foreach (var err in erroresAnalizador)
                    sb.AppendLine(err.ToString());

            sb.AppendLine();
            sb.AppendLine("=== ERRORES PASO 1 ===");
            if (erroresPaso1.Count == 0)
                sb.AppendLine("Sin errores.");
            else
                foreach (var err in erroresPaso1)
                    sb.AppendLine(err.ToString());

            if (erroresPaso2 != null)
            {
                sb.AppendLine();
                sb.AppendLine("=== ERRORES PASO 2 ===");
                if (erroresPaso2.Count == 0)
                    sb.AppendLine("Sin errores.");
                else
                    foreach (var err in erroresPaso2)
                        sb.AppendLine(err.ToString());
            }

            ErrortextBox3.Text = sb.ToString();
        }

        private void AplicarNumeracionDeRenglones(DataGridView grid)
        {
            for (int i = 0; i < grid.Rows.Count; i++)
            {
                grid.Rows[i].HeaderCell.Value = (i + 1).ToString();
            }
        }

        private void AsegurarConsola()
        {
            if (GetConsoleWindow() == nint.Zero)
                AllocConsole();

            if (_consoleReady)
                return;

            _consoleReady = true;
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            Console.Title = "Salida Ensamblador SIC/XE";
        }

        private void EscribirLineaConsola(string texto)
        {
            AsegurarConsola();
            Console.WriteLine(texto);
        }

        private static string SeparadorConsola(char c = '=') => new string(c, 78);

        private void ProgramarResaltado()
        {
            if (_isHighlighting)
                return;

            _highlightTimer?.Stop();
            _highlightTimer?.Start();
        }

        private void AplicarResaltadoSintaxis(IEnumerable<int>? lineasError = null)
        {
            if (_isHighlighting)
                return;

            try
            {
                _isHighlighting = true;

                int originalStart = codigoTextBox1.SelectionStart;
                int originalLength = codigoTextBox1.SelectionLength;

                codigoTextBox1.SuspendLayout();
                codigoTextBox1.SelectAll();
                codigoTextBox1.SelectionColor = Color.Black;

                var instrucciones = Paso1.OPTAB.Keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var directivas = Paso1.DIRECTIVES.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var errorSet = new HashSet<int>(lineasError ?? Enumerable.Empty<int>());

                var lines = codigoTextBox1.Lines;
                for (int i = 0; i < lines.Length; i++)
                {
                    int lineNumber = i + 1;
                    int lineStart = codigoTextBox1.GetFirstCharIndexFromLine(i);
                    if (lineStart < 0)
                        continue;

                    string lineText = lines[i] ?? string.Empty;
                    if (lineText.Length == 0)
                        continue;

                    if (errorSet.Contains(lineNumber))
                    {
                        codigoTextBox1.Select(lineStart, lineText.Length);
                        codigoTextBox1.SelectionColor = Color.Red;
                        continue;
                    }

                    var labelMatch = Regex.Match(lineText, @"^\s*([A-Za-z][A-Za-z0-9_]*)");
                    if (labelMatch.Success)
                    {
                        int ls = lineStart + labelMatch.Groups[1].Index;
                        int ll = labelMatch.Groups[1].Length;
                        codigoTextBox1.Select(ls, ll);
                        codigoTextBox1.SelectionColor = Color.Black;
                    }

                    foreach (Match m in Regex.Matches(lineText, @"\b[A-Za-z][A-Za-z0-9]*\b"))
                    {
                        int tokenStart = lineStart + m.Index;
                        int tokenLength = m.Length;
                        string token = m.Value;

                        if (instrucciones.Contains(token))
                        {
                            codigoTextBox1.Select(tokenStart, tokenLength);
                            codigoTextBox1.SelectionColor = Color.Blue;
                        }
                        else if (directivas.Contains(token))
                        {
                            codigoTextBox1.Select(tokenStart, tokenLength);
                            codigoTextBox1.SelectionColor = Color.ForestGreen;
                        }
                    }
                }

                codigoTextBox1.Select(originalStart, originalLength);
                codigoTextBox1.SelectionColor = Color.Black;
                codigoTextBox1.ResumeLayout();
            }
            finally
            {
                _isHighlighting = false;
            }
        }

        private void MostrarSalidaAnalizadorEnConsola(IReadOnlyList<SICXEError> errores, string archivoErrores)
        {
            AsegurarConsola();
            Console.WriteLine();
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine("ANALIZADOR LÉXICO-SINTÁCTICO");
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine($"Fuente: {ObtenerNombreBaseFuente()}");
            Console.WriteLine($"Total errores: {errores.Count}");
            Console.WriteLine($"Archivo errores: {archivoErrores}");
            Console.WriteLine();

            if (errores.Count == 0)
            {
                Console.WriteLine("Sin errores.");
            }
            else
            {
                foreach (var err in errores)
                    Console.WriteLine(err.ToString());
            }
        }

        private void MostrarSalidaPipelineEnConsola(string titulo, PipelineResult resultado, bool incluirPaso2)
        {
            AsegurarConsola();

            var erroresAnalizador = resultado.Parse.ErroresLexicoSintacticos
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
            var erroresPaso1 = ObtenerErroresPaso1SinDuplicar(resultado)
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
            var erroresPaso2 = (resultado.Paso2?.Errors ?? new List<SICXEError>())
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();

            Console.WriteLine();
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine(titulo);
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine($"Fuente: {ObtenerNombreBaseFuente()}");
            Console.WriteLine($"Intermedio (renglones): {resultado.Paso1.Lines.Count}");
            Console.WriteLine($"TABSIM (símbolos): {resultado.Paso1.SymbolTableExtended.Count}");
            Console.WriteLine();

            Console.WriteLine("[Errores Analizador]");
            if (erroresAnalizador.Count == 0) Console.WriteLine("Sin errores.");
            foreach (var e in erroresAnalizador) Console.WriteLine(e.ToString());
            Console.WriteLine();

            Console.WriteLine("[Errores Paso 1]");
            if (erroresPaso1.Count == 0) Console.WriteLine("Sin errores.");
            foreach (var e in erroresPaso1) Console.WriteLine(e.ToString());

            if (incluirPaso2)
            {
                Console.WriteLine();
                Console.WriteLine("[Errores Paso 2]");
                if (erroresPaso2.Count == 0) Console.WriteLine("Sin errores.");
                foreach (var e in erroresPaso2) Console.WriteLine(e.ToString());

                if (resultado.Registros != null)
                {
                    Console.WriteLine();
                    Console.WriteLine("[Registros Objeto]");
                    foreach (var r in resultado.Registros)
                        Console.WriteLine(r);
                }

                var bloques = ConstruirResumenBloques(resultado.Paso1.Lines);
                Console.WriteLine();
                Console.WriteLine("[Tabla de Bloques]");
                Console.WriteLine("No  Bloque              Inicio  Fin     Longitud");
                foreach (var b in bloques)
                    Console.WriteLine($"{b.Numero,2}  {b.Nombre,-18} {b.Inicio:X4}    {b.Fin:X4}    {b.Longitud:X4}");
            }
        }

        private string ConstruirContenidoErrores(IReadOnlyList<SICXEError> errores, string encabezado)
        {
            var sb = new StringBuilder();
            sb.AppendLine(encabezado);
            sb.AppendLine($"Generado: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine();

            if (errores.Count == 0)
            {
                sb.AppendLine("Sin errores.");
                return sb.ToString();
            }

            foreach (var e in errores)
                sb.AppendLine(e.ToString());

            return sb.ToString();
        }

        private void ExportarAnalizador(IReadOnlyList<SICXEError> erroresLexicoSintacticos)
        {
            string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes");
            Directory.CreateDirectory(reportesDir);

            string baseName = ObtenerNombreBaseFuente();
            string path = Path.Combine(reportesDir, $"{baseName}_ERRORES_SINTACTICOS_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(path, ConstruirContenidoErrores(erroresLexicoSintacticos.OrderBy(e => e.Line).ThenBy(e => e.Column).ToList(), "ANALIZADOR LÉXICO-SINTÁCTICO"));
        }

        private void ExportarSalidaPaso1(PipelineResult resultado)
        {
            string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes_paso1");
            Directory.CreateDirectory(reportesDir);

            string baseName = ObtenerNombreBaseFuente();
            string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string csvPaso1 = Path.Combine(reportesDir, $"{baseName}_PASO1_{time}.csv");
            resultado.Paso1.ExportToSingleCSV(csvPaso1, UnificarErrores(resultado));

            string txtErrores = Path.Combine(reportesDir, $"{baseName}_ERRORES_{time}.txt");
            File.WriteAllText(txtErrores, ConstruirContenidoErrores(UnificarErrores(resultado), "ERRORES ENSAMBLADOR"));
        }

        private void ExportarSalidaPaso2YObjeto(PipelineResult resultado)
        {
            if (resultado.Paso2 == null || resultado.ObjectLines == null)
                return;

            string baseName = ObtenerNombreBaseFuente();
            string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string reportesPaso2 = Path.Combine(ObtenerDirectorioProyecto(), "reportes_paso2");
            Directory.CreateDirectory(reportesPaso2);
            string csvPaso2 = Path.Combine(reportesPaso2, $"{baseName}_PASO2_{time}.csv");
            resultado.Paso2.ExportToCSV(csvPaso2);

            var programaObjeto = new ProgramaObjeto(
                resultado.ObjectLines,
                resultado.Paso1.ProgramName,
                resultado.Paso1.ProgramStartAddress,
                resultado.Paso1.ProgramSize,
                resultado.Paso1.ExecutionEntryPoint);

            string reportesObj = Path.Combine(ObtenerDirectorioProyecto(), "reportes_objeto");
            Directory.CreateDirectory(reportesObj);
            string csvObjeto = Path.Combine(reportesObj, $"{baseName}_OBJETO_{time}.csv");
            programaObjeto.ExportarACSV(csvObjeto);
        }

        private void ExportarTablaBloques(IReadOnlyList<IntermediateLine> lines)
        {
            string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes");
            Directory.CreateDirectory(reportesDir);

            string baseName = ObtenerNombreBaseFuente();
            string path = Path.Combine(reportesDir, $"{baseName}_TABBLOQUES_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var bloques = ConstruirResumenBloques(lines);
            var sb = new StringBuilder();
            sb.AppendLine("TABLA DE BLOQUES");
            sb.AppendLine("NoBloque\tBloque\tInicio\tFin\tLongitud");

            foreach (var b in bloques)
                sb.AppendLine($"{b.Numero}\t{b.Nombre}\t{b.Inicio:X4}\t{b.Fin:X4}\t{b.Longitud:X4}");

            File.WriteAllText(path, sb.ToString());
        }

        private List<BloqueResumen> ConstruirResumenBloques(IReadOnlyList<IntermediateLine> lines)
        {
            return lines
                .Where(l => l.Address >= 0)
                .GroupBy(l => new { l.BlockNumber, l.BlockName })
                .Select(g =>
                {
                    int ini = g.Min(x => x.Address);
                    int fin = g.Max(x => x.Address + Math.Max(0, x.Increment) - 1);
                    if (fin < ini)
                        fin = ini;

                    return new BloqueResumen(
                        g.Key.BlockNumber,
                        g.Key.BlockName,
                        ini,
                        fin,
                        (fin - ini) + 1);
                })
                .OrderBy(b => b.Numero)
                .ToList();
        }

        private DataTable CrearTablaBloques(IReadOnlyList<BloqueResumen> bloques)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("NoBloque", typeof(int));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("Inicio", typeof(string));
            tabla.Columns.Add("Fin", typeof(string));
            tabla.Columns.Add("Longitud", typeof(string));

            foreach (var b in bloques)
            {
                tabla.Rows.Add(b.Numero, b.Nombre, b.Inicio.ToString("X4"), b.Fin.ToString("X4"), b.Longitud.ToString("X4"));
            }

            return tabla;
        }

        private string ConstruirTextoRegistros(IReadOnlyList<string> registros)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ARCHIVO DE REGISTROS ===");

            if (registros.Count == 0)
            {
                sb.AppendLine("Sin registros.");
            }
            else
            {
                foreach (var r in registros)
                    sb.AppendLine(r);
            }

            return sb.ToString();
        }

        private string ObtenerDirectorioProyecto()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);
            while (current != null)
            {
                if (current.GetFiles("*.csproj").Length > 0)
                    return current.FullName;

                current = current.Parent;
            }

            return AppContext.BaseDirectory;
        }

        private string ObtenerNombreBaseFuente()
        {
            return string.IsNullOrWhiteSpace(_currentFilePath)
                ? "editor"
                : Path.GetFileNameWithoutExtension(_currentFilePath);
        }

        private sealed class ParseResult
        {
            public ParseResult(IParseTree tree, string[] sourceLines, List<SICXEError> erroresLexicoSintacticos)
            {
                Tree = tree;
                SourceLines = sourceLines;
                ErroresLexicoSintacticos = erroresLexicoSintacticos;
            }

            public IParseTree Tree { get; }
            public string[] SourceLines { get; }
            public List<SICXEError> ErroresLexicoSintacticos { get; }
        }

        private sealed class PipelineResult
        {
            public PipelineResult(ParseResult parse, Paso1 paso1, Paso2? paso2, List<ObjectCodeLine>? objectLines, List<string>? registros)
            {
                Parse = parse;
                Paso1 = paso1;
                Paso2 = paso2;
                ObjectLines = objectLines;
                Registros = registros;
            }

            public ParseResult Parse { get; }
            public Paso1 Paso1 { get; }
            public Paso2? Paso2 { get; }
            public List<ObjectCodeLine>? ObjectLines { get; }
            public List<string>? Registros { get; }
        }

        private sealed class BloqueResumen
        {
            public BloqueResumen(int numero, string nombre, int inicio, int fin, int longitud)
            {
                Numero = numero;
                Nombre = nombre;
                Inicio = inicio;
                Fin = fin;
                Longitud = longitud;
            }

            public int Numero { get; }
            public string Nombre { get; }
            public int Inicio { get; }
            public int Fin { get; }
            public int Longitud { get; }
        }
    }
}
