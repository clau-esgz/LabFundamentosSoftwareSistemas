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
        private bool _suspendDirtyTracking;
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

        private void Form1_Load(object sender, EventArgs e)
        {
            ConfigurarEditorFuente();
            ConfigurarPanelErrores();
            ConfigurarTablasResultados();
            ConfigurarResaltadoDiferido();
            NuevoArchivo();
        }

        private void ConfigurarEditorFuente()
        {
            codigoTextBox1.AcceptsTab = true;
            codigoTextBox1.WordWrap = false;
            codigoTextBox1.Font = new Font("Consolas", 10F);

            codigoTextBox1.TextChanged += (_, __) =>
            {
                if (_suspendDirtyTracking)
                    return;

                _isDirty = true;
                ActualizarTitulo();
                ProgramarResaltado();
            };
        }

        private void ConfigurarPanelErrores()
        {
            RegistrostextBox2.ReadOnly = true;
            RegistrostextBox2.Font = new Font("Consolas", 9F);

            ErrortextBox3.ReadOnly = true;
            ErrortextBox3.Font = new Font("Consolas", 9F);
        }

        private void ConfigurarTablasResultados()
        {
            ConfigurarGridBase(ArchivoInterdataGridView1, incluirCabecerasRenglon: true, anclarEventos: true, rowPrePaintHandler: ArchivoInterdataGridView1_RowPrePaint, anchoCabecera: 40);
            ConfigurarGridBase(TablaSimdataGridView2, anclarEventos: true, rowPrePaintHandler: TablaSimdataGridView2_RowPrePaint);
            ConfigurarGridBase(tablaBlqsGridView1);
        }

        private void ConfigurarGridBase(
            DataGridView grid,
            bool incluirCabecerasRenglon = false,
            bool anclarEventos = false,
            DataGridViewRowPrePaintEventHandler? rowPrePaintHandler = null,
            int anchoCabecera = 51)
        {
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.ReadOnly = true;

            if (incluirCabecerasRenglon)
            {
                grid.RowHeadersVisible = true;
                grid.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
                grid.RowHeadersWidth = anchoCabecera;
            }

            if (anclarEventos && rowPrePaintHandler != null)
                grid.RowPrePaint += rowPrePaintHandler;
        }

        private void ConfigurarResaltadoDiferido()
        {
            _highlightTimer = new System.Windows.Forms.Timer { Interval = 180 };
            _highlightTimer.Tick += (_, __) =>
            {
                _highlightTimer!.Stop();
                AplicarResaltadoSintaxis();
            };
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

            CargarFuenteEnEditor(string.Empty, null);
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

            CargarFuenteEnEditor(File.ReadAllText(ofd.FileName), ofd.FileName);
        }

        private bool GuardarArchivo()
        {
            if (string.IsNullOrWhiteSpace(_currentFilePath))
                return GuardarArchivoComo();

            File.WriteAllText(_currentFilePath!, codigoTextBox1.Text);
            MarcarDocumentoComoGuardado();
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

            MarcarDocumentoComoGuardado();
            return true;
        }

        private void CargarFuenteEnEditor(string contenido, string? filePath)
        {
            _currentFilePath = filePath;

            _suspendDirtyTracking = true;
            codigoTextBox1.Text = contenido;
            _suspendDirtyTracking = false;

            _isDirty = false;
            LimpiarPanelesResultado();
            ActualizarTitulo();
            AplicarResaltadoSintaxis();
        }

        private void LimpiarPanelesResultado()
        {
            ErrortextBox3.Clear();
            RegistrostextBox2.Clear();
            ArchivoInterdataGridView1.DataSource = null;
            TablaSimdataGridView2.DataSource = null;
            tablaBlqsGridView1.DataSource = null;
        }

        private void MarcarDocumentoComoGuardado()
        {
            _isDirty = false;
            ActualizarTitulo();
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
                RefrescarVista(resultado, incluirCodigoObjeto: false, ExecutionMode.Paso1);
                ExportarSalidaPaso1(resultado, ExecutionMode.Paso1);
                MostrarSalidaPipelineEnConsola("PASO 1 SIC/XE", resultado, ExecutionMode.Paso1);
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
                RefrescarVista(resultado, incluirCodigoObjeto: true, ExecutionMode.Paso2);
                ExportarSalidaPaso1(resultado, ExecutionMode.Paso2);
                ExportarSalidaPaso2YObjeto(resultado);
                MostrarSalidaPipelineEnConsola("PASO 2 SIC/XE", resultado, ExecutionMode.Paso2);
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
                RefrescarVista(resultado, incluirCodigoObjeto: true, ExecutionMode.Ensamblado);

                ExportarAnalizador(resultado.Parse.ErroresLexicoSintacticos);
                ExportarSalidaPaso1(resultado, ExecutionMode.Ensamblado);
                ExportarSalidaPaso2YObjeto(resultado);
                ExportarTablaBloques(resultado.Paso1);
                MostrarSalidaPipelineEnConsola("ENSAMBLADO COMPLETO", resultado, ExecutionMode.Ensamblado);

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
            var paso1 = EjecutarPaso1(parse);
            var paso2 = runPaso2 ? EjecutarPaso2(paso1) : null;
            var objectLines = paso2?.ObjectCodeLines;
            var registros = paso2 != null ? GenerarRegistrosObjeto(paso1, paso2, objectLines) : null;

            return new PipelineResult(parse, paso1, paso2, objectLines, registros);
        }

        private Paso1 EjecutarPaso1(ParseResult parse)
        {
            var paso1 = new Paso1();
            paso1.AddExternalErrors(parse.ErroresLexicoSintacticos);
            paso1.SetSourceLines(parse.SourceLines);

            var walker = new ParseTreeWalker();
            walker.Walk(paso1, parse.Tree);
            return paso1;
        }

        private Paso2 EjecutarPaso2(Paso1 paso1)
        {
            var paso2 = new Paso2(
                paso1.Lines,
                paso1.SymbolTableExtended,
                paso1.ProgramStartAddress,
                paso1.ProgramSize,
                paso1.ProgramName,
                paso1.BaseValue,
                paso1.ExecutionEntryPoint);

            paso2.ObjectCodeGeneration();
            return paso2;
        }

        /// <summary>
        /// Genera registros de modulo objeto (formato H/D/R/T/M/E) a partir de Paso 1 y Paso 2.
        /// Los registros son la salida final del ensamblador, listos para el enlazador (linker).
        /// </summary>
        /// <param name="paso1">Resultado del Paso 1: tabla de simbolos, direcciones, programa, etc.</param>
        /// <param name="paso2">Resultado del Paso 2: codigo objeto, modulos generados</param>
        /// <param name="objectLines">Lineas de codigo objeto (null si solo Paso 1 fue ejecutado)</param>
        /// <returns>Lista de lineas de registros de modulo objeto (H, D, R, T, M, E records)</returns>
        private List<string> GenerarRegistrosObjeto(Paso1 paso1, Paso2 paso2, IReadOnlyList<ObjectCodeLine>? objectLines)
        {
            if (objectLines == null)
                return new List<string>();

            var programaObjeto = new ProgramaObjeto(
                objectLines,
                paso2.GeneratedModules,
                paso1.ProgramName,
                paso1.ProgramStartAddress,
                paso1.ProgramSize,
                paso1.ExecutionEntryPoint);

            return programaObjeto.GenerarRegistros();
        }

        /// <summary>
        /// Refresca todos los componentes visuales con resultados del pipeline de ensamblaje.
        /// Actualiza grillas de intermedio, simbolos, bloques, y paneles de error.
        /// </summary>
        /// <param name="resultado">Resultado completo del pipeline (Parse, Paso1, Paso2)</param>
        /// <param name="incluirCodigoObjeto">Si true, incluye codigo objeto en intermedio (requiere Paso 2)</param>
        /// <param name="mode">Modo de ejecucion (Paso1, Paso2, o Ensamblado completo)</param>
        private void RefrescarVista(PipelineResult resultado, bool incluirCodigoObjeto, ExecutionMode mode)
        {
            ActualizarGridIntermedio(resultado, incluirCodigoObjeto);
            ActualizarGridSimbolos(resultado.Paso1.SymbolTableExtended);

            var erroresAnalizador = resultado.Parse.ErroresLexicoSintacticos.OrderBy(e => e.Line).ThenBy(e => e.Column).ToList();
            var erroresPaso1 = ObtenerErroresPaso1SinDuplicar(resultado).OrderBy(e => e.Line).ThenBy(e => e.Column).ToList();
            var erroresPaso2 = (resultado.Paso2?.Errors ?? new List<SICXEError>()).OrderBy(e => e.Line).ThenBy(e => e.Column).ToList();

            ActualizarErroresVista(resultado, mode, erroresAnalizador, erroresPaso1, erroresPaso2);
            AplicarResaltadoSintaxis(ObtenerLineasConError(erroresAnalizador, erroresPaso1, erroresPaso2));
            ActualizarVistaObjetoYBloques(resultado, incluirCodigoObjeto);
        }

        /// <summary>
        /// Actualiza la grilla de archivo intermedio con lineas y su codigo objeto asociado.
        /// Aplica numeracion automática de renglones y oculta columnas internas.
        /// </summary>
        private void ActualizarGridIntermedio(PipelineResult resultado, bool incluirCodigoObjeto)
        {
            ArchivoInterdataGridView1.DataSource = CrearTablaIntermedio(resultado.Paso1.Lines, incluirCodigoObjeto ? resultado.ObjectLines : null);
            AplicarNumeracionDeRenglones(ArchivoInterdataGridView1);

            if (ArchivoInterdataGridView1.Columns.Contains("TipoFormato"))
                ArchivoInterdataGridView1.Columns["TipoFormato"].Visible = false;
        }

        /// <summary>
        /// Actualiza la grilla de tabla de simbolos con datos de TABSIM.
        /// Oculta columnas de metadata que no deben mostrarse al usuario.
        /// </summary>
        private void ActualizarGridSimbolos(SimbolosYExpresiones simbolos)
        {
            TablaSimdataGridView2.DataSource = CrearTablaSimbolos(simbolos);

            if (TablaSimdataGridView2.Columns.Contains("EsCabecera"))
                TablaSimdataGridView2.Columns["EsCabecera"].Visible = false;
        }

        private void ActualizarErroresVista(
            PipelineResult resultado,
            ExecutionMode mode,
            IReadOnlyList<SICXEError> erroresAnalizador,
            IReadOnlyList<SICXEError> erroresPaso1,
            IReadOnlyList<SICXEError> erroresPaso2)
        {
            if (mode == ExecutionMode.Ensamblado)
            {
                MostrarErroresUnificados(UnificarErrores(resultado));
                return;
            }

            MostrarErroresPorEtapa(erroresAnalizador, erroresPaso1, mode == ExecutionMode.Paso2 ? erroresPaso2 : null);
        }

        private static IEnumerable<int> ObtenerLineasConError(
            IReadOnlyList<SICXEError> erroresAnalizador,
            IReadOnlyList<SICXEError> erroresPaso1,
            IReadOnlyList<SICXEError> erroresPaso2)
        {
            return erroresAnalizador
                .Concat(erroresPaso1)
                .Concat(erroresPaso2)
                .Select(e => e.Line)
                .Distinct();
        }

        private void ActualizarVistaObjetoYBloques(PipelineResult resultado, bool incluirCodigoObjeto)
        {
            if (!incluirCodigoObjeto)
            {
                RegistrostextBox2.Text = string.Empty;
                tablaBlqsGridView1.DataSource = null;
                return;
            }

            var bloques = ConstruirResumenBloques(resultado.Paso1);
            RegistrostextBox2.Text = ConstruirTextoRegistros(resultado.Registros ?? new List<string>());
            tablaBlqsGridView1.DataSource = CrearTablaBloques(bloques);
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

        /// <summary>
        /// Crea DataTable para mostrar archivo intermedio (salida del Paso 1 y entrada del Paso 2).
        /// Incluye direcciones, etiquetas, operaciones, operandos, codigo objeto, y errores detectados.
        /// Columnas: CP (direccion), ETQ (etiqueta), CODOP (operacion), OPR (operando), FMT (formato), COD_OBJ (objeto), ERR (errores).
        /// </summary>
        /// <param name="lines">Lineas intermedias del Paso 1</param>
        /// <param name="objectLines">Lineas de codigo objeto del Paso 2 (null si solo Paso 1 fue ejecutado)</param>
        /// <returns>DataTable poblado con datos del archivo intermedio para visualizacion en DataGridView</returns>
        private DataTable CrearTablaIntermedio(IReadOnlyList<IntermediateLine> lines, IReadOnlyList<ObjectCodeLine>? objectLines)
        {
            var tabla = new DataTable();
            tabla.Columns.Add(" ", typeof(int));
            tabla.Columns.Add("Seccion", typeof(string));
            tabla.Columns.Add("NoSeccion", typeof(int));
            tabla.Columns.Add("CP", typeof(string));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("NoBloque", typeof(string));
            tabla.Columns.Add("ETQ", typeof(string));
            tabla.Columns.Add("CODOP", typeof(string));
            tabla.Columns.Add("OPR", typeof(string));
            tabla.Columns.Add("VALOR_SEM", typeof(string));
            tabla.Columns.Add("FMT", typeof(string));
            tabla.Columns.Add("MOD", typeof(string));
            tabla.Columns.Add("Simbolo externo", typeof(string));
            tabla.Columns.Add("TipoFormato", typeof(string));

            if (objectLines != null)
                tabla.Columns.Add("COD_OBJ", typeof(string));

            tabla.Columns.Add("ERR", typeof(string));
            tabla.Columns.Add("COMENTARIO", typeof(string));

            var objByLine = objectLines?
                .Where(o => o.IntermLine != null)
                .GroupBy(o => o.IntermLine.LineNumber)
                .ToDictionary(g => g.Key, g => g.Last().ObjectCode) ?? new Dictionary<int, string>();

            foreach (var l in lines)
            {
                string cp = l.Address >= 0 ? l.Address.ToString("X4") : string.Empty;
                string fmt = l.Format > 0 ? l.Format.ToString() : string.Empty;
                string externalMark = l.ExternalReferenceSymbols.Count > 0 ? "Sí" : string.Empty;
                
                // Determinar el tipo de formato para aplicar estilos
                string tipoFormato = "Normal";
                string baseOperation = (l.Operation ?? string.Empty).TrimStart('+').ToUpperInvariant();
                if (baseOperation == "START" || baseOperation == "CSECT")
                    tipoFormato = "StartCsect";
                else if (baseOperation == "USE")
                    tipoFormato = "Use";
                else if (baseOperation == "EXTREF")
                    tipoFormato = "Extref";
                else if (baseOperation == "EXTDEF")
                    tipoFormato = "Extdef";

                string noBloqueDisplay = l.BlockNumber >= 0 ? l.BlockNumber.ToString() : "----";
                if (objectLines == null)
                {
                    tabla.Rows.Add(l.LineNumber, l.ControlSectionName, l.ControlSectionNumber, cp, l.BlockName, noBloqueDisplay, l.Label, l.Operation, l.Operand,
                        l.SemanticValue, fmt, l.AddressingMode, externalMark, tipoFormato, l.Error, l.Comment);
                }
                else
                {
                    objByLine.TryGetValue(l.LineNumber, out string? codObj);
                    tabla.Rows.Add(l.LineNumber, l.ControlSectionName, l.ControlSectionNumber, cp, l.BlockName, noBloqueDisplay, l.Label, l.Operation, l.Operand,
                        l.SemanticValue, fmt, l.AddressingMode, externalMark, tipoFormato, codObj ?? string.Empty, l.Error, l.Comment);
                }
            }

            return tabla;
        }

        /// <summary>
        /// Crea DataTable para mostrar tabla de simbolos (TABSIM): todos los simbolos, valores y tipos.
        /// Columnas: Simbolo, Valor (en hex), Tipo (Relativo/Absoluto), Seccion de control.
        /// </summary>
        /// <param name="simbolos">SimbolosYExpresiones con tabla de simbolos completa</param>
        /// <returns>DataTable con lista ordenada alfabeticamente de simbolos definidos</returns>
        private DataTable CrearTablaSimbolos(SimbolosYExpresiones simbolos)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("Seccion", typeof(string));
            tabla.Columns.Add("Simbolo", typeof(string));
            tabla.Columns.Add("Direccion/Valor", typeof(string));
            tabla.Columns.Add("Tipo", typeof(string));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("NoBloque", typeof(string));
            tabla.Columns.Add("Símbolo externo", typeof(string));
            tabla.Columns.Add("EsCabecera", typeof(bool));

            var symbolsBySection = simbolos.GetAllSymbols()
                .Values
                .OrderBy(s => s.ControlSectionName, StringComparer.OrdinalIgnoreCase)
                // Externos primero dentro de cada sección
                .ThenByDescending(s => s.IsExternal)
                .ThenBy(s => s.Value)
                .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
                .GroupBy(s => s.ControlSectionName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            foreach (var group in symbolsBySection)
            {
                // Cabecera de sección
                tabla.Rows.Add(group.Key, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, true);

                foreach (var sym in group)
                {
                    if (sym.IsExternal)
                    {
                        tabla.Rows.Add(string.Empty, sym.Name, "-----", "-", "----", "----", "Sí", false);
                    }
                    else
                    {
                        string tipo = sym.Type == SymbolType.Relative ? "R" : "A";
                        string valor = sym.Value.ToString("X4");
                        string externo = sym.IsExternal ? "Sí" : "No";
                        tabla.Rows.Add(string.Empty, sym.Name, valor, tipo, sym.BlockName, sym.BlockNumber.ToString(), externo, false);
                    }
                }
            }

            return tabla;
        }

        private void TablaSimdataGridView2_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (sender is not DataGridView dgv) return;
            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) return;

            var row = dgv.Rows[e.RowIndex];
            try
            {
                if (dgv.Columns.Contains("EsCabecera"))
                {
                    var col = dgv.Columns["EsCabecera"];
                    var cell = row.Cells[col.Index];
                    if (cell?.Value is bool isHeader && isHeader)
                    {
                        row.DefaultCellStyle.BackColor = Color.LightGray;
                        row.DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                        row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                        row.ReadOnly = true;
                    }
                }
                else
                {
                    // reset to default
                    row.DefaultCellStyle.BackColor = dgv.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.Font = dgv.DefaultCellStyle.Font;
                }
            }
            catch
            {
                // no-op: defensivo para evitar excepciones de renderizado
            }
        }

        private void ArchivoInterdataGridView1_RowPrePaint(object? sender, DataGridViewRowPrePaintEventArgs e)
        {
            if (sender is not DataGridView dgv) return;
            if (e.RowIndex < 0 || e.RowIndex >= dgv.Rows.Count) return;

            var row = dgv.Rows[e.RowIndex];
            try
            {
                if (dgv.Columns.Contains("TipoFormato"))
                {
                    var tipoFormatoCol = dgv.Columns["TipoFormato"];
                    var tipoCell = row.Cells[tipoFormatoCol.Index];
                    
                    if (tipoCell?.Value is string tipoFormato)
                    {
                        if (tipoFormato == "StartCsect")
                        {
                            row.DefaultCellStyle.BackColor = Color.AliceBlue;
                            row.DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                            row.DefaultCellStyle.ForeColor = Color.Blue;
                            row.HeaderCell.Style.Font = new Font(dgv.Font, FontStyle.Bold);
                            row.HeaderCell.Style.ForeColor = Color.Blue;
                        }
                        else if (tipoFormato == "Use")
                        {
                            row.DefaultCellStyle.BackColor = Color.LightGreen;
                            row.DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                            row.DefaultCellStyle.ForeColor = Color.Green;
                            row.HeaderCell.Style.Font = new Font(dgv.Font, FontStyle.Bold);
                            row.HeaderCell.Style.ForeColor = Color.Green;
                        }
                        else if (tipoFormato == "Extref" || tipoFormato == "Extdef")
                        {
                            // Solo colorear el CODOP en verde y negrita
                            if (dgv.Columns.Contains("CODOP"))
                            {
                                var codopCell = row.Cells[dgv.Columns["CODOP"].Index];
                                codopCell.Style.Font = new Font(dgv.Font, FontStyle.Bold);
                                codopCell.Style.ForeColor = Color.Green;
                            }
                            // Colorear el renglón encabezado (número de línea) también en verde
                            row.HeaderCell.Style.ForeColor = Color.Green;
                            row.HeaderCell.Style.Font = new Font(dgv.Font, FontStyle.Bold);
                        }
                        else
                        {
                            // Normal
                            row.DefaultCellStyle.BackColor = dgv.DefaultCellStyle.BackColor;
                            row.DefaultCellStyle.Font = dgv.DefaultCellStyle.Font;
                            row.DefaultCellStyle.ForeColor = dgv.DefaultCellStyle.ForeColor;
                            row.HeaderCell.Style.ForeColor = Color.Black;
                            row.HeaderCell.Style.Font = dgv.Font;
                        }
                        row.DefaultCellStyle.SelectionBackColor = row.DefaultCellStyle.BackColor;
                    }
                }
                else
                {
                    // reset to default
                    row.DefaultCellStyle.BackColor = dgv.DefaultCellStyle.BackColor;
                    row.DefaultCellStyle.Font = dgv.DefaultCellStyle.Font;
                    row.DefaultCellStyle.ForeColor = dgv.DefaultCellStyle.ForeColor;
                    row.HeaderCell.Style.ForeColor = Color.Black;
                    row.HeaderCell.Style.Font = dgv.Font;
                }
            }
            catch
            {
                // no-op: defensivo para evitar excepciones de renderizado
            }
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

        private void MostrarErroresUnificados(IReadOnlyList<SICXEError> errores)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== ERRORES ENSAMBLADO (UNIFICADOS) ===");
            if (errores.Count == 0)
            {
                sb.AppendLine("Sin errores.");
            }
            else
            {
                foreach (var err in errores)
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
                ResaltarSintaxis(lineasError);
            }
            finally
            {
                _isHighlighting = false;
            }
        }

        private void ResaltarSintaxis(IEnumerable<int>? lineasError)
        {
            int originalStart = codigoTextBox1.SelectionStart;
            int originalLength = codigoTextBox1.SelectionLength;

            codigoTextBox1.SuspendLayout();
            try
            {
                PrepararEditorParaResaltado();

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
                        PintarLineaConError(lineStart, lineText.Length);
                        continue;
                    }

                    PintarEtiqueta(lineText, lineStart);
                    PintarPalabrasClave(lineText, lineStart, instrucciones, directivas);
                }

                RestaurarSeleccion(originalStart, originalLength);
            }
            finally
            {
                codigoTextBox1.ResumeLayout();
            }
        }

        private void PrepararEditorParaResaltado()
        {
            codigoTextBox1.SelectAll();
            codigoTextBox1.SelectionColor = Color.Black;
        }

        private void PintarLineaConError(int lineStart, int lineLength)
        {
            codigoTextBox1.Select(lineStart, lineLength);
            codigoTextBox1.SelectionColor = Color.Red;
        }

        private void PintarEtiqueta(string lineText, int lineStart)
        {
            var labelMatch = Regex.Match(lineText, @"^\s*([A-Za-z][A-Za-z0-9_]*)");
            if (!labelMatch.Success)
                return;

            int ls = lineStart + labelMatch.Groups[1].Index;
            int ll = labelMatch.Groups[1].Length;
            codigoTextBox1.Select(ls, ll);
            codigoTextBox1.SelectionColor = Color.Black;
        }

        private void PintarPalabrasClave(
            string lineText,
            int lineStart,
            ISet<string> instrucciones,
            ISet<string> directivas)
        {
            foreach (Match m in Regex.Matches(lineText, @"\b[A-Za-z][A-Za-z0-9]*\b"))
            {
                int tokenStart = lineStart + m.Index;
                int tokenLength = m.Length;
                string token = m.Value;

                if (instrucciones.Contains(token))
                    PintarToken(tokenStart, tokenLength, Color.Blue);
                else if (directivas.Contains(token))
                    PintarToken(tokenStart, tokenLength, Color.ForestGreen);
            }
        }

        private void PintarToken(int tokenStart, int tokenLength, Color color)
        {
            codigoTextBox1.Select(tokenStart, tokenLength);
            codigoTextBox1.SelectionColor = color;
        }

        private void RestaurarSeleccion(int originalStart, int originalLength)
        {
            codigoTextBox1.Select(originalStart, originalLength);
            codigoTextBox1.SelectionColor = Color.Black;
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

        private void MostrarSalidaPipelineEnConsola(string titulo, PipelineResult resultado, ExecutionMode mode)
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

            EscribirEncabezadoPipeline(titulo, resultado);

            if (mode == ExecutionMode.Ensamblado)
            {
                EscribirErroresConsola("[Errores Unificados]", UnificarErrores(resultado));
            }
            else
            {
                EscribirErroresConsola("[Errores Analizador]", erroresAnalizador);
                EscribirErroresConsola("[Errores Paso 1]", erroresPaso1);

                if (mode == ExecutionMode.Paso2)
                    EscribirErroresConsola("[Errores Paso 2]", erroresPaso2);
            }

            EscribirTablaSimbolosConsola(resultado.Paso1.SymbolTableExtended);
            EscribirTablaIntermediaConsola(resultado.Paso1.Lines);
            EscribirTablaBloquesConsola(ConstruirResumenBloques(resultado.Paso1));
            EscribirRegistrosObjetoConsola(resultado.Registros);
        }

        private void EscribirEncabezadoPipeline(string titulo, PipelineResult resultado)
        {
            Console.WriteLine();
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine(titulo);
            Console.WriteLine(SeparadorConsola('='));
            Console.WriteLine($"Fuente: {ObtenerNombreBaseFuente()}");
            Console.WriteLine($"Intermedio (renglones): {resultado.Paso1.Lines.Count}");
            Console.WriteLine($"TABSIM (símbolos): {resultado.Paso1.SymbolTableExtended.Count}");
            Console.WriteLine();
        }

        private static void EscribirErroresConsola(string titulo, IReadOnlyList<SICXEError> errores)
        {
            Console.WriteLine();
            Console.WriteLine(titulo);
            if (errores.Count == 0)
            {
                Console.WriteLine("Sin errores.");
                return;
            }

            foreach (var e in errores)
                Console.WriteLine(e.ToString());
        }

        private void EscribirTablaSimbolosConsola(SimbolosYExpresiones simbolos)
        {
            Console.WriteLine();
            Console.WriteLine("[Tabla de Simbolos]");
            Console.WriteLine("Simbolo             Valor  Tipo  Bloque");
            foreach (var kv in simbolos.GetAllSymbols().OrderBy(k => k.Value.Value))
            {
                var sym = kv.Value;
                string displayValue = sym.IsExternal ? "----" : sym.Value.ToString("X4");
                string tipo = sym.IsExternal ? "---" : (sym.Type == SymbolType.Relative ? "R" : "A");
                Console.WriteLine($"{sym.Name,-18} {displayValue,-6} {tipo,-4}  {sym.BlockName}");
            }
        }

        private void EscribirTablaIntermediaConsola(IReadOnlyList<IntermediateLine> lineas)
        {
            Console.WriteLine();
            Console.WriteLine("[Archivo Intermedio]");
            Console.WriteLine("NL   CP    Bloque             ETQ        CODOP      OPR");
            foreach (var l in lineas)
            {
                string cp = l.Address >= 0 ? l.Address.ToString("X4") : "----";
                Console.WriteLine($"{l.LineNumber,-4} {cp,-5} {l.BlockName,-18} {l.Label,-10} {l.Operation,-10} {l.Operand}");
            }
        }

        private void EscribirTablaBloquesConsola(IReadOnlyList<BloqueResumen> bloques)
        {
            Console.WriteLine();
            Console.WriteLine("[Tabla de Bloques]");
            Console.WriteLine("Seccion  TamSec  No  Bloque              Longitud DirIniRel");
            foreach (var b in bloques)
                Console.WriteLine($"{b.Seccion,-8} {b.TotalSeccion:X4}   {b.Numero,2}  {b.Nombre,-18} {b.Longitud:X4}    {b.DirIniRel:X4}");
        }

        private void EscribirRegistrosObjetoConsola(IReadOnlyList<string>? registros)
        {
            if (registros == null || registros.Count == 0)
                return;

            Console.WriteLine();
            Console.WriteLine("[Registros Objeto]");
            foreach (var r in registros)
                Console.WriteLine(r);
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

        private void ExportarSalidaPaso1(PipelineResult resultado, ExecutionMode mode)
        {
            string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes_paso1");
            Directory.CreateDirectory(reportesDir);

            string baseName = ObtenerNombreBaseFuente();
            string time = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var errores = ObtenerErroresPorModo(resultado, mode);

            string csvPaso1 = Path.Combine(reportesDir, $"{baseName}_PASO1_{time}.csv");
            resultado.Paso1.ExportToSingleCSV(csvPaso1, errores);

            string txtErrores = Path.Combine(reportesDir, $"{baseName}_ERRORES_{time}.txt");
            File.WriteAllText(txtErrores, ConstruirContenidoErrores(errores, mode == ExecutionMode.Ensamblado ? "ERRORES ENSAMBLADOR (UNIFICADOS)" : "ERRORES ENSAMBLADOR"));
        }

        private List<SICXEError> ObtenerErroresPorModo(PipelineResult resultado, ExecutionMode mode)
        {
            var erroresAnalizador = resultado.Parse.ErroresLexicoSintacticos;
            var erroresPaso1 = ObtenerErroresPaso1SinDuplicar(resultado);
            var erroresPaso2 = resultado.Paso2?.Errors ?? new List<SICXEError>();
            var errores = new List<SICXEError>();

            if (mode == ExecutionMode.Ensamblado)
                return UnificarErrores(resultado);

            errores.AddRange(erroresAnalizador);
            errores.AddRange(erroresPaso1);

            if (mode == ExecutionMode.Paso2)
                errores.AddRange(erroresPaso2);

            return errores
                .GroupBy(e => new { e.Line, e.Column, e.Message, e.Type })
                .Select(g => g.First())
                .OrderBy(e => e.Line)
                .ThenBy(e => e.Column)
                .ToList();
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
                resultado.Paso2.GeneratedModules,
                resultado.Paso1.ProgramName,
                resultado.Paso1.ProgramStartAddress,
                resultado.Paso1.ProgramSize,
                resultado.Paso1.ExecutionEntryPoint);

            string reportesObj = Path.Combine(ObtenerDirectorioProyecto(), "reportes_objeto");
            Directory.CreateDirectory(reportesObj);
            string csvObjeto = Path.Combine(reportesObj, $"{baseName}_OBJETO_{time}.csv");
            programaObjeto.ExportarACSV(csvObjeto);
        }

        private void ExportarTablaBloques(Paso1 paso1)
        {
            string reportesDir = Path.Combine(ObtenerDirectorioProyecto(), "reportes");
            Directory.CreateDirectory(reportesDir);

            string baseName = ObtenerNombreBaseFuente();
            string path = Path.Combine(reportesDir, $"{baseName}_TABBLOQUES_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

            var bloques = ConstruirResumenBloques(paso1);
            var sb = new StringBuilder();
            sb.AppendLine("TABLA DE BLOQUES");
            sb.AppendLine("Seccion\tTamSeccion\tNoBloque\tBloque\tLongitud\tDirIniRel");

            foreach (var b in bloques)
                sb.AppendLine($"{b.Seccion}\t{b.TotalSeccion:X4}\t{b.Numero}\t{b.Nombre}\t{b.Longitud:X4}\t{b.DirIniRel:X4}");

            File.WriteAllText(path, sb.ToString());
        }

        private List<BloqueResumen> ConstruirResumenBloques(Paso1 paso1)
        {
            var tabblk = paso1.GetBlockTableBySection();
            var totalesPorSeccion = tabblk.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.Sum(b => b.Length),
                StringComparer.OrdinalIgnoreCase);

            return tabblk
                .SelectMany(sec => sec.Value.Select(b => new BloqueResumen(
                    sec.Key,
                    totalesPorSeccion.TryGetValue(sec.Key, out var total) ? total : 0,
                    b.Number,
                    b.Name,
                    b.StartAddress,
                    b.Length)))
                .OrderBy(b => b.Seccion)
                .ThenBy(b => b.Numero)
                .ToList();
        }

        /// <summary>
        /// Crea DataTable para mostrar tabla de bloques (TABBLK): bloques USE con direcciones y longitudes.
        /// Columnas: Nombre del bloque, Numero, Direccion inicial, Longitud, Tipo (STANDARD/UNNAMED).
        /// </summary>
        /// <param name="bloques">Lista de resumenes de bloques del Paso 1</param>
        /// <returns>DataTable con informacion de todos los bloques USE utilizados en el programa</returns>
        private DataTable CrearTablaBloques(IReadOnlyList<BloqueResumen> bloques)
        {
            var tabla = new DataTable();
            tabla.Columns.Add("Seccion", typeof(string));
            tabla.Columns.Add("TamSeccion", typeof(string));
            tabla.Columns.Add("NoBloque", typeof(string));
            tabla.Columns.Add("Bloque", typeof(string));
            tabla.Columns.Add("Longitud", typeof(string));
            tabla.Columns.Add("DirIniRel", typeof(string));

            string? lastSection = null;
            bool first = true;
            foreach (var b in bloques)
            {
                // Insert blank separator row between sections for visual clarity
                if (!first && !string.Equals(lastSection, b.Seccion, StringComparison.OrdinalIgnoreCase))
                {
                    tabla.Rows.Add(DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value, DBNull.Value);
                }

                tabla.Rows.Add(b.Seccion, b.TotalSeccion.ToString("X4"), b.Numero.ToString(), b.Nombre, b.Longitud.ToString("X4"), b.DirIniRel.ToString("X4"));
                lastSection = b.Seccion;
                first = false;
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
                bool lastWasE = false;
                foreach (var r in registros)
                {
                    // Insertar separador visual entre secciones (después de E, antes de H siguiente)
                    if (lastWasE && r.StartsWith("H"))
                    {
                        sb.AppendLine("=====================================");
                    }

                    sb.AppendLine(r);
                    lastWasE = r.StartsWith("E");
                }
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
            public BloqueResumen(string seccion, int totalSeccion, int numero, string nombre, int dirIniRel, int longitud)
            {
                Seccion = seccion;
                TotalSeccion = totalSeccion;
                Numero = numero;
                Nombre = nombre;
                DirIniRel = dirIniRel;
                Longitud = longitud;
            }

            public string Seccion { get; }
            public int TotalSeccion { get; }
            public int Numero { get; }
            public string Nombre { get; }
            public int DirIniRel { get; }
            public int Longitud { get; }
        }

        private enum ExecutionMode
        {
            Paso1,
            Paso2,
            Ensamblado
        }
    }
}
