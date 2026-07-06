# SealLead — Contexto del proyecto

## Descripción
Aplicación de escritorio Windows Forms en C# / .NET 10.
Permite introducir una URL de Empresite, paginar el listado, extraer empresas y guardar datos en SQLite.

## Estructura de archivos
```
SealLead/
├── Data/
│   └── CompanyData.cs          # Modelo de datos de empresa
├── Services/
│   ├── CompanyScraperService.cs # Lógica de scraping y acceso a BD
│   └── ExcelExportService.cs   # Exportación a Excel con ClosedXML
├── DatabaseService.cs          # Inicialización de SQLite y cadena de conexión
├── Form1.cs                    # Formulario principal
├── Form1.Designer.cs
└── Program.cs
```

## Base de datos
SQLite. El archivo se crea en `bin/Debug/net10.0-windows/Data/SealScout.db`.
Cadena de conexión en `DatabaseService.ConnectionString`.

### Tablas
- **AppUsers**: usuarios de la app. Usuario por defecto: Id=1, Laura, laura.horna@factum.es
- **Searches**: cada búsqueda lanzada. Status: `En curso` / `Detenida` / `Finalizada`
- **Companies**: empresas extraídas. `ProfileUrl` es UNIQUE.
- **SearchResults**: relación N:N entre Searches y Companies. UNIQUE(SearchId, CompanyId)
- **SearchProgress**: página actual y última URL procesada por búsqueda. UNIQUE(SearchId)
- **EmailHistory**: historial de emails enviados
- **Notes**: notas por empresa

### Campos relevantes de Companies
`CompanyName`, `Email`, `Phone`, `ProfileUrl` (UNIQUE), `Address`, `LegalName`, `Cif`, `LegalForm`, `Sector`, `Activity`, `CnaeActivity`, `SearchKeywords`, `EmailStatus`, `EmailSentCount`, `CompanyStatus`

## Flujo de scraping (CompanyScraperService)
1. Construye URL con filtro de email si el checkbox está marcado (`?testfiltros=1&emp_email=true`)
2. Pagina el listado extrayendo `div.cardCompany` (nombre, ProfileUrl, dirección)
3. Por cada empresa del listado:
   - Comprueba en BD si `ProfileUrl` ya existe (`GetCompanyIdByProfileUrlAsync`)
   - Si existe → inserta relación en SearchResults, guarda progreso, **no hace llamada HTTP**
   - Si no existe → espera delay humano (8-18s), llama a la ficha, extrae datos, guarda en BD
4. Guarda progreso en `SearchProgress` (página actual + última URL procesada)
5. Si recibe HTTP 429, 403 o 503 → marca búsqueda como `Detenida` y para

### Métodos principales
- `StartSearchAsync(originalUrl, onlyWithEmail, userId, log)` — lanza o retoma búsqueda
- `GetCompanyIdByProfileUrlAsync(profileUrl)` — comprueba si empresa ya existe en BD
- `UpsertCompanyAsync(company)` — INSERT OR UPDATE en Companies
- `InsertSearchResultAsync(searchId, companyId)` — INSERT OR IGNORE en SearchResults
- `SaveProgressAsync(searchId, page, lastCompanyUrl)` — guarda progreso
- `StopSearchAsync(searchId, reason)` — marca como Detenida
- `FinishSearchAsync(searchId, total)` — marca como Finalizada
- `GetHtmlAsync(url)` — lanza excepción ante 429/403/503

## Estado actual del desarrollo
- [x] Scraping del listado y fichas de Empresite
- [x] Guardado en SQLite
- [x] Deduplicación por ProfileUrl (no repite llamadas HTTP a fichas ya guardadas)
- [x] Detección de HTTP 429/403/503 y parada limpia
- [x] Guardado de progreso en SearchProgress
- [x] Exportación a Excel (ClosedXML)
- [x] Delay solo antes de llamadas HTTP (no en empresas ya existentes en BD)
- [ ] Botón para continuar búsqueda detenida desde SearchProgress
- [ ] Importar TXT de emails
- [ ] Gestión de cuentas SMTP y envío de emails
- [ ] Plantillas de email con etiquetas ({{nombreempresa}}, {{email}}, etc.)
- [ ] Tabla filtrable de empresas
- [ ] Historial de envíos

## Reglas obligatorias para modificaciones
1. No reestructurar el proyecto completo
2. No cambiar nombres de clases, formularios, controles, namespaces ni archivos salvo que sea imprescindible
3. No borrar código existente
4. No sustituir archivos completos salvo petición expresa
5. Antes de modificar, indicar: archivo, método, bloque a sustituir, bloque nuevo, motivo
6. Si se crea un archivo nuevo, indicar: nombre, carpeta y código completo
7. Si el cambio puede romper algo existente, avisar y esperar OK
8. Trabajar funcionalidad a funcionalidad, una por una
9. No generar soluciones grandes de golpe
10. Priorizar que lo que ya funciona siga funcionando

## Dependencias NuGet
- `Microsoft.Data.Sqlite`
- `HtmlAgilityPack`
- `ClosedXML`
