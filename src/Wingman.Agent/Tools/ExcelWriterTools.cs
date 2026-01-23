using System.ComponentModel;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using ClosedXML.Excel;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent.Tools;

[SupportedOSPlatform("windows")]
public static class ExcelWriterTools
{
    private const string ApprovalPhrase = "I_APPROVE_FILE_CHANGES";

    [Description(@"Creates an Excel file (.xlsx) from CSV-formatted content. The LLM provides data in CSV format, and this tool saves it as a proper Excel spreadsheet.

The CSV content should follow these rules:
- First row contains column headers
- Values separated by commas
- Rows separated by newlines
- Values with commas/quotes wrapped in double quotes
- Double quotes escaped by doubling them

Example CSV content: ""Name,Age,City\nAlice,30,New York\nBob,25,Los Angeles""")]
    public static string WriteCsvAsExcel(
        [Description("The file path to save the Excel file. Should end with .xlsx extension.")] string filePath,
        [Description("The CSV-formatted content to write. First row should be headers.")] string csvContent,
        [Description("Optional name for the worksheet. Defaults to 'Sheet1'.")] string sheetName = "Sheet1",
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to create the file.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to create file. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(csvContent);

        var resolvedPath = EnsureXlsxExtension(filePath.ResolvePathWithFileName());
        EnsureDirectoryExists(resolvedPath);

        var rows = ParseCsvContent(csvContent);
        if (rows.Count == 0)
            throw new ArgumentException("CSV content is empty or invalid.");

        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        WriteDataToWorksheet(worksheet, rows);
        FormatHeaderRow(worksheet);
        worksheet.Columns().AdjustToContents();
        workbook.SaveAs(resolvedPath);

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            created = true,
            sheetName,
            rowCount = rows.Count,
            columnCount = rows.Count > 0 ? rows[0].Count : 0,
            fileSizeBytes = new FileInfo(resolvedPath).Length,
            headers = rows.Count > 0 ? rows[0] : new List<string>()
        });
    }

    private static string EnsureXlsxExtension(string path)
    {
        return path.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase)
            ? path
            : Path.ChangeExtension(path, ".xlsx");
    }

    private static void EnsureDirectoryExists(string filePath)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    private static void WriteDataToWorksheet(IXLWorksheet worksheet, List<List<string>> rows)
    {
        for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            var row = rows[rowIndex];
            for (int colIndex = 0; colIndex < row.Count; colIndex++)
            {
                var cell = worksheet.Cell(rowIndex + 1, colIndex + 1);
                SetCellValue(cell, row[colIndex]);
            }
        }
    }

    private static void SetCellValue(IXLCell cell, string value)
    {
        if (double.TryParse(value, out var numericValue))
            cell.Value = numericValue;
        else if (DateTime.TryParse(value, out var dateValue))
            cell.Value = dateValue;
        else if (bool.TryParse(value, out var boolValue))
            cell.Value = boolValue;
        else
            cell.Value = value;
    }

    private static void FormatHeaderRow(IXLWorksheet worksheet)
    {
        var headerRow = worksheet.Row(1);
        headerRow.Style.Font.Bold = true;
        headerRow.Style.Fill.BackgroundColor = XLColor.LightGray;
    }

    private static List<List<string>> ParseCsvContent(string csvContent)
    {
        var rows = new List<List<string>>();
        var lines = csvContent.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var row = ParseCsvLine(line);
            rows.Add(row);
        }

        return rows;
    }

    private static List<string> ParseCsvLine(string line)
    {
        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];

            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == ',' && !inQuotes)
            {
                result.Add(current.ToString().Trim());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString().Trim());
        return result;
    }
}
