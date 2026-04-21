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
            this.codigoTextBox1 = new TextBox();
            label1 = new Label();
            groupBox1 = new GroupBox();
            this.ArchivoInterdataGridView1 = new DataGridView();
            groupBox2 = new GroupBox();
            groupBox3 = new GroupBox();
            this.TablaSimdataGridView2 = new DataGridView();
            groupBox4 = new GroupBox();
            this.RegistrostextBox2 = new TextBox();
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
            groupBox1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.ArchivoInterdataGridView1).BeginInit();
            groupBox2.SuspendLayout();
            groupBox3.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)this.TablaSimdataGridView2).BeginInit();
            groupBox4.SuspendLayout();
            groupBox5.SuspendLayout();
            menuStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // codigoTextBox1
            // 
            this.codigoTextBox1.Location = new Point(6, 26);
            this.codigoTextBox1.Multiline = true;
            this.codigoTextBox1.Name = "codigoTextBox1";
            this.codigoTextBox1.ScrollBars = ScrollBars.Both;
            this.codigoTextBox1.Size = new Size(238, 501);
            this.codigoTextBox1.TabIndex = 1;
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
            groupBox1.Controls.Add(this.codigoTextBox1);
            groupBox1.Location = new Point(15, 31);
            groupBox1.Name = "groupBox1";
            groupBox1.Size = new Size(250, 537);
            groupBox1.TabIndex = 3;
            groupBox1.TabStop = false;
            groupBox1.Text = "Programa Fuente";
            groupBox1.Enter += groupBox1_Enter;
            // 
            // ArchivoInterdataGridView1
            // 
            this.ArchivoInterdataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.ArchivoInterdataGridView1.Location = new Point(6, 26);
            this.ArchivoInterdataGridView1.Name = "ArchivoInterdataGridView1";
            this.ArchivoInterdataGridView1.RowHeadersWidth = 51;
            this.ArchivoInterdataGridView1.Size = new Size(726, 482);
            this.ArchivoInterdataGridView1.TabIndex = 4;
            // 
            // groupBox2
            // 
            groupBox2.Controls.Add(this.ArchivoInterdataGridView1);
            groupBox2.Location = new Point(271, 44);
            groupBox2.Name = "groupBox2";
            groupBox2.Size = new Size(754, 514);
            groupBox2.TabIndex = 5;
            groupBox2.TabStop = false;
            groupBox2.Text = "Archivo Intermedio";
            groupBox2.Enter += groupBox2_Enter;
            // 
            // groupBox3
            // 
            groupBox3.Controls.Add(this.TablaSimdataGridView2);
            groupBox3.Location = new Point(1031, 57);
            groupBox3.Name = "groupBox3";
            groupBox3.Size = new Size(338, 330);
            groupBox3.TabIndex = 6;
            groupBox3.TabStop = false;
            groupBox3.Text = "Tabla de simbolos";
            groupBox3.Enter += groupBox3_Enter;
            // 
            // TablaSimdataGridView2
            // 
            this.TablaSimdataGridView2.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.TablaSimdataGridView2.Location = new Point(6, 35);
            this.TablaSimdataGridView2.Name = "TablaSimdataGridView2";
            this.TablaSimdataGridView2.RowHeadersWidth = 51;
            this.TablaSimdataGridView2.Size = new Size(326, 289);
            this.TablaSimdataGridView2.TabIndex = 0;
            // 
            // groupBox4
            // 
            groupBox4.Controls.Add(this.RegistrostextBox2);
            groupBox4.Location = new Point(1031, 393);
            groupBox4.Name = "groupBox4";
            groupBox4.Size = new Size(338, 165);
            groupBox4.TabIndex = 8;
            groupBox4.TabStop = false;
            groupBox4.Text = "Registros";
            // 
            // RegistrostextBox2
            // 
            this.RegistrostextBox2.Location = new Point(6, 26);
            this.RegistrostextBox2.Multiline = true;
            this.RegistrostextBox2.Name = "RegistrostextBox2";
            this.RegistrostextBox2.ScrollBars = ScrollBars.Both;
            this.RegistrostextBox2.Size = new Size(318, 133);
            this.RegistrostextBox2.TabIndex = 0;
            // 
            // groupBox5
            // 
            groupBox5.Controls.Add(ErrortextBox3);
            groupBox5.Location = new Point(1375, 57);
            groupBox5.Name = "groupBox5";
            groupBox5.Size = new Size(250, 501);
            groupBox5.TabIndex = 9;
            groupBox5.TabStop = false;
            groupBox5.Text = "Problemas en el Código";
            groupBox5.Enter += groupBox5_Enter;
            // 
            // ErrortextBox3
            // 
            ErrortextBox3.Location = new Point(6, 26);
            ErrortextBox3.Multiline = true;
            ErrortextBox3.Name = "ErrortextBox3";
            ErrortextBox3.ScrollBars = ScrollBars.Both;
            ErrortextBox3.Size = new Size(238, 469);
            ErrortextBox3.TabIndex = 0;
            ErrortextBox3.TextChanged += textBox3_TextChanged;
            // 
            // menuStrip1
            // 
            menuStrip1.ImageScalingSize = new Size(20, 20);
            menuStrip1.Items.AddRange(new ToolStripItem[] { archivoToolStripMenuItem, ensamblarToolStripMenuItem, cargarToolStripMenuItem, simularToolStripMenuItem });
            menuStrip1.Location = new Point(0, 0);
            menuStrip1.Name = "menuStrip1";
            menuStrip1.Size = new Size(1671, 28);
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
            // 
            // simularToolStripMenuItem
            // 
            simularToolStripMenuItem.Name = "simularToolStripMenuItem";
            simularToolStripMenuItem.Size = new Size(73, 24);
            simularToolStripMenuItem.Text = "Simular";
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1671, 663);
            Controls.Add(groupBox5);
            Controls.Add(groupBox4);
            Controls.Add(groupBox3);
            Controls.Add(groupBox2);
            Controls.Add(groupBox1);
            Controls.Add(label1);
            Controls.Add(menuStrip1);
            MainMenuStrip = menuStrip1;
            Name = "Form1";
            Text = "ENSAMBLARODR SICXE";
            Load += Form1_Load;
            groupBox1.ResumeLayout(false);
            groupBox1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)this.ArchivoInterdataGridView1).EndInit();
            groupBox2.ResumeLayout(false);
            groupBox3.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)this.TablaSimdataGridView2).EndInit();
            groupBox4.ResumeLayout(false);
            groupBox4.PerformLayout();
            groupBox5.ResumeLayout(false);
            groupBox5.PerformLayout();
            menuStrip1.ResumeLayout(false);
            menuStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion
        private TextBox codigoTextBox1;
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
    }
}