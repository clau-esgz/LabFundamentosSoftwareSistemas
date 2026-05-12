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

        public CargadorL()
        {
            InitializeComponent();
            cargarProg.Click += CargarProg_Click;
            dataGridView2.SelectionChanged += DataGridView2_SelectionChanged;
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
                _modules = parser.ParseCSV(filePath);

                if (_modules.Count == 0)
                {
                    MessageBox.Show("No se encontraron módulos válidos en el archivo.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                ExecutePass1();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar el archivo: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ExecutePass1()
        {
            int initialAddr = 0;
            if (!string.IsNullOrWhiteSpace(DirCarga.Text))
            {
                int.TryParse(DirCarga.Text, System.Globalization.NumberStyles.HexNumber, null, out initialAddr);
            }

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

            // Seleccionar por defecto la sección que tenga dirección de ejecución
            var entryModule = _modules.FirstOrDefault(m => m.ExecutionAddress != 0) ?? _modules.FirstOrDefault();
            if (entryModule != null)
            {
                MessageBox.Show($"Se ha detectado '{entryModule.Name}' como sección de inicio por defecto. Puedes seleccionar otra en la tabla.", "Cargador", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            
            ExecutePass2();
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
                        int loadAddr = _loaderResult.Pass1.SectionLoadAddresses[module.Name];
                        int entryPoint = loadAddr + module.ExecutionAddress;
                        _loaderResult.ExecutionEntryPoint = entryPoint;
                        
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
            
            // Si el usuario no ha seleccionado manualmente, usamos el detectado
            if (_loaderResult.ExecutionEntryPoint == 0)
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

            // Determinar rango a mostrar (de la primera dirección de carga hasta el final del programa)
            int minAddr = _loaderResult!.Pass1.SectionLoadAddresses.Values.Min();
            int totalLen = _modules.Sum(m => m.Length);
            int maxAddr = minAddr + totalLen;

            // Redondear a múltiplo de 16
            int start = (minAddr / 16) * 16;
            int end = ((maxAddr + 15) / 16) * 16;

            // Limitar para no saturar el DataGridView si el programa es muy grande
            // pero mostrar al menos lo cargado.
            for (int addr = start; addr < end && addr < memoryManager.MaxMemorySize; addr += 16)
            {
                var row = new object[18];
                row[0] = addr.ToString("X6");

                var bytes = memoryManager.ReadMemory(addr, 16);
                var sbAscii = new StringBuilder();

                for (int i = 0; i < 16; i++)
                {
                    byte b = bytes[i];
                    row[i + 1] = b.ToString("X2");
                    
                    // ASCII legible
                    if (b >= 32 && b <= 126)
                        sbAscii.Append((char)b);
                    else
                        sbAscii.Append('.');
                }
                row[17] = sbAscii.ToString();
                dataGridView1.Rows.Add(row);
            }

            // Aplicar estilo monospaciado
            dataGridView1.DefaultCellStyle.Font = new Font("Courier New", 9);
            dataGridView1.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
        }

        private void UpdateStatus()
        {
            if (_loaderResult != null)
            {
                TamProg.Text = _modules.Sum(m => m.Length).ToString("X6");
                // Mostrar punto de ejecución actual
                label2.Text = $"Punto de Ejecución: {_loaderResult.ExecutionEntryPoint:X6}";
            }
        }
    }
}
