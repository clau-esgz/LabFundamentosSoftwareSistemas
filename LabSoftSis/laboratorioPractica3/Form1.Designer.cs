namespace laboratorioPractica3
{
    partial class Form1
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
            codigoTextBox1 = new RichTextBox();
            label1 = new Label();
            groupBox1 = new GroupBox();
            ArchivoInterdataGridView1 = new DataGridView();
            groupBox2 = new GroupBox();
            groupBox3 = new GroupBox();
            TablaSimdataGridView2 = new DataGridView();
            groupBox4 = new GroupBox();
            RegistrostextBox2 = new TextBox();
            groupBox5 = new GroupBox();
            ErrortextBox3 = new TextBox();
            menuStrip1 = new MenuStrip();
            archivoToolStripMenuItem = new ToolStripMenuItem();
            Nuevo = new ToolStripMenuItem();
            Abrir = new ToolStripMenuItem();
            toolStripSeparator1 = new ToolStripSeparator();
            Guardar = new ToolStripMenuItem();
            GuardarComoMenu = new ToolStripMenuItem();
            SalirStripSeparator2 = new ToolStripSeparator();
            salirToolStripMenuItem = new ToolStripMenuItem();
            ensamblarToolStripMenuItem = new ToolStripMenuItem();
            toolStripMenuItem1 = new ToolStripMenuItem();
            toolStripMenuItem2 = new ToolStripMenuItem();
            Paso2StripMenuItem3 = new ToolStripMenuItem();
            toolStripSeparator2 = new ToolStripSeparator();
            EnsambladoStripMenuItem4 = new ToolStripMenuItem();
            cargarToolStripMenuItem = new ToolStripMenuItem();
            simularToolStripMenuItem = new ToolStripMenuItem();
            groupBox6 = new GroupBox();
            tablaBlqsGridView1 = new DataGridView();
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)ArchivoInterdataGridView1).BeginInit();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)TablaSimdataGridView2).BeginInit();
            groupBox4.SuspendLayout();
            groupBox5.SuspendLayout();
            menuStrip1.SuspendLayout();
            groupBox6.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)tablaBlqsGridView1).BeginInit();
            SuspendLayout();
            // 
            // codigoTextBox1
            // 
            codigoTextBox1.Dock = DockStyle.Fill;
            codigoTextBox1.Location = new Point(3, 23);
            codigoTextBox1.Name = "codigoTextBox1";
            codigoTextBox1.Size = new Size(325, 511);
            codigoTextBox1.TabIndex = 1;
            codigoTextBox1.Text = "";
            // 
            // label1
            // 
            label1.AutoSize = true;
            label1.Location = new Point(15, 28);
            label1.Name = "label1";
            label1.Size = new Size(0, 20);
            label1.TabIndex = 2;
            // 
            // groupBox1
            // 
            groupBox1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left;
            groupBox1.Controls.Add(codigoTextBox1);
            groupBox1.Location = new Point(15, 31);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(331, 537);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Programa Fuente";
            // 
            // ArchivoInterdataGridView1
            // 
            ArchivoInterdataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            ArchivoInterdataGridView1.Dock = DockStyle.Fill;
            ArchivoInterdataGridView1.Location = new Point(3, 23);
            ArchivoInterdataGridView1.Name = "ArchivoInterdataGridView1";
            ArchivoInterdataGridView1.RowHeadersWidth = 51;
            ArchivoInterdataGridView1.Size = new Size(986, 498);
            ArchivoInterdataGridView1.TabIndex = 4;
            // 
            // groupBox2
            // 
            groupBox2.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox2.Controls.Add(ArchivoInterdataGridView1);
            groupBox2.Location = new Point(361, 44);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(992, 524);
            groupBox2.TabIndex = 5;
            groupBox2.TabStop = false;
            groupBox2.Text = "Archivo Intermedio";
            // 
            // groupBox3
            // 
            groupBox3.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox3.Controls.Add(TablaSimdataGridView2);
            groupBox3.Location = new Point(1359, 44);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(522, 480);
            groupBox3.TabIndex = 6;
            groupBox3.TabStop = false;
            groupBox3.Text = "Tabla de simbolos";
            // 
            // TablaSimdataGridView2
            // 
            TablaSimdataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            TablaSimdataGridView2.Dock = DockStyle.Fill;
            TablaSimdataGridView2.Location = new Point(3, 23);
            TablaSimdataGridView2.Name = "TablaSimdataGridView2";
            TablaSimdataGridView2.RowHeadersWidth = 51;
            TablaSimdataGridView2.Size = new Size(516, 454);
            TablaSimdataGridView2.TabIndex = 0;
            // 
            // groupBox4
            // 
            groupBox4.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            groupBox4.Controls.Add(RegistrostextBox2);
            groupBox4.Location = new Point(1362, 530);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(519, 280);
            groupBox4.TabIndex = 8;
            groupBox4.TabStop = false;
            groupBox4.Text = "Registros";
            // 
            // RegistrostextBox2
            // 
            RegistrostextBox2.Dock = DockStyle.Fill;
            RegistrostextBox2.Location = new Point(3, 23);
            RegistrostextBox2.Multiline = true;
            RegistrostextBox2.Name = "RegistrostextBox2";
            RegistrostextBox2.ScrollBars = ScrollBars.Both;
            RegistrostextBox2.Size = new Size(513, 254);
            RegistrostextBox2.TabIndex = 0;
            // 
            // groupBox5
            // 
            groupBox5.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            groupBox5.Controls.Add(ErrortextBox3);
            groupBox5.Location = new Point(619, 585);
            groupBox5.Name = "groupBox5";
            groupBox5.Size = new Size(697, 222);
            groupBox5.TabIndex = 9;
            groupBox5.TabStop = false;
            groupBox5.Text = "Problemas en el Código";
            // 
            // ErrortextBox3
            // 
            ErrortextBox3.Dock = DockStyle.Fill;
            ErrortextBox3.Location = new Point(3, 23);
            ErrortextBox3.Multiline = true;
            ErrortextBox3.Name = "ErrortextBox3";
            ErrortextBox3.ScrollBars = ScrollBars.Both;
            ErrortextBox3.Size = new Size(691, 196);
            ErrortextBox3.TabIndex = 0;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { archivoToolStripMenuItem, ensamblarToolStripMenuItem, cargarToolStripMenuItem, simularToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1893, 28);
            menuStrip1.TabIndex = 10;
            menuStrip1.Text = "menuStrip1";
            // 
            // archivoToolStripMenuItem
            // 
            archivoToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { Nuevo, Abrir, toolStripSeparator1, Guardar, GuardarComoMenu, SalirStripSeparator2, salirToolStripMenuItem });
            archivoToolStripMenuItem.Name = "archivoToolStripMenuItem";
            archivoToolStripMenuItem.Size = new Size(73, 24);
            archivoToolStripMenuItem.Text = "Archivo";
            // 
            // Nuevo
            // 
            Nuevo.Name = "Nuevo";
            Nuevo.Size = new Size(189, 26);
            Nuevo.Text = "Nuevo";
            Nuevo.Click += Nuevo_Click;
            // 
            // Abrir
            // 
            Abrir.Name = "Abrir";
            Abrir.Size = new Size(189, 26);
            Abrir.Text = "Abrir";
            Abrir.Click += Abrir_Click;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new Size(186, 6);
            // 
            // Guardar
            // 
            Guardar.Name = "Guardar";
            Guardar.Size = new Size(189, 26);
            Guardar.Text = "Guardar";
            Guardar.Click += Guardar_Click;
            // 
            // GuardarComoMenu
            // 
            GuardarComoMenu.Name = "GuardarComoMenu";
            GuardarComoMenu.Size = new Size(189, 26);
            GuardarComoMenu.Text = "Guardar Como";
            GuardarComoMenu.Click += GuardarComoMenu_Click;
            // 
            // SalirStripSeparator2
            // 
            SalirStripSeparator2.Name = "SalirStripSeparator2";
            SalirStripSeparator2.Size = new Size(186, 6);
            // 
            // salirToolStripMenuItem
            // 
            salirToolStripMenuItem.Name = "salirToolStripMenuItem";
            salirToolStripMenuItem.Size = new Size(189, 26);
            salirToolStripMenuItem.Text = "Salir";
            salirToolStripMenuItem.Click += salirToolStripMenuItem_Click;
            // 
            // ensamblarToolStripMenuItem
            // 
            ensamblarToolStripMenuItem.DropDownItems.AddRange(new ToolStripItem[] { toolStripMenuItem1, toolStripMenuItem2, Paso2StripMenuItem3, toolStripSeparator2, EnsambladoStripMenuItem4 });
            ensamblarToolStripMenuItem.Name = "ensamblarToolStripMenuItem";
            ensamblarToolStripMenuItem.Size = new Size(92, 24);
            ensamblarToolStripMenuItem.Text = "Ensamblar";
            // 
            // toolStripMenuItem1
            // 
            toolStripMenuItem1.Name = "toolStripMenuItem1";
            toolStripMenuItem1.Size = new Size(164, 26);
            toolStripMenuItem1.Text = "Analizador";
            toolStripMenuItem1.Click += toolStripMenuItem1_Click;
            // 
            // toolStripMenuItem2
            // 
            toolStripMenuItem2.Name = "toolStripMenuItem2";
            toolStripMenuItem2.Size = new Size(164, 26);
            toolStripMenuItem2.Text = "Paso 1";
            toolStripMenuItem2.Click += toolStripMenuItem2_Click;
            // 
            // Paso2StripMenuItem3
            // 
            Paso2StripMenuItem3.Name = "Paso2StripMenuItem3";
            Paso2StripMenuItem3.Size = new Size(164, 26);
            Paso2StripMenuItem3.Text = "Paso 2";
            Paso2StripMenuItem3.Click += toolStripMenuItem3_Click;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new Size(161, 6);
            // 
            // EnsambladoStripMenuItem4
            // 
            EnsambladoStripMenuItem4.Name = "EnsambladoStripMenuItem4";
            EnsambladoStripMenuItem4.Size = new Size(164, 26);
            EnsambladoStripMenuItem4.Text = "Ensamblar";
            EnsambladoStripMenuItem4.Click += toolStripMenuItem4_Click;
            // 
            // cargarToolStripMenuItem
            // 
            cargarToolStripMenuItem.Name = "cargarToolStripMenuItem";
            cargarToolStripMenuItem.Size = new Size(67, 24);
            cargarToolStripMenuItem.Text = "Cargar";
            cargarToolStripMenuItem.Click += cargarToolStripMenuItem_Click;
            // 
            // simularToolStripMenuItem
            // 
            simularToolStripMenuItem.Name = "simularToolStripMenuItem";
            simularToolStripMenuItem.Size = new Size(73, 24);
            simularToolStripMenuItem.Text = "Simular";
            // 
            // groupBox6
            // 
            groupBox6.Anchor = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            groupBox6.Controls.Add(tablaBlqsGridView1);
            groupBox6.Location = new Point(17, 585);
            groupBox6.Name = "groupBox6";
            groupBox6.Size = new Size(586, 225);
            groupBox6.TabIndex = 11;
            groupBox6.TabStop = false;
            groupBox6.Text = "Tabla de bloques";
            // 
            // tablaBlqsGridView1
            // 
            tablaBlqsGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            tablaBlqsGridView1.Dock = DockStyle.Fill;
            tablaBlqsGridView1.Location = new Point(3, 23);
            tablaBlqsGridView1.Name = "tablaBlqsGridView1";
            tablaBlqsGridView1.RowHeadersWidth = 51;
            tablaBlqsGridView1.Size = new Size(580, 199);
            tablaBlqsGridView1.TabIndex = 0;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1893, 822);
            Controls.Add(groupBox6);
            Controls.Add(groupBox5);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            MinimumSize = new Size(1000, 700);
            Name = "Form1";
            Text = "ENSAMBLARODR SICXE";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)ArchivoInterdataGridView1).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)TablaSimdataGridView2).EndInit();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBox5.ResumeLayout(false);
            groupBox5.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            groupBox6.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)tablaBlqsGridView1).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private RichTextBox codigoTextBox1;
        private Label label1;
        private GroupBox groupBox1;
        private DataGridView ArchivoInterdataGridView1;
        private GroupBox groupBox2;
        private GroupBox groupBox3;
        private DataGridView TablaSimdataGridView2;
        private GroupBox groupBox4;
        private TextBox RegistrostextBox2;
        private GroupBox groupBox5;
        private TextBox ErrortextBox3;
        private MenuStrip menuStrip1;
        private ToolStripMenuItem archivoToolStripMenuItem;
        private ToolStripMenuItem Nuevo;
        private ToolStripMenuItem Abrir;
        private ToolStripSeparator toolStripSeparator1;
        private ToolStripMenuItem Guardar;
        private ToolStripMenuItem GuardarComoMenu;
        private ToolStripSeparator SalirStripSeparator2;
        private ToolStripMenuItem salirToolStripMenuItem;
        private ToolStripMenuItem ensamblarToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem1;
        private ToolStripMenuItem cargarToolStripMenuItem;
        private ToolStripMenuItem simularToolStripMenuItem;
        private ToolStripMenuItem toolStripMenuItem2;
        private ToolStripMenuItem Paso2StripMenuItem3;
        private ToolStripSeparator toolStripSeparator2;
        private ToolStripMenuItem EnsambladoStripMenuItem4;
        private GroupBox groupBox6;
        private DataGridView tablaBlqsGridView1;
    }
}