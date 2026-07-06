using Microsoft.Data.Sqlite;
using System.Data;

namespace SealLead.Services
{
    public class CompanyQueryService
    {
        private readonly string _connectionString;

        public CompanyQueryService(string connectionString)
        {
            _connectionString = connectionString;
        }

        public async Task<List<string>> GetDistinctValuesAsync(string column)
        {
            var result = new List<string> { "Todos" };

            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = $@"
SELECT DISTINCT [{column}]
FROM Companies
WHERE [{column}] IS NOT NULL AND [{column}] != ''
ORDER BY [{column}];";

            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                result.Add(reader.GetString(0));

            return result;
        }

        public async Task<DataTable> GetFilteredCompaniesAsync(
            string sector,
            string activity,
            string cnae,
            string keyword,
            string emailStatus,
            string companyStatus)
        {
            using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var command = connection.CreateCommand();
            command.CommandText = @"
SELECT
    CompanyName     AS 'Empresa',
    Email,
    EmailStatus     AS 'Estado Email',
    Sector,
    Activity        AS 'Actividad',
    CnaeActivity    AS 'CNAE',
    Address         AS 'Dirección',
    LegalName       AS 'Razón Social',
    Cif,
    LegalForm       AS 'Forma Jurídica',
    SearchKeywords  AS 'Keyword',
    CompanyStatus   AS 'Estado Empresa',
    ProfileUrl      AS 'URL',
    CreatedAt       AS 'Creada'
FROM Companies
WHERE (@Sector       = '' OR Sector        = @Sector)
  AND (@Activity     = '' OR Activity      = @Activity)
  AND (@Cnae         = '' OR CnaeActivity  = @Cnae)
  AND (@Keyword      = '' OR SearchKeywords = @Keyword)
  AND (@EmailStatus  = '' OR EmailStatus   = @EmailStatus)
  AND (@CompanyStatus= '' OR CompanyStatus = @CompanyStatus)
ORDER BY CreatedAt DESC;";

            command.Parameters.AddWithValue("@Sector",        sector        == "Todos" ? "" : sector);
            command.Parameters.AddWithValue("@Activity",      activity      == "Todos" ? "" : activity);
            command.Parameters.AddWithValue("@Cnae",          cnae          == "Todos" ? "" : cnae);
            command.Parameters.AddWithValue("@Keyword",       keyword       == "Todos" ? "" : keyword);
            command.Parameters.AddWithValue("@EmailStatus",   emailStatus   == "Todos" ? "" : emailStatus);
            command.Parameters.AddWithValue("@CompanyStatus", companyStatus == "Todos" ? "" : companyStatus);

            var table = new DataTable();
            using var reader = await command.ExecuteReaderAsync();
            table.Load(reader);
            return table;
        }
    }
}
