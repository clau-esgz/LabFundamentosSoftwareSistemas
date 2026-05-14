namespace laboratorioPractica3
{
    partial class CargadorL
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            groupBox1 = new GroupBox();
            TamProg = new TextBox();
            DirCarga = new TextBox();
            label2 = new Label();
            label1 = new Label();
            label3 = new Label();
            programaInicioComboBox = new ComboBox();
            cargarProg = new Button();
            MapaMem = new GroupBox();
            dataGridView1 = new DataGridView();
            groupBox2 = new GroupBox();
            dataGridView2 = new DataGridView();
            seccCont = new DataGridViewTextBoxColumn();
            simCol = new DataGridViewTextBoxColumn();
            dirCol = new DataGridViewTextBoxColumn();
            longCol = new DataGridViewTextBoxColumn();
            groupBox1.SuspendLayout();
            MapaMem.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            groupBox2.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)dataGridView2).BeginInit();
            SuspendLayout();
            // 
            // groupBox1
            // 
            groupBox1.Controls.Add(TamProg);
            groupBox1.Controls.Add(DirCarga);
            groupBox1.Controls.Add(label2);
            groupBox1.Controls.Add(label1);
            groupBox1.Controls.Add(label3);
            groupBox1.Controls.Add(programaInicioComboBox);
            groupBox1.Controls.Add(cargarProg);
            groupBox1.Location = new Point(4, 2);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(784, 125);
            groupBox1.TabIndex = 0;
            groupBox1.TabStop = false;
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // TamProg
            // 
            TamProg.Location = new Point(379, 53);
            TamProg.Name = "TamProg";
            TamProg.Size = new Size(155, 27);
            TamProg.TabIndex = 4;
            // 
            // DirCarga
            // 
            DirCarga.Location = new Point(212, 53);
            DirCarga.Name = "DirCarga";
            DirCarga.Size = new Size(136, 27);
            DirCarga.TabIndex = 3;
            // 
            // label2
            // 
            label2.AutoSize = true;
            label2.Location = new Point(379, 23);
            label2.Name = "label2";
            label2.Size = new Size(155, 20);
            label2.TabIndex = 2;
            label2.Text = "Tamaño del Programa";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(212, 23);
            label1.Name = "label1";
            label1.Size = new Size(136, 20);
            label1.TabIndex = 1;
            label1.Text = "Dirección de Carga";
            // 
            // label3
            // 
            label3.AutoSize = true;
            label3.Location = new Point(545, 23);
            label3.Name = "label3";
            label3.Size = new Size(130, 20);
            label3.TabIndex = 5;
            label3.Text = "Programa inicio";
            // 
            // programaInicioComboBox
            // 
            programaInicioComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            programaInicioComboBox.FormattingEnabled = true;
            programaInicioComboBox.Location = new Point(545, 52);
            programaInicioComboBox.Name = "programaInicioComboBox";
            programaInicioComboBox.Size = new Size(221, 28);
            programaInicioComboBox.TabIndex = 6;
            // 
            // cargarProg
            // 
            cargarProg.Location = new Point(17, 40);
            cargarProg.Name = "cargarProg";
            cargarProg.Size = new Size(170, 52);
            cargarProg.TabIndex = 0;
            cargarProg.Text = "Cargar Programa ";
            cargarProg.UseVisualStyleBackColor = true;
            // 
            // MapaMem
            // 
            MapaMem.Controls.Add(dataGridView1);
            MapaMem.Location = new Point(12, 145);
            MapaMem.Name = "MapaMem";
            MapaMem.Size = new Size(778, 497);
            MapaMem.TabIndex = 1;
            MapaMem.TabStop = false;
            MapaMem.Text = "MapaMemoria";
            MapaMem.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            // 
            // dataGridView1
            // 
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Location = new Point(6, 26);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.Size = new Size(766, 456);
            dataGridView1.TabIndex = 0;
            dataGridView1.Dock = DockStyle.Fill;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(dataGridView2);
            groupBox2.Location = new Point(823, 12);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(524, 431);
            groupBox2.TabIndex = 2;
            groupBox2.TabStop = false;
            groupBox2.Text = "TabSE";
            groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Right;
            // 
            // dataGridView2
            // 
            dataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView2.Columns.AddRange(new DataGridViewColumn[] { seccCont, simCol, dirCol, longCol });
            dataGridView2.Location = new Point(4, 32);
            dataGridView2.Name = "dataGridView2";
            dataGridView2.RowHeadersWidth = 51;
            dataGridView2.Size = new Size(501, 188);
            dataGridView2.TabIndex = 0;
            dataGridView2.Dock = DockStyle.Fill;
            // 
            // seccCont
            // 
            seccCont.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            seccCont.HeaderText = "Seccion de control";
            seccCont.MinimumWidth = 6;
            seccCont.Name = "seccCont";
            seccCont.Width = 147;
            // 
            // simCol
            // 
            simCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            simCol.HeaderText = "Simbolo";
            simCol.MinimumWidth = 6;
            simCol.Name = "simCol";
            simCol.Width = 94;
            // 
            // dirCol
            // 
            dirCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            dirCol.HeaderText = "Direccion";
            dirCol.MinimumWidth = 6;
            dirCol.Name = "dirCol";
            dirCol.Width = 101;
            // 
            // longCol
            // 
            longCol.AutoSizeMode = DataGridViewAutoSizeColumnMode.ColumnHeader;
            longCol.HeaderText = "Longitud";
            longCol.MinimumWidth = 6;
            longCol.Name = "longCol";
            longCol.Width = 97;
            // 
            // CargadorL
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1359, 741);
            Controls.Add(groupBox2);
            Controls.Add(MapaMem);
            Controls.Add(groupBox1);
            Name = "CargadorL";
            Text = "Cargador";
            AutoScroll = true;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            MapaMem.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            groupBox2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)dataGridView2).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private GroupBox groupBox1;
        private Button cargarProg;
        private TextBox TamProg;
        private TextBox DirCarga;
        private Label label2;
        private Label label1;
        private Label label3;
        private ComboBox programaInicioComboBox;
        private GroupBox MapaMem;
        private DataGridView dataGridView1;
        private GroupBox groupBox2;
        private DataGridView dataGridView2;
        private DataGridViewTextBoxColumn seccCont;
        private DataGridViewTextBoxColumn simCol;
        private DataGridViewTextBoxColumn dirCol;
        private DataGridViewTextBoxColumn longCol;
    }
}