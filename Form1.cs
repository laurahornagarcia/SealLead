using Microsoft.Web.WebView2.Core;
using SealLead.Data;
using SealLead.Services;
using SealScout;
using System.Data;
using System.Text.Json;

namespace SealLead
{
    public partial class Form1 : Form
    {
        private DataTable _currentTable = new();
        private CompanyScraperService? _scraper;
        private CancellationTokenSource? _cts;

        public Form1()
        {
            InitializeComponent();
            InicializarTabla();
            DGVEmpresas.DataSource = _currentTable;
            Load += async (_, _) =>
            {
                await CargarFiltrosAsync();
                await CargarBusquedasPendientesAsync();
                await CargarTablaInicialAsync();
                await InicializarWebViewAsync();
            };
        }

        private async Task InicializarWebViewAsync()
        {
            string udDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "SealLead", "WebView2");

            // Intento 1: sesión persistente normal
            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: udDir);
                await webView21.EnsureCoreWebView2Async(env);
                LogStatus2("Navegador listo.");
                return;
            }
            catch { }

            // Intento 2: matar procesos bloqueados y reintentar con carpeta nueva
            LogStatus2("Navegador bloqueado, liberando procesos...");
            try
            {
                foreach (var p in System.Diagnostics.Process.GetProcessesByName("msedgewebview2"))
                    try { p.Kill(true); } catch { }
                await Task.Delay(1500);

                if (Directory.Exists(udDir))
                    Directory.Delete(udDir, recursive: true);
            }
            catch { }

            try
            {
                var env = await CoreWebView2Environment.CreateAsync(userDataFolder: udDir);
                await webView21.EnsureCoreWebView2Async(env);
                LogStatus2("Navegador listo (sesión nueva).");
                return;
            }
            catch { }

            // Intento 3: sin carpeta persistente
            try
            {
                await webView21.EnsureCoreWebView2Async();
                LogStatus2("Navegador listo (sesión temporal).");
            }
            catch (Exception ex)
            {
                LogStatus2($"⚠ No se pudo iniciar el navegador: {ex.Message}");
            }
        }

        private void LogStatus2(string msg)
        {
            if (InvokeRequired)
                BeginInvoke((Delegate)(() => LBEstado2.Text = msg));
            else
                LBEstado2.Text = msg;
        }

        private void InicializarTabla()
        {
            _currentTable.Columns.Add("Empresa");
            _currentTable.Columns.Add("Email");
            _currentTable.Columns.Add("Estado Email");
            _currentTable.Columns.Add("Sector");
            _currentTable.Columns.Add("Actividad");
            _currentTable.Columns.Add("CNAE");
            _currentTable.Columns.Add("Dirección");
            _currentTable.Columns.Add("Razón Social");
            _currentTable.Columns.Add("Cif");
            _currentTable.Columns.Add("Forma Jurídica");
            _currentTable.Columns.Add("Keyword");
            _currentTable.Columns.Add("Estado Empresa");
            _currentTable.Columns.Add("URL");
        }

        private void ActualizarResumen()
        {
            int total = _currentTable.Rows.Count;
            int enviado = _currentTable.AsEnumerable()
                .Count(r => {
                    var v = r["Estado Email"]?.ToString();
                    return !string.IsNullOrWhiteSpace(v) && !v.Equals("Pendiente", StringComparison.OrdinalIgnoreCase);
                });
            int sinEnvio = total - enviado;
            int nueva = _currentTable.AsEnumerable()
                .Count(r => string.Equals(r["Estado Empresa"]?.ToString(), "Nueva", StringComparison.OrdinalIgnoreCase));
            int otros = total - nueva;

            LBResumen.Text = $"Total: {total}  |  Email enviado: {enviado}  |  Sin envío: {sinEnvio}  |  Estado 'Nueva': {nueva}  |  Otros estados: {otros}";
        }

        private async Task CargarTablaInicialAsync()
        {
            try
            {
                var svc = new CompanyQueryService(DatabaseService.ConnectionString);
                _currentTable = await svc.GetFilteredCompaniesAsync(
                    "Todos", "Todos", "Todos", "Todos", "Todos", "Todos");
                DGVEmpresas.DataSource = _currentTable;
                ActualizarResumen();
                LBEstado.Text = $"BD cargada: {_currentTable.Rows.Count} empresas";
            }
            catch (Exception ex)
            {
                LBEstado.Text = $"Error al cargar BD: {ex.Message}";
            }
        }

        private static string NormalizeForSearch(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var formD = s.Normalize(System.Text.NormalizationForm.FormD);
            var sb = new System.Text.StringBuilder(formD.Length);
            foreach (char c in formD)
            {
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) !=
                    System.Globalization.UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }
            return sb.ToString().ToLowerInvariant();
        }

        private void AplicarFiltroPorNombre()
        {
            string busqueda = NormalizeForSearch(TXTBuscarEmpresa.Text.Trim());
            if (string.IsNullOrEmpty(busqueda))
            {
                DGVEmpresas.DataSource = _currentTable;
                ActualizarResumen();
                return;
            }
            var filtrada = _currentTable.Clone();
            foreach (DataRow row in _currentTable.Rows)
            {
                string nombre = NormalizeForSearch(row["Empresa"]?.ToString() ?? "");
                if (nombre.Contains(busqueda))
                    filtrada.ImportRow(row);
            }
            DGVEmpresas.DataSource = filtrada;
            LBEstado.Text = $"Búsqueda '{TXTBuscarEmpresa.Text.Trim()}': {filtrada.Rows.Count} resultados";
        }

        private void TXTBuscarEmpresa_TextChanged(object sender, EventArgs e)
        {
            AplicarFiltroPorNombre();
        }

        // ── Búsquedas pendientes (retomar) ───────────────────────────────

        private record PendingSearch(int SearchId, string OriginalUrl, string Status, bool OnlyWithEmail, int Page, string CreatedAt)
        {
            public override string ToString() =>
                $"[{Status}] Pág.{Page}  ·  {OriginalUrl}  ({CreatedAt})";
        }

        private async Task CargarBusquedasPendientesAsync()
        {
            var lista = new List<PendingSearch>();
            await Task.Run(() =>
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(DatabaseService.ConnectionString);
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = @"
                    SELECT s.Id, s.OriginalUrl, s.Status, s.OnlyWithEmail,
                           COALESCE(sp.CurrentPage, 1) AS CurrentPage,
                           substr(s.CreatedAt, 1, 10)
                    FROM Searches s
                    LEFT JOIN SearchProgress sp ON sp.SearchId = s.Id
                    WHERE s.Status IN ('En curso', 'Detenida')
                    ORDER BY s.CreatedAt DESC
                    LIMIT 50;";
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    lista.Add(new PendingSearch(
                        reader.GetInt32(0),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetInt32(3) == 1,
                        reader.IsDBNull(4) ? 1 : reader.GetInt32(4),
                        reader.GetString(5)));
                }
            });

            CBBusquedasPendientes.SelectedIndexChanged -= CBBusquedasPendientes_SelectedIndexChanged;
            CBBusquedasPendientes.Items.Clear();
            CBBusquedasPendientes.Items.Add("-- Selecciona una búsqueda para retomar --");
            foreach (var p in lista)
                CBBusquedasPendientes.Items.Add(p);
            CBBusquedasPendientes.SelectedIndex = 0;
            CBBusquedasPendientes.SelectedIndexChanged += CBBusquedasPendientes_SelectedIndexChanged;
        }

        private void CBBusquedasPendientes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (CBBusquedasPendientes.SelectedItem is PendingSearch ps)
            {
                textBox1.Text = ps.OriginalUrl;
                checkBox1.Checked = ps.OnlyWithEmail;
            }
        }

        private async Task CargarFiltrosAsync()
        {
            var svc = new CompanyQueryService(DatabaseService.ConnectionString);

            var results = await Task.WhenAll(
                svc.GetDistinctValuesAsync("Sector"),
                svc.GetDistinctValuesAsync("Activity"),
                svc.GetDistinctValuesAsync("CnaeActivity"),
                svc.GetDistinctValuesAsync("SearchKeywords"),
                svc.GetDistinctValuesAsync("EmailStatus"),
                svc.GetDistinctValuesAsync("CompanyStatus"));

            void Fill(ComboBox cb, List<string> items)
            {
                cb.Items.Clear();
                cb.Items.AddRange(items.ToArray<object>());
                cb.SelectedIndex = 0;
            }

            Fill(CBSector,        results[0]);
            Fill(CBActividad,     results[1]);
            Fill(CBCnae,          results[2]);
            Fill(CBKeyword,       results[3]);
            Fill(CBEmailStatus,   results[4]);
            Fill(CBCompanyStatus, results[5]);
        }

        private async void Buscar_ClickAsync(object sender, EventArgs e)
        {
            string url = textBox1.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Introduce una URL.");
                return;
            }

            Buscar.Enabled = false;
            BTNExportar.Enabled = false;
            BTNParar.Enabled = true;
            BTNParar.Text = "Parar";
            _currentTable = new DataTable();
            InicializarTabla();
            DGVEmpresas.DataSource = _currentTable;
            LBEstado.Text = "Iniciando búsqueda...";

            _cts = new CancellationTokenSource();

            try
            {
                _scraper = new CompanyScraperService(
                    DatabaseService.ConnectionString,
                    webGetHtml: async u => await NavigateAndGetHtmlAsync(u));

                await _scraper.StartSearchAsync(
                    url,
                    checkBox1.Checked,
                    userId: 1,
                    log: msg => LogStatus2(msg),
                    onProgress: progress =>
                    {
                        Invoke((Delegate)(() =>
                        {
                            LBEstado.Text = $"Ubicación: {progress.CurrentLocality}  |  Pág: {progress.CurrentPage}  |  {progress.CurrentCompany}  |  Procesadas: {progress.TotalProcessed}  |  Nuevas: {progress.NewCompanies}";
                        }));
                    },
                    onCompanySaved: company =>
                    {
                        Invoke((Delegate)(() =>
                        {
                            _currentTable.Rows.InsertAt(_currentTable.NewRow(), 0);
                            var row = _currentTable.Rows[0];
                            row["Empresa"]        = company.CompanyName;
                            row["Email"]          = company.Email;
                            row["Estado Email"]   = "Pendiente";
                            row["Sector"]         = company.Sector;
                            row["Actividad"]      = company.Activity;
                            row["CNAE"]           = company.CnaeActivity;
                            row["Dirección"]      = company.Address;
                            row["Razón Social"]   = company.LegalName;
                            row["Cif"]            = company.Cif;
                            row["Forma Jurídica"] = company.LegalForm;
                            row["Keyword"]        = company.SearchKeywords;
                            row["Estado Empresa"] = "";
                            row["URL"]            = company.ProfileUrl;
                            ActualizarResumen();
                        }));
                    },
                    cancellationToken: _cts.Token);

                LBEstado.Text = $"Búsqueda finalizada. {_currentTable.Rows.Count} empresas nuevas en esta sesión.";
                ActualizarResumen();
                await CargarFiltrosAsync();
            }
            catch (OperationCanceledException)
            {
                LBEstado.Text = "Búsqueda detenida. Puedes reanudarla con la misma URL.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                Buscar.Enabled = true;
                BTNExportar.Enabled = true;
                BTNParar.Enabled = false;
                BTNParar.Text = "Parar";
                _scraper = null;
                _cts?.Dispose();
                _cts = null;
                await CargarBusquedasPendientesAsync();
            }
        }

        private void CBFiltroEmail_SelectedIndexChanged(object sender, EventArgs e)
        {
            string filtro = CBFiltroEmail.SelectedItem?.ToString() ?? "Todos";

            var tabla = _currentTable.Clone();
            tabla.Clear();

            foreach (DataRow row in _currentTable.Rows)
            {
                string email = row["Email"]?.ToString() ?? "";
                bool incluir = filtro switch
                {
                    "Con email" => !string.IsNullOrWhiteSpace(email),
                    "Sin email" => string.IsNullOrWhiteSpace(email),
                    _ => true
                };

                if (incluir) tabla.ImportRow(row);
            }

            DGVEmpresas.DataSource = tabla;
            LBEstado.Text = $"Filtrado: {filtro} | Registros: {tabla.Rows.Count}";
        }

        private void BTNParar_Click(object sender, EventArgs e)
        {
            _cts?.Cancel();
            BTNParar.Enabled = false;
            BTNParar.Text = "Parando...";
        }

        private async void BTNCompletarDatos_Click(object sender, EventArgs e)
        {
            Buscar.Enabled = false;
            BTNExportar.Enabled = false;
            BTNCompletarDatos.Enabled = false;
            BTNParar.Enabled = true;
            BTNParar.Text = "Parar";
            LBEstado.Text = "Completando datos de empresas sin email...";

            _cts = new CancellationTokenSource();

            try
            {
                _scraper = new CompanyScraperService(DatabaseService.ConnectionString,
                    webGetHtml: async u => await NavigateAndGetHtmlAsync(u));

                await _scraper.ReattemptIncompleteCompaniesAsync(
                    log: msg => LogStatus2(msg),
                    onProgress: progress =>
                    {
                        Invoke((Delegate)(() =>
                        {
                            LBEstado.Text = $"Completando: {progress.CurrentCompany} | Procesadas: {progress.TotalProcessed} | Actualizadas: {progress.NewCompanies}";
                        }));
                    },
                    onCompanySaved: company =>
                    {
                        LogStatus2($"Actualizada: {company.CompanyName} - {company.Email}");
                    },
                    cancellationToken: _cts.Token);

                LBEstado.Text = "Proceso de completamiento finalizado.";

                // Recargar tabla para mostrar los nuevos emails
                var svc = new CompanyQueryService(DatabaseService.ConnectionString);
                _currentTable = await svc.GetFilteredCompaniesAsync(
                    CBSector.SelectedItem?.ToString() ?? "Todos",
                    CBActividad.SelectedItem?.ToString() ?? "Todos",
                    CBCnae.SelectedItem?.ToString() ?? "Todos",
                    CBKeyword.SelectedItem?.ToString() ?? "Todos",
                    CBEmailStatus.SelectedItem?.ToString() ?? "Todos",
                    CBCompanyStatus.SelectedItem?.ToString() ?? "Todos");
                AplicarFiltroPorNombre();
                await CargarFiltrosAsync();
                CBFiltroEmail.SelectedIndex = 0;
            }
            catch (OperationCanceledException)
            {
                LBEstado.Text = "Completamiento cancelado.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
            }
            finally
            {
                Buscar.Enabled = true;
                BTNExportar.Enabled = true;
                BTNCompletarDatos.Enabled = true;
                BTNParar.Enabled = false;
                BTNParar.Text = "Parar";
                _scraper = null;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void BTNLimpiarSinEmail_Click(object sender, EventArgs e)
        {
            try
            {
                using var conn = new Microsoft.Data.Sqlite.SqliteConnection(DatabaseService.ConnectionString);
                conn.Open();

                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM Companies WHERE Email IS NULL OR Email = '';";
                long count = (long)cmd.ExecuteScalar();

                if (count == 0)
                {
                    MessageBox.Show("✓ No hay empresas sin email.", "Búsqueda");
                    LBEstado.Text = "No hay registros sin email en la BD.";
                }
                else
                {
                    MessageBox.Show($"Encontradas {count} empresas sin email.\n\nPresiona 'Completar' para intentar obtener los emails.", "Búsqueda");
                    LBEstado.Text = $"Encontradas {count} empresas sin email. Presiona 'Completar' para buscar los emails.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message, "Error");
            }
        }

        // ── WebView2: navegación y resolución de CAPTCHA ──────────────────

        private async Task<string> NavigateAndGetHtmlAsync(string url)
        {
            if (webView21.CoreWebView2 == null)
            {
                LogStatus2("⚠ El navegador no está disponible. Reinicia la aplicación.");
                throw new InvalidOperationException("WebView2 no inicializado. Cierra msedgewebview2.exe en el Administrador de tareas y reinicia la app.");
            }

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<CoreWebView2NavigationCompletedEventArgs> handler = null!;
            handler = (s, e) =>
            {
                webView21.NavigationCompleted -= handler;
                tcs.TrySetResult(true);
            };
            webView21.NavigationCompleted += handler;
            webView21.Source = new Uri(url);

            // Timeout de 30s para que nunca se quede colgado
            var timeout = Task.Delay(30000);
            if (await Task.WhenAny(tcs.Task, timeout) == timeout)
            {
                webView21.NavigationCompleted -= handler;
                LogStatus2("⚠ Tiempo de espera agotado cargando la página.");
            }

            string html = await GetWebViewHtmlAsync();
            return await ResolveIfCaptchaAsync(html);
        }

        private async Task<string> GetWebViewHtmlAsync()
        {
            string json = await webView21.ExecuteScriptAsync("document.documentElement.outerHTML");
            return JsonSerializer.Deserialize<string>(json) ?? "";
        }

        private async Task<string> ResolveIfCaptchaAsync(string html)
        {
            if (!IsCaptchaPage(html)) return html;

            string lower = html.ToLowerInvariant();
            bool esBloqueoIp = lower.Contains("upstream connect error") || lower.Contains("connection failure");

            if (esBloqueoIp)
            {
                System.Media.SystemSounds.Hand.Play();
                LogStatus2("⛔ IP bloqueada por Empresite. Reintentando en 2 min... (Parar para cancelar)");
            }
            else
            {
                System.Media.SystemSounds.Exclamation.Play();
                LogStatus2("⚠ CAPTCHA detectado. Intentando resolución automática...");
            }
            if (!esBloqueoIp)
            {
                await Task.Delay(3000); // esperar que el iframe de reCAPTCHA cargue
                await TryClickCaptchaCheckboxAsync();
                await Task.Delay(3500);
                bool clicVerificar = await TryClickVerificarAsync();
                if (!clicVerificar)
                    LogStatus2("CAPTCHA: si apareció un reto de imágenes, resuélvelo y pulsa VERIFICAR.");
            }

            int maxSegundos = esBloqueoIp ? 120 : 180;
            for (int i = 0; i < maxSegundos; i++)
            {
                if (_cts?.IsCancellationRequested == true) break;
                await Task.Delay(1000);

                if (esBloqueoIp && i > 0 && i % 30 == 0)
                    LogStatus2($"⛔ IP bloqueada. Reintentando en {maxSegundos - i}s... (Parar para cancelar)");

                string current = await GetWebViewHtmlAsync();
                if (!IsCaptchaPage(current))
                {
                    LogStatus2(esBloqueoIp ? "Conexión restablecida. Continuando..." : "CAPTCHA resuelto. Continuando...");
                    await Task.Delay(2000);
                    return await GetWebViewHtmlAsync();
                }

                if (!esBloqueoIp && i > 0 && i % 20 == 0)
                    await TryClickVerificarAsync();
            }
            return await GetWebViewHtmlAsync();
        }

        private bool IsCaptchaPage(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return false;
            string lower = html.ToLowerInvariant();
            return lower.Contains("error_capado_robots") ||
                   lower.Contains("demasiadas peticiones detectadas") ||
                   lower.Contains("verify_recaptcha") ||
                   lower.Contains("too many requests") ||
                   lower.Contains("verify you are human") ||
                   lower.Contains("upstream connect error") ||
                   lower.Contains("reset reason: connection failure");
        }


        private async Task TryClickCaptchaCheckboxAsync()
        {
            try
            {
                if (webView21.CoreWebView2 == null) return;

                // Espera a que el iframe de reCAPTCHA cargue completamente
                await Task.Delay(2000);

                // IMPORTANTE: devolver el objeto directamente (no JSON.stringify) porque
                // ExecuteScriptAsync ya envuelve el resultado en JSON — usar stringify
                // produce una cadena doblemente codificada que no se puede parsear como objeto.
                string script = @"(function() {
                    var iframe = document.querySelector('iframe[src*=""api2/anchor""]')
                              || document.querySelector('iframe[src*=""enterprise/anchor""]')
                              || document.querySelector('iframe[title=""reCAPTCHA""]')
                              || document.querySelector('iframe[src*=""recaptcha""]');
                    if (iframe) {
                        iframe.scrollIntoView({block: 'center', behavior: 'instant'});
                        var r = iframe.getBoundingClientRect();
                        return {x: r.left + 25, y: r.top + 37};
                    }
                    var el = document.querySelector('.g-recaptcha');
                    if (!el) return null;
                    el.scrollIntoView({block: 'center', behavior: 'instant'});
                    var r = el.getBoundingClientRect();
                    return {x: r.left + 25, y: r.top + 37};
                })()";

                string result = await webView21.ExecuteScriptAsync(script);
                if (result == "null" || string.IsNullOrEmpty(result)) return;

                var pos = JsonSerializer.Deserialize<JsonElement>(result);
                double x = pos.GetProperty("x").GetDouble();
                double y = pos.GetProperty("y").GetDouble();

                LogStatus2($"CAPTCHA: haciendo clic en checkbox ({x:F0}, {y:F0})...");

                string move = $@"{{""type"":""mouseMoved"",""x"":{x:F1},""y"":{y:F1},""button"":""none""}}";
                await webView21.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", move);
                await Task.Delay(300);

                string down = $@"{{""type"":""mousePressed"",""x"":{x:F1},""y"":{y:F1},""button"":""left"",""buttons"":1,""clickCount"":1}}";
                string up   = $@"{{""type"":""mouseReleased"",""x"":{x:F1},""y"":{y:F1},""button"":""left"",""clickCount"":1}}";
                await webView21.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", down);
                await Task.Delay(200);
                await webView21.CoreWebView2.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", up);
            }
            catch (Exception ex) { LogStatus2($"CAPTCHA clic error: {ex.Message}"); }
        }

        private async Task<bool> TryClickVerificarAsync()
        {
            try
            {
                if (webView21.CoreWebView2 == null) return false;

                // Si g-recaptcha-response tiene token → enviar el formulario directamente vía JS.
                // form.submit() nativo evita el handler jQuery que muestra alert() si está vacío.
                string script = @"(function() {
                    var resp = document.getElementById('g-recaptcha-response');
                    if (!resp || !resp.value || resp.value.length === 0) return null;
                    var form = document.getElementById('form_capados_recaptcha')
                            || document.querySelector('form');
                    if (!form) return null;
                    form.submit();
                    return true;
                })()";

                string result = await webView21.ExecuteScriptAsync(script);
                bool submitted = result == "true";
                if (submitted) LogStatus2("CAPTCHA: formulario enviado automáticamente.");
                return submitted;
            }
            catch { return false; }
        }

        private async void BTNFiltrar_Click(object sender, EventArgs e)
        {
            BTNFiltrar.Enabled = false;
            try
            {
                var svc = new CompanyQueryService(DatabaseService.ConnectionString);
                var tabla = await svc.GetFilteredCompaniesAsync(
                    CBSector.SelectedItem?.ToString()        ?? "Todos",
                    CBActividad.SelectedItem?.ToString()     ?? "Todos",
                    CBCnae.SelectedItem?.ToString()          ?? "Todos",
                    CBKeyword.SelectedItem?.ToString()       ?? "Todos",
                    CBEmailStatus.SelectedItem?.ToString()   ?? "Todos",
                    CBCompanyStatus.SelectedItem?.ToString() ?? "Todos");

                _currentTable = tabla;
                AplicarFiltroPorNombre();
                if (string.IsNullOrWhiteSpace(TXTBuscarEmpresa.Text))
                    LBEstado.Text = $"Resultados: {_currentTable.Rows.Count} empresas";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al filtrar: " + ex.Message);
            }
            finally
            {
                BTNFiltrar.Enabled = true;
            }
        }

        private void BTNExportarFiltrado_Click(object sender, EventArgs e)
        {
            if (_currentTable.Rows.Count == 0)
            {
                MessageBox.Show("No hay datos en la tabla. Pulsa Filtrar primero.");
                return;
            }

            using var dlg = new SaveFileDialog();
            dlg.Filter = "Archivo Excel (*.xlsx)|*.xlsx";
            dlg.FileName = "Empresas_Filtrado.xlsx";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ExcelExportService.ExportDataTable(_currentTable, dlg.FileName);
                MessageBox.Show("Excel exportado correctamente.");
            }
        }

        private void BTNExportar_Click(object sender, EventArgs e)
        {
            using var dlg = new SaveFileDialog();
            dlg.Filter = "Archivo Excel (*.xlsx)|*.xlsx";
            dlg.FileName = $"Empresas_SealLead_{DateTime.Now:yyyyMMdd_HHmmss}.xlsx";

            if (dlg.ShowDialog() == DialogResult.OK)
            {
                ExcelExportService.ExportCompanies(DatabaseService.ConnectionString, dlg.FileName);
                MessageBox.Show("Excel exportado correctamente.");
            }
        }
    }
}
