using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using laboratorioPractica3.Loader;

namespace laboratorioPractica3
{
    public partial class CargadorL : Form
    {
        private List<ObjectModuleParsed> _modules = new();
        private LoaderResult? _loaderResult;
        private readonly LoaderLinker _linker = new();
        private MemoryManager? _memoryManager;
        private ObjectModuleParsed? _selectedStartModule;

        public CargadorL()
        {
            InitializeComponent();
            if (string.IsNullOrWhiteSpace(DirCarga.Text))
                DirCarga.Text = "0000";
            cargarProg.Click += CargarProg_Click;
            btnNuevo.Click += BtnNuevo_Click;
            dataGridView2.SelectionChanged += DataGridView2_SelectionChanged;
            programaInicioComboBox.SelectedIndexChanged += ProgramaInicioComboBox_SelectedIndexChanged;
        }

        private void BtnNuevo_Click(object? sender, EventArgs e)
        {
            _modules.Clear();
            _loaderResult = null;
            _memoryManager = null;
            _selectedStartModule = null;

            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();
            dataGridView2.Rows.Clear();
            TamProg.Clear();
            label2.Text = "Punto Ej: 000000 | Final: 000000";

            programaInicioComboBox.SelectedIndexChanged -= ProgramaInicioComboBox_SelectedIndexChanged;
            programaInicioComboBox.DataSource = null;
            programaInicioComboBox.SelectedIndexChanged += ProgramaInicioComboBox_SelectedIndexChanged;
        }

        private void CargarProg_Click(object sender, EventArgs e)
        {
            using var ofd = new OpenFileDialog
            {
                Title = "Seleccionar Archivo Objeto (.obj, .csv, .txt)",
                Filter = "Archivos Objeto (*.obj;*.csv;*.txt)|*.obj;*.csv;*.txt|Todos los archivos (*.*)|*.*"
            };

            if (ofd.ShowDialog() == DialogResult.OK)
            {
                LoadFile(ofd.FileName);
            }
        }

        private void LoadFile(string filePath)
        {
            try
            {
                var parser = new ObjectProgramParser();
                var newModules = parser.ParseCSV(filePath);

                if (newModules.Count == 0)
                {
                    MessageBox.Show("No se encontraron módulos válidos en el archivo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                _modules.AddRange(newModules);

                ExecutePass1();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecutePass1()
        {
            int initialAddr = ParseHexAddress(DirCarga.Text);

            var tabse = new ExternalSymbolTable();
            var pass1Result = new LoaderPass1().Execute(_modules, initialAddr, tabse);

            _loaderResult = new LoaderResult
            {
                Pass1 = pass1Result
            };

            UpdateUIWithPass1();
        }

        private void UpdateUIWithPass1()
        {
            dataGridView2.Rows.Clear();
            foreach (var module in _modules)
            {
                int addr = _loaderResult!.Pass1.SectionLoadAddresses[module.Name];
                dataGridView2.Rows.Add(module.Name, "", addr.ToString("X6"), module.Length.ToString("X6"));

                foreach (var def in module.Definitions)
                {
                    int absAddr = addr + def.RelativeAddress;
                    dataGridView2.Rows.Add("", def.Symbol, absAddr.ToString("X6"), "");
                }
            }

            RefrescarSelectorProgramaInicio();
            ExecutePass2();
        }

        private void RefrescarSelectorProgramaInicio()
        {
            var previousSelection = _selectedStartModule;

            var opciones = _modules
                .Select((module, index) => new ProgramaInicioOption(index + 1, module))
                .ToList();

            programaInicioComboBox.SelectedIndexChanged -= ProgramaInicioComboBox_SelectedIndexChanged;
            programaInicioComboBox.DataSource = null;
            programaInicioComboBox.DisplayMember = nameof(ProgramaInicioOption.Label);
            programaInicioComboBox.ValueMember = nameof(ProgramaInicioOption.Module);
            programaInicioComboBox.DataSource = opciones;

            if (previousSelection != null)
            {
                int index = opciones.FindIndex(o => ReferenceEquals(o.Module, previousSelection));
                if (index >= 0)
                    programaInicioComboBox.SelectedIndex = index;
            }

            if (programaInicioComboBox.SelectedIndex < 0 && opciones.Count > 0)
                programaInicioComboBox.SelectedIndex = 0;

            programaInicioComboBox.SelectedIndexChanged += ProgramaInicioComboBox_SelectedIndexChanged;
        }

        private void ProgramaInicioComboBox_SelectedIndexChanged(object? sender, EventArgs e)
        {
            if (programaInicioComboBox.SelectedItem is ProgramaInicioOption option)
            {
                _selectedStartModule = option.Module;

                if (_loaderResult != null)
                {
                    _loaderResult.ExecutionEntryPoint = CalcularPuntoInicio(_selectedStartModule);
                    UpdateStatus();
                }
            }
        }

        private void DataGridView2_SelectionChanged(object sender, EventArgs e)
        {
            if (dataGridView2.SelectedRows.Count > 0)
            {
                var row = dataGridView2.SelectedRows[0];
                string? sectionName = row.Cells["seccCont"].Value?.ToString();
                
                if (!string.IsNullOrEmpty(sectionName))
                {
                    // El usuario seleccionó una sección de control para empezar
                    var module = _modules.FirstOrDefault(m => m.Name == sectionName);
                    if (module != null && _loaderResult != null)
                    {
                        SeleccionarModuloInicio(module);
                        
                        if (_memoryManager != null)
                            UpdateMemoryMap(_memoryManager);

                        UpdateStatus();
                    }
                }
            }
        }

        private void ExecutePass2()
        {
            if (_loaderResult == null) return;

            _memoryManager = new MemoryManager();
            var tabse = new ExternalSymbolTable();
            // Llenar TABSE desde Pass 1
            foreach (var entry in _loaderResult.Pass1.SymbolTable.Values)
            {
                tabse.TryAdd(entry.Name, entry.Address, entry.ControlSectionName, out _);
            }

            var pass2 = new LoaderPass2();
            int entryPoint = pass2.Execute(_loaderResult.Pass1, _modules, tabse, _memoryManager, _loaderResult.AllErrors);
            
            // Si el usuario no ha seleccionado manualmente, usamos el módulo elegido o el detectado.
            if (_selectedStartModule != null)
                _loaderResult.ExecutionEntryPoint = CalcularPuntoInicio(_selectedStartModule);
            else if (_loaderResult.ExecutionEntryPoint == 0)
                _loaderResult.ExecutionEntryPoint = entryPoint;

            _loaderResult.IsSuccessful = _loaderResult.AllErrors.Count == 0;
            
            UpdateMemoryMap(_memoryManager);
            UpdateStatus();
        }

        private void UpdateMemoryMap(MemoryManager memoryManager)
        {
            dataGridView1.Rows.Clear();
            dataGridView1.Columns.Clear();

            // Configurar columnas: Dirección + 16 bytes + ASCII
            dataGridView1.Columns.Add("addr", "Dirección");
            for (int i = 0; i < 16; i++)
            {
                dataGridView1.Columns.Add($"b{i:X}", i.ToString("X"));
            }
            dataGridView1.Columns.Add("ascii", "ASCII");

            // Recolectar todas las direcciones importantes (Inicios de sección y símbolos)
            var sectionAddresses = new HashSet<int>(_loaderResult!.Pass1.SectionLoadAddresses.Values);
            var symbolAddresses = new HashSet<int>(_loaderResult!.Pass1.SymbolTable.Values.Select(s => s.Address));

            // Rango a mostrar
            int minAddr = _loaderResult!.Pass1.SectionLoadAddresses.Values.Count > 0 
                ? _loaderResult!.Pass1.SectionLoadAddresses.Values.Min() 
                : 0;
            
            // La dirección final es la mayor dirección escrita o la última sección + su longitud
            int maxWrittenAddr = 0;
            if (_modules.Count > 0)
            {
                foreach(var module in _modules)
                {
                    if (_loaderResult.Pass1.SectionLoadAddresses.TryGetValue(module.Name, out int loadAddr))
                    {
                        int endAddr = loadAddr + module.Length;
                        if (endAddr > maxWrittenAddr) maxWrittenAddr = endAddr;
                    }
                }
            }
            
            int start = (minAddr / 16) * 16;
            int end = ((maxWrittenAddr + 15) / 16) * 16;

            bool lastRowWasGap = false;

            for (int addr = start; addr < end && addr < memoryManager.MaxMemorySize; addr += 16)
            {
                bool isImportantRow = false;
                bool rowHasRelocation = false;
                bool rowHasSectionStart = false;
                bool rowHasSymbol = false;
                bool rowHasData = false;
                
                // Verificar qué hay en este bloque de 16
                for (int i = 0; i < 16; i++)
                {
                    int currentAddr = addr + i;
                    if (sectionAddresses.Contains(currentAddr)) rowHasSectionStart = true;
                    if (symbolAddresses.Contains(currentAddr)) rowHasSymbol = true;
                    if (memoryManager.IsRelocated(currentAddr)) rowHasRelocation = true;
                    if (memoryManager.IsWritten(currentAddr)) rowHasData = true;
                }

                // Una fila es importante si tiene un inicio de sección, un símbolo, una relocalización o simplemente datos cargados.
                isImportantRow = rowHasSectionStart || rowHasSymbol || rowHasRelocation || rowHasData;

                if (!isImportantRow)
                {
                    if (!lastRowWasGap)
                    {
                        int gapRowIndex = dataGridView1.Rows.Add("...", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "", "");
                        dataGridView1.Rows[gapRowIndex].DefaultCellStyle.ForeColor = Color.Gray;
                        lastRowWasGap = true;
                    }
                    continue;
                }

                lastRowWasGap = false;
                var row = new object[18];
                row[0] = addr.ToString("X6");

                var bytes = memoryManager.ReadMemory(addr, 16);
                var sbAscii = new StringBuilder();

                for (int i = 0; i < 16; i++)
                {
                    byte b = bytes[i];
                    row[i + 1] = b.ToString("X2");
                    
                    if (b >= 32 && b <= 126)
                        sbAscii.Append((char)b);
                    else
                        sbAscii.Append('.');
                }
                row[17] = sbAscii.ToString();
                
                int rowIndex = dataGridView1.Rows.Add(row);
                var gridRow = dataGridView1.Rows[rowIndex];

                // Prioridad de colores de fondo de la FILA
                if (rowHasSectionStart)
                {
                    gridRow.DefaultCellStyle.BackColor = Color.LightBlue; // Sección: Azul
                    gridRow.Cells[0].Style.Font = new Font(dataGridView1.Font, FontStyle.Bold);
                }
                else if (rowHasSymbol)
                {
                    gridRow.DefaultCellStyle.BackColor = Color.LightCyan; // Símbolo: Cian
                }

                // Resaltar celdas individuales relocalizadas
                for (int i = 0; i < 16; i++)
                {
                    if (memoryManager.IsRelocated(addr + i))
                    {
                        gridRow.Cells[i + 1].Style.BackColor = Color.Yellow;
                        gridRow.Cells[i + 1].Style.ForeColor = Color.Red;
                    }
                }
            }

            dataGridView1.DefaultCellStyle.Font = new Font("Courier New", 9);
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }

        private void UpdateStatus()
        {
            if (_loaderResult != null)
            {
                int totalLen = _modules.Sum(m => m.Length);
                int startAddr = _loaderResult.Pass1.SectionLoadAddresses.Values.Count > 0 
                    ? _loaderResult.Pass1.SectionLoadAddresses.Values.Min() 
                    : ParseHexAddress(DirCarga.Text);
                int finalAddr = startAddr + totalLen;

                TamProg.Text = totalLen.ToString("X6");
                
                // Mostrar punto de ejecución y dirección final
                label2.Text = $"Punto Ej: {_loaderResult.ExecutionEntryPoint:X6} | Final: {finalAddr:X6}";
            }
        }

        private void SeleccionarModuloInicio(ObjectModuleParsed module)
        {
            _selectedStartModule = module;

            var optionIndex = programaInicioComboBox.Items
                .Cast<object>()
                .OfType<ProgramaInicioOption>()
                .Select((option, index) => new { option, index })
                .FirstOrDefault(x => ReferenceEquals(x.option.Module, module))?.index;

            if (optionIndex.HasValue && programaInicioComboBox.SelectedIndex != optionIndex.Value)
                programaInicioComboBox.SelectedIndex = optionIndex.Value;

            if (_loaderResult != null)
            {
                _loaderResult.ExecutionEntryPoint = CalcularPuntoInicio(module);
                UpdateStatus();
            }
        }

        private int CalcularPuntoInicio(ObjectModuleParsed module)
        {
            if (_loaderResult == null)
                return 0;

            if (!_loaderResult.Pass1.SectionLoadAddresses.TryGetValue(module.Name, out int loadAddr))
                loadAddr = ParseHexAddress(DirCarga.Text);

            int offset = module.ExecutionAddress.HasValue ? module.ExecutionAddress.Value : module.StartAddress;
            return loadAddr + offset;
        }

        private static int ParseHexAddress(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            return int.TryParse(text.Trim(), System.Globalization.NumberStyles.HexNumber, null, out int value)
                ? value
                : 0;
        }

        private sealed class ProgramaInicioOption
        {
            public ProgramaInicioOption(int index, ObjectModuleParsed module)
            {
                Module = module;
                Label = $"{index}. {module.Name}";
            }

            public string Label { get; }
            public ObjectModuleParsed Module { get; }
            public override string ToString() => Label;
        }
    }
}
