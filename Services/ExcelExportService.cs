using ClosedXML.Excel;
using Microsoft.Data.Sqlite;

namespace SealLead.Services
{ 

public static class ExcelExportService
{

    public static void ExportCompanies(string connectionString, string filePath)
    {
        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT 
    CompanyName,
    Email,
    Phone,
    Address,
    LegalName,
    Cif,
    LegalForm,
    Sector,
    Activity,
    CnaeActivity,
    ProfileUrl,
    EmailStatus,
    EmailSentCount,
    CompanyStatus,
    CreatedAt,
    UpdatedAt
FROM Companies
ORDER BY CreatedAt DESC;
";

        using var reader = command.ExecuteReader();

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Empresas");

        for (int i = 0; i < reader.FieldCount; i++)
            worksheet.Cell(1, i + 1).Value = reader.GetName(i);

        int row = 2;

        while (reader.Read())
        {
            for (int col = 0; col < reader.FieldCount; col++)
            {
                worksheet.Cell(row, col + 1).Value =
                    reader.IsDBNull(col) ? "" : reader.GetValue(col)?.ToString();
            }

            row++;
        }

        worksheet.Columns().AdjustToContents();
        worksheet.Row(1).Style.Font.Bold = true;

        workbook.SaveAs(filePath);
    }

    public static void ExportDataTable(System.Data.DataTable table, string filePath)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Empresas");

        for (int i = 0; i < table.Columns.Count; i++)
            worksheet.Cell(1, i + 1).Value = table.Columns[i].ColumnName;

        for (int row = 0; row < table.Rows.Count; row++)
            for (int col = 0; col < table.Columns.Count; col++)
                worksheet.Cell(row + 2, col + 1).Value = table.Rows[row][col]?.ToString() ?? "";

        worksheet.Columns().AdjustToContents();
        worksheet.Row(1).Style.Font.Bold = true;
        workbook.SaveAs(filePath);
    }
}}