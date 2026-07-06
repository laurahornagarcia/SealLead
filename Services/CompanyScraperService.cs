using HtmlAgilityPack;
using Microsoft.Data.Sqlite;
using SealLead.Data;
using SealScout;
using System.Net;
using System.Text.RegularExpressions;

namespace SealLead.Services
{
    public record ScraperProgress(string CurrentCompany, int TotalProcessed, int NewCompanies, string CurrentLocality = "", int CurrentPage = 0, int TotalLocalities = 0, int CurrentLocalityIndex = 0);

    public class CompanyScraperService
    {
        private readonly string _connectionString;
        private readonly HttpClient _httpClient;
        private readonly Func<string, Task<string>>? _webGetHtml;
        private readonly Random _random = new();
        private Action<string>? _statusLog;
        private Action? _onCaptchaDetected;
        private string? _proxyHost;
        private System.Net.CookieContainer _cookieContainer = new();
        private TaskCompletionSource<string>? _captchaTcs;

        private const string _userAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36";

        public CompanyScraperService(string connectionString, string? proxyUrl = null, Func<string, Task<string>>? webGetHtml = null)
        {
            _connectionString = connectionString;
            _webGetHtml = webGetHtml;

            // SocketsHttpHandler gestiona correctamente el ciclo 407→retry con credenciales en el CONNECT.
            // HttpClientHandler lanza excepción en el 407 del tunnel HTTPS y no reintenta.
            System.Net.Http.SocketsHttpHandler socketsHandler;

            if (!string.IsNullOrWhiteSpace(proxyUrl))
            {
                string fullUrl = proxyUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                    ? proxyUrl : "http://" + proxyUrl;
                var proxyUri = new Uri(fullUrl);

                string proxyUser = "", proxyPass = "";
                if (!string.IsNullOrEmpty(proxyUri.UserInfo))
                {
                    int colon = proxyUri.UserInfo.IndexOf(':');
                    proxyUser = colon > 0 ? proxyUri.UserInfo[..colon] : proxyUri.UserInfo;
                    proxyPass = colon > 0 ? Uri.UnescapeDataString(proxyUri.UserInfo[(colon + 1)..]) : "";
                }

                _cookieContainer = new System.Net.CookieContainer();
                socketsHandler = new System.Net.Http.SocketsHttpHandler
                {
                    UseCookies = true,
                    CookieContainer = _cookieContainer,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                    AllowAutoRedirect = true,
                    UseProxy = true,
                    Proxy = new System.Net.WebProxy($"http://{proxyUri.Host}:{proxyUri.Port}")
                    {
                        Credentials = new System.Net.NetworkCredential(proxyUser, proxyPass),
                        BypassProxyOnLocal = false
                    },
                };
                _proxyHost = $"{proxyUri.Host}:{proxyUri.Port}";
            }
            else
            {
                _cookieContainer = new System.Net.CookieContainer();
                socketsHandler = new System.Net.Http.SocketsHttpHandler
                {
                    UseCookies = true,
                    CookieContainer = _cookieContainer,
                    AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate | System.Net.DecompressionMethods.Brotli,
                    AllowAutoRedirect = true,
                };
            }

            _httpClient = new HttpClient(socketsHandler);
            _httpClient.Timeout = TimeSpan.FromSeconds(40);

            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept",
                "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8"
            );
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept-Language", "es-ES,es;q=0.9,en;q=0.8"
            );
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Referer", "https://empresite.eleconomista.es/"
            );
        }

        private async Task WarmUpAsync(CancellationToken ct = default)
        {
            if (_webGetHtml != null)
            {
                // En modo WebView2 no hace falta warmup — el navegador ya tiene sesión persistente
                return;
            }

            using var request = new HttpRequestMessage(HttpMethod.Get, "https://empresite.eleconomista.es/");
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Not/A)Brand\";v=\"8\", \"Chromium\";v=\"126\", \"Google Chrome\";v=\"126\"");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
            request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
            request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
            request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
            request.Headers.TryAddWithoutValidation("sec-fetch-site", "none");
            request.Headers.TryAddWithoutValidation("sec-fetch-user", "?1");
            request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");
            try { await _httpClient.SendAsync(request, ct); } catch { }
            await Task.Delay(_random.Next(6000, 12000), ct);
        }

        public async Task StartSearchAsync(
            string originalUrl,
            bool onlyWithEmail,
            int userId,
            Action<string>? log = null,
            Action<ScraperProgress>? onProgress = null,
            Action<CompanyData>? onCompanySaved = null,
            Action? onCaptchaDetected = null,
            CancellationToken cancellationToken = default)
        {
            string finalUrl = BuildUrl(originalUrl, onlyWithEmail);
            string searchActivity = GetSearchActivityFromUrl(originalUrl);

            var stopped = await GetStoppedSearchProgressAsync(originalUrl, onlyWithEmail);

            int searchId;
            if (stopped.HasValue)
            {
                searchId = stopped.Value.SearchId;
                await ResumeSearchAsync(searchId);
                log?.Invoke($"Retomando búsqueda detenida desde página {stopped.Value.CurrentPage}.");
            }
            else
            {
                searchId = await InsertSearchAsync(searchActivity, originalUrl, finalUrl, onlyWithEmail, userId);
                await SaveProgressAsync(searchId, 1, "");
            }

            _statusLog = log;
            _onCaptchaDetected = onCaptchaDetected;
            int total = 0;
            int totalProcessed = 0;

            try
            {
                string proxyInfo = _proxyHost != null ? $" | Proxy: {_proxyHost}" : " | Sin proxy";
                _statusLog?.Invoke($"Iniciando sesión con Empresite...{proxyInfo}");
                await WarmUpAsync(cancellationToken);

                _statusLog?.Invoke("Cargando primera página...");
                string page1Html = await GetHtmlAsync(BuildPageUrl(finalUrl, 1));

                {
                    int ca = 0;
                    while (IsBlocked(page1Html))
                    {
                        if (++ca > 10) { await StopSearchAsync(searchId, "CAPTCHA no resuelto"); log?.Invoke("Sigue bloqueado. Búsqueda detenida."); return; }
                        await WaitForCaptchaAsync(log, onCaptchaDetected, cancellationToken);
                        page1Html = await GetHtmlAsync(BuildPageUrl(finalUrl, 1));
                    }
                }

                var localityUrls = ExtractLocalityUrls(page1Html);

                if (localityUrls.Count > 0)
                {
                    log?.Invoke($"{localityUrls.Count} ubicaciones encontradas.");

                    // Si es una reanudación, localizar la ubicación donde se paró
                    string savedLocalityUrl = stopped.HasValue ? stopped.Value.CurrentLocalityUrl : "";
                    int resumeFromIndex = 0;
                    if (!string.IsNullOrEmpty(savedLocalityUrl))
                    {
                        int found = localityUrls.FindIndex(l =>
                            l.url.TrimEnd('/').Equals(savedLocalityUrl.TrimEnd('/'), StringComparison.OrdinalIgnoreCase));
                        resumeFromIndex = found >= 0 ? found : 0;
                        if (resumeFromIndex > 0)
                            log?.Invoke($"Saltando {resumeFromIndex} ubicaciones ya procesadas. Retomando desde: {localityUrls[resumeFromIndex].name}");
                    }

                    if (resumeFromIndex == 0)
                    {
                        int pauseMs = _random.Next(10000, 18000);
                        log?.Invoke($"Pausa {pauseMs / 1000}s antes de empezar...");
                        await Task.Delay(pauseMs, cancellationToken);
                    }

                    for (int i = resumeFromIndex; i < localityUrls.Count; i++)
                    {
                        var (localityUrl, localityName) = localityUrls[i];
                        string filteredUrl = BuildUrl(localityUrl, onlyWithEmail);

                        // Guardar en qué ubicación estamos antes de empezar a paginarla
                        await SaveProgressAsync(searchId, 1, "", localityUrl);

                        log?.Invoke($">> Ubicación {i + 1}/{localityUrls.Count}: {localityName}");

                        // Si es la ubicación donde se paró, reanudar desde la página guardada
                        int startPage = (stopped.HasValue && i == resumeFromIndex && !string.IsNullOrEmpty(savedLocalityUrl))
                            ? stopped.Value.CurrentPage
                            : 1;

                        var (completed, added, processed) = await PaginateUrlAsync(
                            searchId, filteredUrl, startPage, searchActivity,
                            total, totalProcessed,
                            localityName, i + 1, localityUrls.Count,
                            log, onProgress, onCompanySaved, onCaptchaDetected, cancellationToken);
                        total += added;
                        totalProcessed += processed;

                        if (!completed) return;

                        if (i < localityUrls.Count - 1)
                        {
                            int pause = _random.Next(15000, 30000);
                            log?.Invoke($"Pausa entre ubicaciones: {pause / 1000}s...");
                            await Task.Delay(pause, cancellationToken);
                        }
                    }
                }
                else
                {
                    int startPage = stopped.HasValue ? stopped.Value.CurrentPage : 1;
                    var (completed, added, processed) = await PaginateUrlAsync(
                        searchId, finalUrl, startPage, searchActivity,
                        total, totalProcessed, "URL principal", 1, 1,
                        log, onProgress, onCompanySaved, onCaptchaDetected, cancellationToken);
                    total += added;
                    totalProcessed += processed;
                    if (!completed) return;
                }

                await FinishSearchAsync(searchId, total);
                log?.Invoke($"Búsqueda finalizada. Total empresas nuevas: {total}.");
                onProgress?.Invoke(new ScraperProgress("Finalizado", totalProcessed, total));
            }
            catch (OperationCanceledException)
            {
                await StopSearchAsync(searchId, "Detenida por el usuario");
                log?.Invoke("Búsqueda detenida.");
            }
            catch (Exception ex)
            {
                await StopSearchAsync(searchId, ex.Message);
                log?.Invoke("Proceso detenido: " + ex.Message);
            }
        }

        public async Task ReattemptIncompleteCompaniesAsync(
            Action<string>? log = null,
            Action<ScraperProgress>? onProgress = null,
            Action<CompanyData>? onCompanySaved = null,
            CancellationToken cancellationToken = default)
        {
            log?.Invoke("Buscando empresas sin email en la base de datos...");

            var incompleteCompanies = await GetIncompleteCompaniesAsync();
            if (incompleteCompanies.Count == 0)
            {
                log?.Invoke("No hay empresas sin email para completar.");
                return;
            }

            log?.Invoke($"Encontradas {incompleteCompanies.Count} empresas sin email. Intentando completar datos...");

            int completed = 0;
            int updated = 0;

            foreach (var company in incompleteCompanies)
            {
                if (cancellationToken.IsCancellationRequested) break;

                onProgress?.Invoke(new ScraperProgress(
                    company.CompanyName,
                    completed + 1,
                    updated,
                    "Completando datos",
                    0,
                    incompleteCompanies.Count,
                    completed + 1));

                completed++;
                log?.Invoke($"[{completed}/{incompleteCompanies.Count}] Intentando: {company.CompanyName}");

                try
                {
                    await HumanDelayAsync(8000, 15000, cancellationToken);
                    string profileHtml = await GetHtmlAsync(company.ProfileUrl);

                    int retries = 0;
                    while (IsBlocked(profileHtml) && ++retries < 3)
                    {
                        await Task.Delay(5000, cancellationToken);
                        profileHtml = await GetHtmlAsync(company.ProfileUrl);
                    }

                    string email = ExtractEmail(profileHtml);
                    if (string.IsNullOrWhiteSpace(email))
                    {
                        log?.Invoke($"  ✗ Sin email");
                        continue;
                    }

                    company.Email = email;
                    company.LegalName = GetInfoGeneralValue(profileHtml, "Razón social");
                    company.Cif = GetInfoGeneralValue(profileHtml, "CIF");
                    company.LegalForm = GetInfoGeneralValue(profileHtml, "Forma jurídica");
                    company.Sector = GetInfoGeneralValue(profileHtml, "Sector");
                    company.Activity = GetInfoGeneralValue(profileHtml, "Actividad");
                    company.CnaeActivity = GetInfoGeneralValue(profileHtml, "Actividad CNAE");

                    await UpsertCompanyAsync(company);
                    updated++;
                    onCompanySaved?.Invoke(company);
                    log?.Invoke($"  ✓ Completada: {company.Email}");
                }
                catch (Exception ex)
                {
                    log?.Invoke($"  ✗ Error: {ex.Message}");
                }
            }

            log?.Invoke($"Completado. {updated} empresas actualizadas con email.");
        }

        private async Task<List<CompanyData>> GetIncompleteCompaniesAsync()
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, CompanyName, ProfileUrl, Email
                FROM Companies
                WHERE Email IS NULL OR Email = ''
                LIMIT 500
            ";

            var companies = new List<CompanyData>();
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                companies.Add(new CompanyData
                {
                    Id = reader.GetInt32(0),
                    CompanyName = reader.GetString(1),
                    ProfileUrl = reader.GetString(2),
                    Email = reader.IsDBNull(3) ? null : reader.GetString(3)
                });
            }

            return companies;
        }

        private async Task<(bool completed, int added, int processed)> PaginateUrlAsync(
            int searchId,
            string url,
            int startPage,
            string searchActivity,
            int totalNewOffset,
            int totalProcessedOffset,
            string localityName,
            int localityIndex,
            int totalLocalities,
            Action<string>? log,
            Action<ScraperProgress>? onProgress,
            Action<CompanyData>? onCompanySaved,
            Action? onCaptchaDetected = null,
            CancellationToken cancellationToken = default)
        {
            int page = startPage;
            int added = 0;
            int processed = 0;

            while (true)
            {
                string pageUrl = BuildPageUrl(url, page);
                log?.Invoke($"[{localityName}] Página {page}: {pageUrl}");

                string html = await GetHtmlAsync(pageUrl);

                {
                    int ca = 0;
                    while (IsBlocked(html))
                    {
                        if (++ca > 10) { await StopSearchAsync(searchId, "CAPTCHA no resuelto"); log?.Invoke("Sigue bloqueado. Búsqueda detenida."); return (false, added, processed); }
                        await WaitForCaptchaAsync(log, onCaptchaDetected, cancellationToken);
                        html = await GetHtmlAsync(pageUrl);
                    }
                }

                var companies = ExtractCompaniesFromList(html, searchActivity);

                if (companies.Count == 0)
                {
                    log?.Invoke($"[{localityName}] Sin más registros en página {page}. Pasando a siguiente ubicación.");
                    return (true, added, processed);
                }

                foreach (var company in companies)
                {
                    processed++;
                    onProgress?.Invoke(new ScraperProgress(
                        company.CompanyName,
                        totalProcessedOffset + processed,
                        totalNewOffset + added,
                        $"{localityName} ({localityIndex}/{totalLocalities})",
                        page,
                        totalLocalities,
                        localityIndex));

                    int? existingCompanyId = await GetCompanyIdByProfileUrlAsync(company.ProfileUrl);

                    if (existingCompanyId.HasValue)
                    {
                        await InsertSearchResultAsync(searchId, existingCompanyId.Value);
                        await SaveProgressAsync(searchId, page, company.ProfileUrl);
                        log?.Invoke($"Ya existía: {company.CompanyName}");
                        continue;
                    }

                    log?.Invoke($"Ficha: {company.CompanyName}");
                    await HumanDelayAsync(8000, 15000, cancellationToken);

                    string profileHtml = await GetHtmlAsync(company.ProfileUrl);

                    {
                        int ca = 0;
                        while (IsBlocked(profileHtml))
                        {
                            if (++ca > 10) { await StopSearchAsync(searchId, "CAPTCHA no resuelto en ficha"); log?.Invoke("Sigue bloqueado. Búsqueda detenida."); return (false, added, processed); }
                            await WaitForCaptchaAsync(log, onCaptchaDetected, cancellationToken);
                            profileHtml = await GetHtmlAsync(company.ProfileUrl);
                        }
                    }

                    company.Email = ExtractEmail(profileHtml);

                    // Si no conseguimos email, reintentar 2 veces la misma ficha
                    if (string.IsNullOrWhiteSpace(company.Email))
                    {
                        for (int retry = 0; retry < 2 && string.IsNullOrWhiteSpace(company.Email); retry++)
                        {
                            log?.Invoke($"Email no encontrado en ficha de {company.CompanyName}. Reintentando ({retry + 1}/2)...");
                            await HumanDelayAsync(8000, 15000, cancellationToken);
                            profileHtml = await GetHtmlAsync(company.ProfileUrl);
                            company.Email = ExtractEmail(profileHtml);
                        }
                    }

                    company.LegalName = GetInfoGeneralValue(profileHtml, "Razón social");
                    company.Cif = GetInfoGeneralValue(profileHtml, "CIF");
                    company.LegalForm = GetInfoGeneralValue(profileHtml, "Forma jurídica");
                    company.Sector = GetInfoGeneralValue(profileHtml, "Sector");
                    company.Activity = GetInfoGeneralValue(profileHtml, "Actividad");
                    company.CnaeActivity = GetInfoGeneralValue(profileHtml, "Actividad CNAE");

                    int companyId = await UpsertCompanyAsync(company);
                    await InsertSearchResultAsync(searchId, companyId);
                    await SaveProgressAsync(searchId, page, company.ProfileUrl);
                    added++;
                    onCompanySaved?.Invoke(company);

                    onProgress?.Invoke(new ScraperProgress(
                        company.CompanyName,
                        totalProcessedOffset + processed,
                        totalNewOffset + added,
                        $"{localityName} ({localityIndex}/{totalLocalities})",
                        page,
                        totalLocalities,
                        localityIndex));
                    log?.Invoke($"Guardada: {company.CompanyName} | {company.Email}");
                }

                page++;
                await HumanDelayAsync(12000, 22000, cancellationToken);
            }
        }

        private string BuildUrl(string url, bool onlyWithEmail)
        {
            url = url.Trim();

            if (!onlyWithEmail)
                return url;

            if (url.Contains("emp_email=true", StringComparison.OrdinalIgnoreCase))
                return url;

            var separator = url.Contains("?") ? "&" : "?";

            if (url.Contains("testfiltros=1", StringComparison.OrdinalIgnoreCase))
                return url + separator + "emp_email=true";

            return url + separator + "testfiltros=1&emp_email=true";
        }

        private string BuildPageUrl(string finalUrl, int page)
        {
            var uri = new Uri(finalUrl);

            string query = uri.Query;

            string baseUrl = finalUrl.Split('?')[0].TrimEnd('/');

            baseUrl = Regex.Replace(
                baseUrl,
                @"/PgNum-\d+/?$",
                "",
                RegexOptions.IgnoreCase
            );

            if (page == 1)
                return baseUrl + "/" + query;

            return $"{baseUrl}/PgNum-{page}/{query}";
        }

        private async Task<string> GetHtmlAsync(string url, int maxRetries = 3)
        {
            // Modo WebView2: el delegate navega el navegador real y gestiona CAPTCHA
            if (_webGetHtml != null)
                return await _webGetHtml(url);

            int attempt = 0;
            while (true)
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
                request.Headers.TryAddWithoutValidation("sec-ch-ua", "\"Not/A)Brand\";v=\"8\", \"Chromium\";v=\"126\", \"Google Chrome\";v=\"126\"");
                request.Headers.TryAddWithoutValidation("sec-ch-ua-mobile", "?0");
                request.Headers.TryAddWithoutValidation("sec-ch-ua-platform", "\"Windows\"");
                request.Headers.TryAddWithoutValidation("sec-fetch-dest", "document");
                request.Headers.TryAddWithoutValidation("sec-fetch-mode", "navigate");
                request.Headers.TryAddWithoutValidation("sec-fetch-site", "same-origin");
                request.Headers.TryAddWithoutValidation("sec-fetch-user", "?1");
                request.Headers.TryAddWithoutValidation("upgrade-insecure-requests", "1");

                using var response = await _httpClient.SendAsync(request);
                string html = await response.Content.ReadAsStringAsync();
                int status = (int)response.StatusCode;

                if (status == 429 || status == 503)
                {
                    attempt++;
                    if (attempt >= maxRetries)
                        throw new Exception($"HTTP {status} tras {maxRetries} intentos. Espera unos minutos y relanza la búsqueda.");
                    int waitSec = attempt == 1 ? 180 : 300;
                    for (int rem = waitSec; rem > 0; rem -= 10)
                    {
                        _statusLog?.Invoke($"HTTP {status} — IP bloqueada. Reintentando en {rem}s... (intento {attempt}/{maxRetries - 1})");
                        await Task.Delay(Math.Min(10000, rem * 1000));
                    }
                    continue;
                }

                if (status == 404)
                    return ""; // no hay más páginas — se trata como fin de paginación

                if (status == 403)
                    throw new Exception("HTTP 403 Forbidden — acceso bloqueado.");

                response.EnsureSuccessStatusCode();
                return html;
            }
        }

        public void ProvideCookies(string rawCookieHeader)
        {
            var uri = new Uri("https://empresite.eleconomista.es");
            foreach (var part in rawCookieHeader.Split(';'))
            {
                var trimmed = part.Trim();
                int eq = trimmed.IndexOf('=');
                if (eq <= 0) continue;
                string name = trimmed[..eq].Trim();
                string value = trimmed[(eq + 1)..].Trim();
                try { _cookieContainer.Add(uri, new System.Net.Cookie(name, value)); } catch { }
            }
            _captchaTcs?.TrySetResult(rawCookieHeader);
        }

        private async Task WaitForCaptchaAsync(Action<string>? log, Action? onCaptchaDetected, CancellationToken ct = default)
        {
            if (_webGetHtml != null)
            {
                // Modo WebView2: el CAPTCHA ya está visible en la ventana inferior
                log?.Invoke("CAPTCHA pendiente. Resuélvelo en la ventana del navegador. Reintentando en 30s...");
                await Task.Delay(30000, ct);
                return;
            }

            // Modo HttpClient: panel de cookies manual
            _captchaTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            log?.Invoke("CAPTCHA detectado. Resuelve el CAPTCHA en el navegador y pega las cookies en la app.");
            onCaptchaDetected?.Invoke();
            await _captchaTcs.Task.WaitAsync(ct);
            log?.Invoke("Cookies recibidas. Reintentando...");
        }

        // ── DuckDuckGo ────────────────────────────────────────────────────────────

        private string BuildDdgQuery(string empresiteUrl)
        {
            // Las fichas de empresa están en la raíz: empresite.eleconomista.es/EMPRESA.html
            // No bajo /Actividad/ — por eso buscamos en todo el site con palabras clave extraídas de la URL
            var keywords = new List<string>();

            var actMatch = Regex.Match(empresiteUrl, @"/Actividad/([^/]+)/", RegexOptions.IgnoreCase);
            if (actMatch.Success)
                keywords.Add(actMatch.Groups[1].Value.Replace("-", " ").ToLowerInvariant());

            var locMatch = Regex.Match(empresiteUrl, @"/(localidad|provincia)/([^/]+)/", RegexOptions.IgnoreCase);
            if (locMatch.Success)
                keywords.Add(locMatch.Groups[2].Value.Replace("-", " ").ToLowerInvariant());

            string kw = keywords.Count > 0 ? string.Join(" ", keywords) : "empresas";
            return $"site:empresite.eleconomista.es {kw}";
        }

        private async Task WarmUpDuckDuckGoAsync()
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://html.duckduckgo.com/");
            req.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            req.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            req.Headers.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9");
            try { await _httpClient.SendAsync(req); } catch { }
            await Task.Delay(_random.Next(2000, 4000));
        }

        private async Task<List<string>> SearchDuckDuckGoAsync(string query, int maxPages, Action<string>? log)
        {
            var results = new List<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            string? lastHtml = null;

            for (int page = 1; page <= maxPages; page++)
            {
                try
                {
                    string html;
                    if (page == 1)
                    {
                        string url = $"https://html.duckduckgo.com/html/?q={Uri.EscapeDataString(query)}&kl=es-es";
                        html = await GetDuckDuckGoHtmlAsync(url, null);
                    }
                    else
                    {
                        var formData = ExtractDdgNextFormData(lastHtml!);
                        if (formData == null) break;
                        html = await GetDuckDuckGoHtmlAsync("https://html.duckduckgo.com/html/", formData);
                    }

                    lastHtml = html;
                    int before = results.Count;
                    ExtractEmpresiteProfileUrls(html, results, seen);
                    log?.Invoke($"DuckDuckGo pág {page}: {results.Count - before} nuevas (total: {results.Count})");

                    if (results.Count == before) break;
                    if (page < maxPages) await Task.Delay(_random.Next(3000, 6000));
                }
                catch (Exception ex) { log?.Invoke($"DDG pág {page} error: {ex.Message}"); break; }
            }

            return results;
        }

        private async Task<string> GetDuckDuckGoHtmlAsync(string url, Dictionary<string, string>? postData)
        {
            HttpRequestMessage request = postData != null
                ? new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(postData) }
                : new HttpRequestMessage(HttpMethod.Get, url);

            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "es-ES,es;q=0.9,en;q=0.8");
            request.Headers.TryAddWithoutValidation("Referer", "https://html.duckduckgo.com/");

            using var response = await _httpClient.SendAsync(request);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private void ExtractEmpresiteProfileUrls(string html, List<string> results, HashSet<string> seen)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var links = doc.DocumentNode.SelectNodes("//a[@class='result__a']");
            if (links == null) return;

            foreach (var link in links)
            {
                string href = link.GetAttributeValue("href", "");
                string url = ExtractRealUrlFromDdg(href);

                if (string.IsNullOrEmpty(url)) continue;
                if (!url.Contains("empresite.eleconomista.es", StringComparison.OrdinalIgnoreCase)) continue;

                // Las fichas de empresa terminan en .html en la raíz.
                // Las páginas de listado terminan en / o contienen /Actividad/, /localidad/, etc.
                string path = new Uri(url).AbsolutePath;
                if (!path.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) continue;

                if (seen.Add(url)) results.Add(url);
            }
        }

        private string ExtractRealUrlFromDdg(string href)
        {
            if (string.IsNullOrEmpty(href)) return "";
            var m = Regex.Match(href, @"uddg=([^&]+)");
            if (m.Success)
                try { return Uri.UnescapeDataString(m.Groups[1].Value); } catch { }
            if (href.StartsWith("http", StringComparison.OrdinalIgnoreCase)) return href;
            return "";
        }

        private Dictionary<string, string>? ExtractDdgNextFormData(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var form = doc.DocumentNode.SelectSingleNode("//form[contains(@action,'html')]");
            if (form == null) return null;
            var inputs = form.SelectNodes(".//input[@type='hidden']");
            if (inputs == null) return null;
            var data = new Dictionary<string, string>();
            foreach (var inp in inputs)
            {
                string name = inp.GetAttributeValue("name", "");
                if (!string.IsNullOrEmpty(name))
                    data[name] = inp.GetAttributeValue("value", "");
            }
            return data.Count > 0 ? data : null;
        }

        private string ExtractCompanyNameFromProfile(string html, string profileUrl)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var h1 = doc.DocumentNode.SelectSingleNode("//h1");
            if (h1 != null) return Clean(h1.InnerText);
            // Fallback: nombre desde la URL
            string seg = System.IO.Path.GetFileNameWithoutExtension(new Uri(profileUrl).AbsolutePath);
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(seg.Replace("-", " ").ToLowerInvariant());
        }

        private string ExtractAddressFromProfile(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode("//span[@itemprop='address']");
            return node != null ? Clean(node.InnerText) : "";
        }

        // ─────────────────────────────────────────────────────────────────────────

        private bool IsBlocked(string html)
        {
            if (string.IsNullOrWhiteSpace(html))
                return true;

            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Si hay empresas, NO está bloqueado
            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'cardCompany')]");
            if (cards != null && cards.Count > 0)
                return false;

            // Si hay ficha de empresa, NO está bloqueado
            var infoGeneral = doc.DocumentNode.SelectSingleNode("//div[@id='infogeneral']");
            if (infoGeneral != null)
                return false;

            string lower = html.ToLowerInvariant();

            // "cloudflare" sola NO es indicador — aparece en cualquier página que usa CF como CDN.
            // Solo bloqueamos con señales inequívocas de challenge o bloqueo real.
            return lower.Contains("too many requests") ||
                   lower.Contains("demasiadas peticiones detectadas") ||
                   lower.Contains("error_capado_robots") ||
                   lower.Contains("verify_recaptcha") ||
                   lower.Contains("verify you are human") ||
                   lower.Contains("comprueba que no eres un robot") ||
                   lower.Contains("checking your browser before accessing") ||
                   lower.Contains("cf-browser-verification") ||
                   lower.Contains("enable javascript and cookies to continue") ||
                   lower.Contains("acceso denegado") ||
                   lower.Contains("access denied");
        }
        private List<(string url, string name)> ExtractLocalityUrls(string html)
        {
            var result = new List<(string url, string name)>();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            // Busca el contenedor del filtro de ubicación por su clase overflow-y-scroll (único en la página)
            // Si no lo encuentra, intenta con el título del bloque como fallback
            var container = doc.DocumentNode.SelectSingleNode("//div[contains(@class,'overflow-y-scroll')]");
            var links = container != null
                ? container.SelectNodes(".//a[@href and @href!='#']")
                : doc.DocumentNode.SelectNodes("//p[contains(text(),'Filtrar por ubicación')]/ancestor::div[2]//a[@href and @href!='#']");

            if (links == null) return result;

            foreach (var link in links)
            {
                string href = link.GetAttributeValue("href", "").Trim();
                if (string.IsNullOrEmpty(href) || href == "#") continue;
                if (result.Any(r => r.url == href)) continue;

                string name = ExtractUbicacionNameFromUrl(href);
                result.Add((href, name));
            }

            return result;
        }

        private string ExtractUbicacionNameFromUrl(string url)
        {
            // Coge el último segmento de la URL con contenido: /localidad/ALCOBENDAS-MADRID/ -> "Alcobendas Madrid"
            // También funciona con /provincia/MADRID/, /ciudad/BARCELONA/, etc.
            var uri = url.TrimEnd('/');
            int last = uri.LastIndexOf('/');
            if (last < 0) return url;
            string segment = uri[(last + 1)..];
            return System.Globalization.CultureInfo.CurrentCulture.TextInfo
                .ToTitleCase(segment.Replace("-", " ").ToLowerInvariant());
        }

        private List<CompanyData> ExtractCompaniesFromList(string html, string searchKeywords)
        {
            var result = new List<CompanyData>();
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode.SelectNodes("//div[contains(@class,'cardCompany')]");

            if (cards == null) return result;

            foreach (var card in cards)
            {
                var linkNode = card.SelectSingleNode(".//h3/a");
                if (linkNode == null) continue;

                string name = Clean(linkNode.InnerText);
                string profileUrl = linkNode.GetAttributeValue("href", "");
                string address = Clean(card.SelectSingleNode(".//span[@itemprop='address']")?.InnerText ?? "");

                if (!string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(profileUrl))
                {
                    result.Add(new CompanyData
                    {
                        CompanyName = name,
                        ProfileUrl = profileUrl,
                        Address = address,
                        SearchKeywords = searchKeywords
                    });
                }
            }

            return result;
        }

        private string ExtractEmail(string html)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var emailNode = doc.DocumentNode.SelectSingleNode("//a[contains(@class,'email')]");

            if (emailNode != null)
                return Clean(emailNode.InnerText);

            var mailtoNode = doc.DocumentNode.SelectSingleNode("//a[starts-with(@href,'mailto:')]");

            if (mailtoNode != null)
            {
                string href = mailtoNode.GetAttributeValue("href", "");
                return Clean(href.Replace("mailto:", "").Split('?')[0]);
            }

            var match = Regex.Match(html, @"[a-zA-Z0-9._%+\-]+@[a-zA-Z0-9.\-]+\.[a-zA-Z]{2,}");
            return match.Success ? match.Value : "";
        }

        private string GetInfoGeneralValue(string html, string label)
        {
            var doc = new HtmlAgilityPack.HtmlDocument();
            doc.LoadHtml(html);

            var h3Node = doc.DocumentNode.SelectSingleNode(
                $"//div[@id='infogeneral']//h3[normalize-space()='{label}']");

            if (h3Node == null) return "";

            return Clean(h3Node.ParentNode.SelectSingleNode(".//span")?.InnerText ?? "");
        }

        private string GetSearchActivityFromUrl(string url)
        {
            const string marker = "/Actividad/";

            int index = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (index == -1) return "";

            string rest = url[(index + marker.Length)..];
            int end = rest.IndexOf('/');

            if (end >= 0)
                rest = rest[..end];

            return Uri.UnescapeDataString(rest).Replace("-", " ").Trim();
        }

        private async Task<int> InsertSearchAsync(string searchActivity, string originalUrl, string finalUrl, bool onlyWithEmail, int userId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO Searches (
    SearchActivity,
    OriginalUrl,
    FinalUrl,
    OnlyWithEmail,
    ExtractionUserId,
    Status
)
VALUES (
    @SearchActivity,
    @OriginalUrl,
    @FinalUrl,
    @OnlyWithEmail,
    @ExtractionUserId,
    'En curso'
);

SELECT last_insert_rowid();
";

            command.Parameters.AddWithValue("@SearchActivity", searchActivity);
            command.Parameters.AddWithValue("@OriginalUrl", originalUrl);
            command.Parameters.AddWithValue("@FinalUrl", finalUrl);
            command.Parameters.AddWithValue("@OnlyWithEmail", onlyWithEmail ? 1 : 0);
            command.Parameters.AddWithValue("@ExtractionUserId", userId);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private async Task<int> UpsertCompanyAsync(CompanyData company)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO Companies (
    CompanyName,
    Email,
    Phone,
    ProfileUrl,
    Address,
    LegalName,
    Cif,
    LegalForm,
    Sector,
    Activity,
    CnaeActivity,
    SearchKeywords,
    UpdatedAt
)
VALUES (
    @CompanyName,
    @Email,
    @Phone,
    @ProfileUrl,
    @Address,
    @LegalName,
    @Cif,
    @LegalForm,
    @Sector,
    @Activity,
    @CnaeActivity,
    @SearchKeywords,
    CURRENT_TIMESTAMP
)
ON CONFLICT(ProfileUrl) DO UPDATE SET
    CompanyName = excluded.CompanyName,
    Email = excluded.Email,
    Phone = excluded.Phone,
    Address = excluded.Address,
    LegalName = excluded.LegalName,
    Cif = excluded.Cif,
    LegalForm = excluded.LegalForm,
    Sector = excluded.Sector,
    Activity = excluded.Activity,
    CnaeActivity = excluded.CnaeActivity,
    SearchKeywords = excluded.SearchKeywords,
    UpdatedAt = CURRENT_TIMESTAMP;

SELECT Id FROM Companies WHERE ProfileUrl = @ProfileUrl;
";

            command.Parameters.AddWithValue("@CompanyName", company.CompanyName);
            command.Parameters.AddWithValue("@Email", company.Email);
            command.Parameters.AddWithValue("@Phone", company.Phone);
            command.Parameters.AddWithValue("@ProfileUrl", company.ProfileUrl);
            command.Parameters.AddWithValue("@Address", company.Address);
            command.Parameters.AddWithValue("@LegalName", company.LegalName);
            command.Parameters.AddWithValue("@Cif", company.Cif);
            command.Parameters.AddWithValue("@LegalForm", company.LegalForm);
            command.Parameters.AddWithValue("@Sector", company.Sector);
            command.Parameters.AddWithValue("@Activity", company.Activity);
            command.Parameters.AddWithValue("@CnaeActivity", company.CnaeActivity);
            command.Parameters.AddWithValue("@SearchKeywords", company.SearchKeywords);

            return Convert.ToInt32(await command.ExecuteScalarAsync());
        }

        private async Task InsertSearchResultAsync(int searchId, int companyId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT OR IGNORE INTO SearchResults (
    SearchId,
    CompanyId
)
VALUES (
    @SearchId,
    @CompanyId
);
";

            command.Parameters.AddWithValue("@SearchId", searchId);
            command.Parameters.AddWithValue("@CompanyId", companyId);

            await command.ExecuteNonQueryAsync();
        }

        private async Task SaveProgressAsync(int searchId, int currentPage, string lastCompanyUrl, string? currentLocalityUrl = null)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
INSERT INTO SearchProgress (
    SearchId,
    CurrentPage,
    LastCompanyUrl,
    CurrentLocalityUrl,
    UpdatedAt
)
VALUES (
    @SearchId,
    @CurrentPage,
    @LastCompanyUrl,
    @CurrentLocalityUrl,
    CURRENT_TIMESTAMP
)
ON CONFLICT(SearchId) DO UPDATE SET
    CurrentPage = excluded.CurrentPage,
    LastCompanyUrl = excluded.LastCompanyUrl,
    CurrentLocalityUrl = COALESCE(excluded.CurrentLocalityUrl, CurrentLocalityUrl),
    UpdatedAt = CURRENT_TIMESTAMP;
";

            command.Parameters.AddWithValue("@SearchId", searchId);
            command.Parameters.AddWithValue("@CurrentPage", currentPage);
            command.Parameters.AddWithValue("@LastCompanyUrl", lastCompanyUrl ?? "");
            command.Parameters.AddWithValue("@CurrentLocalityUrl", (object?)currentLocalityUrl ?? DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        private async Task FinishSearchAsync(int searchId, int totalCompanies)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Searches
SET Status = 'Finalizada',
    FinishedAt = CURRENT_TIMESTAMP,
    StopReason = NULL,
    TotalCompanies = @TotalCompanies
WHERE Id = @SearchId;
";

            command.Parameters.AddWithValue("@SearchId", searchId);
            command.Parameters.AddWithValue("@TotalCompanies", totalCompanies);

            await command.ExecuteNonQueryAsync();
        }

        private async Task StopSearchAsync(int searchId, string reason)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
UPDATE Searches
SET Status = 'Detenida',
    FinishedAt = CURRENT_TIMESTAMP,
    StopReason = @StopReason
WHERE Id = @SearchId;
";

            command.Parameters.AddWithValue("@SearchId", searchId);
            command.Parameters.AddWithValue("@StopReason", reason);

            await command.ExecuteNonQueryAsync();
        }

        private async Task HumanDelayAsync(int minMs, int maxMs, CancellationToken ct = default)
        {
            await Task.Delay(_random.Next(minMs, maxMs), ct);
        }

        private string Clean(string value)
        {
            return WebUtility.HtmlDecode(value)
                .Replace("\n", " ")
                .Replace("\r", " ")
                .Replace("\t", " ")
                .Trim();
        }
        private async Task<int?> GetCompanyIdByProfileUrlAsync(string profileUrl)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();

            command.CommandText = @"
            SELECT Id
            FROM Companies
            WHERE ProfileUrl = @ProfileUrl
            LIMIT 1;
            ";

            command.Parameters.AddWithValue("@ProfileUrl", profileUrl);

            var result = await command.ExecuteScalarAsync();

            if (result == null || result == DBNull.Value)
                return null;

            return Convert.ToInt32(result);
        }


        private async Task<(int SearchId, int CurrentPage, string CurrentLocalityUrl)?> GetStoppedSearchProgressAsync(string originalUrl, bool onlyWithEmail)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        SELECT s.Id, COALESCE(sp.CurrentPage, 1), COALESCE(sp.CurrentLocalityUrl, '')
        FROM Searches s
        LEFT JOIN SearchProgress sp ON sp.SearchId = s.Id
        WHERE s.OriginalUrl = @OriginalUrl
          AND s.OnlyWithEmail = @OnlyWithEmail
          AND s.Status IN ('Detenida', 'En curso')
        ORDER BY s.Id DESC
        LIMIT 1;
    ";
            command.Parameters.AddWithValue("@OriginalUrl", originalUrl);
            command.Parameters.AddWithValue("@OnlyWithEmail", onlyWithEmail ? 1 : 0);

            using var reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
                return (reader.GetInt32(0), reader.GetInt32(1), reader.GetString(2));

            return null;
        }

        private async Task ResumeSearchAsync(int searchId)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
        UPDATE Searches
        SET Status = 'En curso',
            FinishedAt = NULL,
            StopReason = NULL
        WHERE Id = @SearchId;
    ";
            command.Parameters.AddWithValue("@SearchId", searchId);
            await command.ExecuteNonQueryAsync();
        }











    }
}
 
 