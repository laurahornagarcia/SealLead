namespace SealLead
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            textBox1 = new TextBox();
            LBLUrl = new Label();
            LBLProxy = new Label();
            textBoxProxy = new TextBox();
            checkBox1 = new CheckBox();
            Buscar = new Button();
            BTNExportar = new Button();
            LBLSector = new Label();
            CBSector = new ComboBox();
            LBLActividad = new Label();
            CBActividad = new ComboBox();
            LBLCnae = new Label();
            CBCnae = new ComboBox();
            LBLKeyword = new Label();
            CBKeyword = new ComboBox();
            LBLEmailStatus = new Label();
            CBEmailStatus = new ComboBox();
            LBLCompanyStatus = new Label();
            CBCompanyStatus = new ComboBox();
            LBLFiltroEmail = new Label();
            CBFiltroEmail = new ComboBox();
            BTNFiltrar = new Button();
            BTNExportarFiltrado = new Button();
            DGVEmpresas = new DataGridView();
            LBResumen = new Label();
            LBEstado = new Label();
            LBEstado2 = new Label();
            webView21 = new Microsoft.Web.WebView2.WinForms.WebView2();
            BTNParar = new Button();
            BTNCompletarDatos = new Button();
            BTNLimpiarSinEmail = new Button();
            LBLBuscarEmpresa = new Label();
            TXTBuscarEmpresa = new TextBox();
            LBLRetomar = new Label();
            CBBusquedasPendientes = new ComboBox();
            ((System.ComponentModel.ISupportInitialize)DGVEmpresas).BeginInit();
            ((System.ComponentModel.ISupportInitialize)webView21).BeginInit();
            SuspendLayout();
            // 
            // textBox1
            // 
            textBox1.Location = new Point(42, 42);
            textBox1.Name = "textBox1";
            textBox1.Size = new Size(520, 23);
            textBox1.TabIndex = 0;
            // 
            // LBLUrl
            // 
            LBLUrl.AutoSize = true;
            LBLUrl.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLUrl.Location = new Point(10, 46);
            LBLUrl.Name = "LBLUrl";
            LBLUrl.Size = new Size(33, 15);
            LBLUrl.TabIndex = 25;
            LBLUrl.Text = "URL:";
            // 
            // LBLProxy
            // 
            LBLProxy.AutoSize = true;
            LBLProxy.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLProxy.Location = new Point(570, 46);
            LBLProxy.Name = "LBLProxy";
            LBLProxy.Size = new Size(42, 15);
            LBLProxy.TabIndex = 24;
            LBLProxy.Text = "Proxy:";
            // 
            // textBoxProxy
            // 
            textBoxProxy.Location = new Point(610, 42);
            textBoxProxy.Name = "textBoxProxy";
            textBoxProxy.PlaceholderText = "http://user:pass@host:port";
            textBoxProxy.Size = new Size(190, 23);
            textBoxProxy.TabIndex = 1;
            // 
            // checkBox1
            // 
            checkBox1.AutoSize = true;
            checkBox1.Checked = true;
            checkBox1.CheckState = CheckState.Checked;
            checkBox1.Location = new Point(808, 44);
            checkBox1.Name = "checkBox1";
            checkBox1.Size = new Size(104, 19);
            checkBox1.TabIndex = 2;
            checkBox1.Text = "Solo con email";
            checkBox1.UseVisualStyleBackColor = true;
            // 
            // Buscar
            // 
            Buscar.Location = new Point(908, 41);
            Buscar.Name = "Buscar";
            Buscar.Size = new Size(100, 26);
            Buscar.TabIndex = 3;
            Buscar.Text = "Iniciar búsqueda";
            Buscar.UseVisualStyleBackColor = true;
            Buscar.Click += Buscar_ClickAsync;
            // 
            // BTNExportar
            // 
            BTNExportar.Location = new Point(1076, 41);
            BTNExportar.Name = "BTNExportar";
            BTNExportar.Size = new Size(70, 26);
            BTNExportar.TabIndex = 4;
            BTNExportar.Text = "Exportar todo";
            BTNExportar.UseVisualStyleBackColor = true;
            BTNExportar.Click += BTNExportar_Click;
            // 
            // LBLSector
            // 
            LBLSector.AutoSize = true;
            LBLSector.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLSector.Location = new Point(966, 240);
            LBLSector.Name = "LBLSector";
            LBLSector.Size = new Size(44, 15);
            LBLSector.TabIndex = 23;
            LBLSector.Text = "Sector";
            // 
            // CBSector
            // 
            CBSector.DropDownStyle = ComboBoxStyle.DropDownList;
            CBSector.Location = new Point(966, 256);
            CBSector.Name = "CBSector";
            CBSector.Size = new Size(398, 23);
            CBSector.TabIndex = 4;
            // 
            // LBLActividad
            // 
            LBLActividad.AutoSize = true;
            LBLActividad.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLActividad.Location = new Point(966, 293);
            LBLActividad.Name = "LBLActividad";
            LBLActividad.Size = new Size(59, 15);
            LBLActividad.TabIndex = 22;
            LBLActividad.Text = "Actividad";
            // 
            // CBActividad
            // 
            CBActividad.DropDownStyle = ComboBoxStyle.DropDownList;
            CBActividad.Location = new Point(966, 309);
            CBActividad.Name = "CBActividad";
            CBActividad.Size = new Size(398, 23);
            CBActividad.TabIndex = 5;
            // 
            // LBLCnae
            // 
            LBLCnae.AutoSize = true;
            LBLCnae.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLCnae.Location = new Point(966, 344);
            LBLCnae.Name = "LBLCnae";
            LBLCnae.Size = new Size(37, 15);
            LBLCnae.TabIndex = 21;
            LBLCnae.Text = "CNAE";
            // 
            // CBCnae
            // 
            CBCnae.DropDownStyle = ComboBoxStyle.DropDownList;
            CBCnae.Location = new Point(966, 360);
            CBCnae.Name = "CBCnae";
            CBCnae.Size = new Size(398, 23);
            CBCnae.TabIndex = 6;
            // 
            // LBLKeyword
            // 
            LBLKeyword.AutoSize = true;
            LBLKeyword.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLKeyword.Location = new Point(966, 406);
            LBLKeyword.Name = "LBLKeyword";
            LBLKeyword.Size = new Size(57, 15);
            LBLKeyword.TabIndex = 20;
            LBLKeyword.Text = "Keyword";
            // 
            // CBKeyword
            // 
            CBKeyword.DropDownStyle = ComboBoxStyle.DropDownList;
            CBKeyword.Location = new Point(966, 422);
            CBKeyword.Name = "CBKeyword";
            CBKeyword.Size = new Size(398, 23);
            CBKeyword.TabIndex = 7;
            // 
            // LBLEmailStatus
            // 
            LBLEmailStatus.AutoSize = true;
            LBLEmailStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLEmailStatus.Location = new Point(966, 463);
            LBLEmailStatus.Name = "LBLEmailStatus";
            LBLEmailStatus.Size = new Size(83, 15);
            LBLEmailStatus.TabIndex = 18;
            LBLEmailStatus.Text = "Email enviado";
            // 
            // CBEmailStatus
            // 
            CBEmailStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            CBEmailStatus.Location = new Point(966, 479);
            CBEmailStatus.Name = "CBEmailStatus";
            CBEmailStatus.Size = new Size(398, 23);
            CBEmailStatus.TabIndex = 8;
            // 
            // LBLCompanyStatus
            // 
            LBLCompanyStatus.AutoSize = true;
            LBLCompanyStatus.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLCompanyStatus.Location = new Point(966, 527);
            LBLCompanyStatus.Name = "LBLCompanyStatus";
            LBLCompanyStatus.Size = new Size(94, 15);
            LBLCompanyStatus.TabIndex = 17;
            LBLCompanyStatus.Text = "Estado empresa";
            // 
            // CBCompanyStatus
            // 
            CBCompanyStatus.DropDownStyle = ComboBoxStyle.DropDownList;
            CBCompanyStatus.Location = new Point(966, 543);
            CBCompanyStatus.Name = "CBCompanyStatus";
            CBCompanyStatus.Size = new Size(398, 23);
            CBCompanyStatus.TabIndex = 9;
            // 
            // LBLFiltroEmail
            // 
            LBLFiltroEmail.AutoSize = true;
            LBLFiltroEmail.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLFiltroEmail.Location = new Point(10, 72);
            LBLFiltroEmail.Name = "LBLFiltroEmail";
            LBLFiltroEmail.Size = new Size(68, 15);
            LBLFiltroEmail.TabIndex = 19;
            LBLFiltroEmail.Text = "Filtro Email";
            // 
            // CBFiltroEmail
            // 
            CBFiltroEmail.DropDownStyle = ComboBoxStyle.DropDownList;
            CBFiltroEmail.Items.AddRange(new object[] { "Todos", "Con email", "Sin email" });
            CBFiltroEmail.Location = new Point(10, 88);
            CBFiltroEmail.Name = "CBFiltroEmail";
            CBFiltroEmail.Size = new Size(120, 23);
            CBFiltroEmail.TabIndex = 11;
            CBFiltroEmail.SelectedIndexChanged += CBFiltroEmail_SelectedIndexChanged;
            // 
            // BTNFiltrar
            // 
            BTNFiltrar.Location = new Point(979, 153);
            BTNFiltrar.Name = "BTNFiltrar";
            BTNFiltrar.Size = new Size(70, 23);
            BTNFiltrar.TabIndex = 10;
            BTNFiltrar.Text = "Filtrar";
            BTNFiltrar.UseVisualStyleBackColor = true;
            BTNFiltrar.Click += BTNFiltrar_Click;
            // 
            // BTNExportarFiltrado
            // 
            BTNExportarFiltrado.Location = new Point(1065, 153);
            BTNExportarFiltrado.Name = "BTNExportarFiltrado";
            BTNExportarFiltrado.Size = new Size(70, 23);
            BTNExportarFiltrado.TabIndex = 11;
            BTNExportarFiltrado.Text = "Exportar";
            BTNExportarFiltrado.UseVisualStyleBackColor = true;
            BTNExportarFiltrado.Click += BTNExportarFiltrado_Click;
            // 
            // DGVEmpresas
            // 
            DGVEmpresas.AllowUserToAddRows = false;
            DGVEmpresas.AllowUserToDeleteRows = false;
            DGVEmpresas.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            DGVEmpresas.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            DGVEmpresas.Font = new Font("Segoe UI", 8F);
            DGVEmpresas.Location = new Point(10, 115);
            DGVEmpresas.Name = "DGVEmpresas";
            DGVEmpresas.ReadOnly = true;
            DGVEmpresas.RowHeadersVisible = false;
            DGVEmpresas.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            DGVEmpresas.Size = new Size(950, 180);
            DGVEmpresas.TabIndex = 12;
            // 
            // LBResumen
            // 
            LBResumen.Font = new Font("Segoe UI", 8F);
            LBResumen.ForeColor = Color.DimGray;
            LBResumen.Location = new Point(10, 114);
            LBResumen.Name = "LBResumen";
            LBResumen.Size = new Size(1120, 15);
            LBResumen.TabIndex = 15;
            // 
            // LBEstado
            // 
            LBEstado.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBEstado.ForeColor = Color.DarkSlateBlue;
            LBEstado.Location = new Point(10, 758);
            LBEstado.Name = "LBEstado";
            LBEstado.Size = new Size(1120, 18);
            LBEstado.TabIndex = 13;
            // 
            // LBEstado2
            // 
            LBEstado2.Font = new Font("Segoe UI", 8F);
            LBEstado2.ForeColor = Color.SlateGray;
            LBEstado2.Location = new Point(10, 740);
            LBEstado2.Name = "LBEstado2";
            LBEstado2.Size = new Size(1120, 18);
            LBEstado2.TabIndex = 16;
            // 
            // webView21
            // 
            webView21.AllowExternalDrop = true;
            webView21.CreationProperties = null;
            webView21.DefaultBackgroundColor = Color.White;
            webView21.Location = new Point(10, 301);
            webView21.Name = "webView21";
            webView21.Size = new Size(950, 429);
            webView21.TabIndex = 21;
            webView21.ZoomFactor = 1D;
            // 
            // BTNParar
            // 
            BTNParar.BackColor = Color.LightCoral;
            BTNParar.Enabled = false;
            BTNParar.Location = new Point(1012, 41);
            BTNParar.Name = "BTNParar";
            BTNParar.Size = new Size(60, 26);
            BTNParar.TabIndex = 22;
            BTNParar.Text = "Parar";
            BTNParar.UseVisualStyleBackColor = false;
            BTNParar.Click += BTNParar_Click;
            // 
            // BTNCompletarDatos
            // 
            BTNCompletarDatos.BackColor = Color.LightBlue;
            BTNCompletarDatos.Location = new Point(136, 86);
            BTNCompletarDatos.Name = "BTNCompletarDatos";
            BTNCompletarDatos.Size = new Size(70, 26);
            BTNCompletarDatos.TabIndex = 23;
            BTNCompletarDatos.Text = "Completar";
            BTNCompletarDatos.UseVisualStyleBackColor = false;
            BTNCompletarDatos.Click += BTNCompletarDatos_Click;
            // 
            // BTNLimpiarSinEmail
            // 
            BTNLimpiarSinEmail.BackColor = Color.LightSalmon;
            BTNLimpiarSinEmail.Location = new Point(212, 86);
            BTNLimpiarSinEmail.Name = "BTNLimpiarSinEmail";
            BTNLimpiarSinEmail.Size = new Size(80, 23);
            BTNLimpiarSinEmail.TabIndex = 24;
            BTNLimpiarSinEmail.Text = "Buscar";
            BTNLimpiarSinEmail.UseVisualStyleBackColor = false;
            BTNLimpiarSinEmail.Click += BTNLimpiarSinEmail_Click;
            // 
            // LBLBuscarEmpresa
            // 
            LBLBuscarEmpresa.AutoSize = true;
            LBLBuscarEmpresa.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLBuscarEmpresa.Location = new Point(305, 72);
            LBLBuscarEmpresa.Name = "LBLBuscarEmpresa";
            LBLBuscarEmpresa.Size = new Size(98, 15);
            LBLBuscarEmpresa.TabIndex = 25;
            LBLBuscarEmpresa.Text = "Buscar empresa:";
            // 
            // TXTBuscarEmpresa
            // 
            TXTBuscarEmpresa.Location = new Point(305, 88);
            TXTBuscarEmpresa.Name = "TXTBuscarEmpresa";
            TXTBuscarEmpresa.PlaceholderText = "Buscar por nombre...";
            TXTBuscarEmpresa.Size = new Size(250, 23);
            TXTBuscarEmpresa.TabIndex = 26;
            TXTBuscarEmpresa.TextChanged += TXTBuscarEmpresa_TextChanged;
            //
            // LBLRetomar
            //
            LBLRetomar.AutoSize = true;
            LBLRetomar.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            LBLRetomar.Location = new Point(10, 16);
            LBLRetomar.Name = "LBLRetomar";
            LBLRetomar.TabIndex = 27;
            LBLRetomar.Text = "Retomar:";
            //
            // CBBusquedasPendientes
            //
            CBBusquedasPendientes.DropDownStyle = ComboBoxStyle.DropDownList;
            CBBusquedasPendientes.Location = new Point(80, 12);
            CBBusquedasPendientes.Name = "CBBusquedasPendientes";
            CBBusquedasPendientes.Size = new Size(820, 23);
            CBBusquedasPendientes.TabIndex = 28;
            CBBusquedasPendientes.SelectedIndexChanged += CBBusquedasPendientes_SelectedIndexChanged;
            //
            // Form1
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1438, 790);
            Controls.Add(LBEstado);
            Controls.Add(LBEstado2);
            Controls.Add(LBResumen);
            Controls.Add(DGVEmpresas);
            Controls.Add(BTNExportarFiltrado);
            Controls.Add(BTNFiltrar);
            Controls.Add(CBCompanyStatus);
            Controls.Add(LBLCompanyStatus);
            Controls.Add(CBEmailStatus);
            Controls.Add(LBLEmailStatus);
            Controls.Add(CBFiltroEmail);
            Controls.Add(LBLFiltroEmail);
            Controls.Add(CBKeyword);
            Controls.Add(LBLKeyword);
            Controls.Add(CBCnae);
            Controls.Add(LBLCnae);
            Controls.Add(CBActividad);
            Controls.Add(LBLActividad);
            Controls.Add(CBSector);
            Controls.Add(LBLSector);
            Controls.Add(BTNExportar);
            Controls.Add(Buscar);
            Controls.Add(checkBox1);
            Controls.Add(textBoxProxy);
            Controls.Add(LBLProxy);
            Controls.Add(textBox1);
            Controls.Add(LBLUrl);
            Controls.Add(webView21);
            Controls.Add(BTNParar);
            Controls.Add(BTNCompletarDatos);
            Controls.Add(BTNLimpiarSinEmail);
            Controls.Add(LBLBuscarEmpresa);
            Controls.Add(TXTBuscarEmpresa);
            Controls.Add(LBLRetomar);
            Controls.Add(CBBusquedasPendientes);
            Name = "Form1";
            Text = "SealLead";
            ((System.ComponentModel.ISupportInitialize)DGVEmpresas).EndInit();
            ((System.ComponentModel.ISupportInitialize)webView21).EndInit();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private TextBox textBox1;
        private Label LBLUrl;
        private Label LBLProxy;
        private TextBox textBoxProxy;
        private CheckBox checkBox1;
        private Button Buscar;
        private Button BTNExportar;
        private Label LBLSector;
        private ComboBox CBSector;
        private Label LBLActividad;
        private ComboBox CBActividad;
        private Label LBLCnae;
        private ComboBox CBCnae;
        private Label LBLKeyword;
        private ComboBox CBKeyword;
        private Label LBLEmailStatus;
        private ComboBox CBEmailStatus;
        private Label LBLCompanyStatus;
        private ComboBox CBCompanyStatus;
        private Button BTNFiltrar;
        private Button BTNExportarFiltrado;
        private DataGridView DGVEmpresas;
        private Label LBResumen;
        private Label LBEstado2;
        private Label LBEstado;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView21;
        private Button BTNParar;
        private Button BTNCompletarDatos;
        private Button BTNLimpiarSinEmail;
        private Label LBLFiltroEmail;
        private ComboBox CBFiltroEmail;
        private Label LBLBuscarEmpresa;
        private TextBox TXTBuscarEmpresa;
        private Label LBLRetomar;
        private ComboBox CBBusquedasPendientes;
    }
}
