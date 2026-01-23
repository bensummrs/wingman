using System.ComponentModel;
using System.Data;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Text;
using System.Text.Json;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent.Tools;

[SupportedOSPlatform("windows")]
public static class SpreadsheetTools
{
    [Description("Reads data from an Excel file (.xls or .xlsx). Returns the data as structured JSON with rows and columns.")]
    public static string ReadExcel(
        [Description("The full path to the Excel file.")] string filePath,
        [Description("Optional: The name of the sheet to read. If not provided, reads the first sheet.")] string? sheetName = null,
        [Description("If true, treats the first row as column headers.")] bool hasHeaders = true,
        [Description("Maximum number of rows to return (0 = all rows).")] int maxRows = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Excel file not found: {resolvedPath}");

        var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();
        if (extension != ".xls" && extension != ".xlsx")
            throw new ArgumentException($"Invalid file type. Expected .xls or .xlsx, got: {extension}");

        var connectionString = GetExcelConnectionString(resolvedPath, hasHeaders);

        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        var schemaTable = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
        var sheetNames = new List<string>();
        
        if (schemaTable != null)
        {
            foreach (DataRow row in schemaTable.Rows)
            {
                var tableName = row["TABLE_NAME"]?.ToString();
                if (!string.IsNullOrEmpty(tableName) && tableName.EndsWith("$"))
                {
                    sheetNames.Add(tableName.TrimEnd('$'));
                }
            }
        }

        var targetSheet = sheetName ?? sheetNames.FirstOrDefault()
            ?? throw new InvalidOperationException("No sheets found in the Excel file.");

        if (!string.IsNullOrEmpty(sheetName) && !sheetNames.Contains(sheetName, StringComparer.OrdinalIgnoreCase))
        {
            var available = string.Join(", ", sheetNames);
            throw new ArgumentException($"Sheet '{sheetName}' not found. Available sheets: {available}");
        }

        var query = $"SELECT * FROM [{targetSheet}$]";
        using var command = new OleDbCommand(query, connection);
        using var adapter = new OleDbDataAdapter(command);
        
        var dataTable = new DataTable();
        adapter.Fill(dataTable);

        var columns = dataTable.Columns.Cast<DataColumn>()
            .Select(c => c.ColumnName)
            .ToList();

        var rows = new List<Dictionary<string, object?>>();
        var rowCount = maxRows > 0 ? Math.Min(maxRows, dataTable.Rows.Count) : dataTable.Rows.Count;

        for (int i = 0; i < rowCount; i++)
        {
            var row = dataTable.Rows[i];
            var rowDict = new Dictionary<string, object?>();
            
            foreach (var col in columns)
            {
                var value = row[col];
                rowDict[col] = value == DBNull.Value ? null : value;
            }
            
            rows.Add(rowDict);
        }

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            sheetName = targetSheet,
            availableSheets = sheetNames,
            hasHeaders,
            columnCount = columns.Count,
            columns,
            rowCount = rows.Count,
            totalRowsInSheet = dataTable.Rows.Count,
            rows
        });
    }

    [Description("Gets information about an Excel file including sheet names and basic statistics without reading all data.")]
    public static string GetExcelInfo(
        [Description("The full path to the Excel file.")] string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"Excel file not found: {resolvedPath}");

        var connectionString = GetExcelConnectionString(resolvedPath, hasHeaders: true);

        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        var schemaTable = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
        var sheets = new List<object>();

        if (schemaTable != null)
        {
            foreach (DataRow row in schemaTable.Rows)
            {
                var tableName = row["TABLE_NAME"]?.ToString();
                if (!string.IsNullOrEmpty(tableName) && tableName.EndsWith("$"))
                {
                    var sheetName = tableName.TrimEnd('$');
                    
                    var countQuery = $"SELECT COUNT(*) FROM [{tableName}]";
                    using var countCmd = new OleDbCommand(countQuery, connection);
                    var rowCount = Convert.ToInt32(countCmd.ExecuteScalar());

                    var sampleQuery = $"SELECT TOP 1 * FROM [{tableName}]";
                    using var sampleCmd = new OleDbCommand(sampleQuery, connection);
                    using var reader = sampleCmd.ExecuteReader();
                    
                    var columns = new List<string>();
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        columns.Add(reader.GetName(i));
                    }

                    sheets.Add(new
                    {
                        name = sheetName,
                        rowCount,
                        columnCount = columns.Count,
                        columns
                    });
                }
            }
        }

        var fileInfo = new FileInfo(resolvedPath);

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            fileName = fileInfo.Name,
            fileSizeBytes = fileInfo.Length,
            lastModified = fileInfo.LastWriteTimeUtc,
            sheetCount = sheets.Count,
            sheets
        });
    }

    [Description("Reads data from a CSV file. Returns the data as structured JSON with rows and columns.")]
    public static string ReadCsv(
        [Description("The full path to the CSV file.")] string filePath,
        [Description("The delimiter character used in the CSV (default is comma).")] string delimiter = ",",
        [Description("If true, treats the first row as column headers.")] bool hasHeaders = true,
        [Description("Maximum number of rows to return (0 = all rows).")] int maxRows = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"CSV file not found: {resolvedPath}");

        var lines = File.ReadAllLines(resolvedPath);
        
        if (lines.Length == 0)
        {
            return JsonSerializer.Serialize(new
            {
                filePath = resolvedPath,
                hasHeaders,
                columnCount = 0,
                columns = Array.Empty<string>(),
                rowCount = 0,
                rows = Array.Empty<object>()
            });
        }

        var delimChar = delimiter.Length > 0 ? delimiter[0] : ',';
        var columns = new List<string>();
        var rows = new List<Dictionary<string, object?>>();
        
        int startIndex = 0;
        
        if (hasHeaders && lines.Length > 0)
        {
            columns = ParseCsvLine(lines[0], delimChar);
            startIndex = 1;
        }
        else if (lines.Length > 0)
        {
            var firstRow = ParseCsvLine(lines[0], delimChar);
            for (int i = 0; i < firstRow.Count; i++)
            {
                columns.Add($"Column{i + 1}");
            }
        }

        var rowLimit = maxRows > 0 ? Math.Min(startIndex + maxRows, lines.Length) : lines.Length;

        for (int i = startIndex; i < rowLimit; i++)
        {
            var values = ParseCsvLine(lines[i], delimChar);
            var rowDict = new Dictionary<string, object?>();

            for (int j = 0; j < columns.Count; j++)
            {
                var value = j < values.Count ? values[j] : null;
                rowDict[columns[j]] = string.IsNullOrEmpty(value) ? null : ParseValue(value);
            }

            rows.Add(rowDict);
        }

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            delimiter,
            hasHeaders,
            columnCount = columns.Count,
            columns,
            rowCount = rows.Count,
            totalRowsInFile = lines.Length - (hasHeaders ? 1 : 0),
            rows
        });
    }

    [Description("Gets information about a CSV file including column names and row count without reading all data.")]
    public static string GetCsvInfo(
        [Description("The full path to the CSV file.")] string filePath,
        [Description("The delimiter character used in the CSV (default is comma).")] string delimiter = ",",
        [Description("If true, treats the first row as column headers.")] bool hasHeaders = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"CSV file not found: {resolvedPath}");

        var fileInfo = new FileInfo(resolvedPath);
        var lines = File.ReadAllLines(resolvedPath);
        
        var delimChar = delimiter.Length > 0 ? delimiter[0] : ',';
        var columns = new List<string>();
        
        if (lines.Length > 0)
        {
            if (hasHeaders)
            {
                columns = ParseCsvLine(lines[0], delimChar);
            }
            else
            {
                var firstRow = ParseCsvLine(lines[0], delimChar);
                for (int i = 0; i < firstRow.Count; i++)
                {
                    columns.Add($"Column{i + 1}");
                }
            }
        }

        var dataRows = hasHeaders ? lines.Length - 1 : lines.Length;

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            fileName = fileInfo.Name,
            fileSizeBytes = fileInfo.Length,
            lastModified = fileInfo.LastWriteTimeUtc,
            delimiter,
            hasHeaders,
            columnCount = columns.Count,
            columns,
            rowCount = Math.Max(0, dataRows)
        });
    }

    [Description("Queries data from a spreadsheet (Excel or CSV) using a SQL-like filter expression.")]
    public static string QuerySpreadsheet(
        [Description("The full path to the Excel or CSV file.")] string filePath,
        [Description("Column names to select (comma-separated). Use '*' for all columns.")] string selectColumns = "*",
        [Description("Optional: Filter expression (e.g., 'Age > 30', 'Name = \"John\"').")] string? whereClause = null,
        [Description("Optional: Column to sort by.")] string? orderBy = null,
        [Description("Sort descending if true.")] bool descending = false,
        [Description("Maximum number of rows to return.")] int maxRows = 100,
        [Description("For Excel files: sheet name to query.")] string? sheetName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();
        var extension = Path.GetExtension(resolvedPath).ToLowerInvariant();

        if (extension == ".csv")
        {
            return QueryCsvWithFilter(resolvedPath, selectColumns, whereClause, orderBy, descending, maxRows);
        }
        else if (extension == ".xls" || extension == ".xlsx")
        {
            return QueryExcelWithSql(resolvedPath, selectColumns, whereClause, orderBy, descending, maxRows, sheetName);
        }
        else
        {
            throw new ArgumentException($"Unsupported file type: {extension}. Expected .csv, .xls, or .xlsx");
        }
    }

    private static string QueryExcelWithSql(string filePath, string selectColumns, string? whereClause,
        string? orderBy, bool descending, int maxRows, string? sheetName)
    {
        var connectionString = GetExcelConnectionString(filePath, hasHeaders: true);

        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        if (string.IsNullOrEmpty(sheetName))
        {
            var schemaTable = connection.GetOleDbSchemaTable(OleDbSchemaGuid.Tables, null);
            if (schemaTable?.Rows.Count > 0)
            {
                sheetName = schemaTable.Rows[0]["TABLE_NAME"]?.ToString()?.TrimEnd('$');
            }
        }

        if (string.IsNullOrEmpty(sheetName))
            throw new InvalidOperationException("Could not determine sheet name.");

        var query = new StringBuilder();
        query.Append($"SELECT TOP {maxRows} ");
        query.Append(selectColumns == "*" ? "*" : selectColumns);
        query.Append($" FROM [{sheetName}$]");

        if (!string.IsNullOrEmpty(whereClause))
        {
            query.Append($" WHERE {whereClause}");
        }

        if (!string.IsNullOrEmpty(orderBy))
        {
            query.Append($" ORDER BY [{orderBy}]");
            if (descending) query.Append(" DESC");
        }

        using var command = new OleDbCommand(query.ToString(), connection);
        using var adapter = new OleDbDataAdapter(command);

        var dataTable = new DataTable();
        adapter.Fill(dataTable);

        var columns = dataTable.Columns.Cast<DataColumn>().Select(c => c.ColumnName).ToList();
        var rows = new List<Dictionary<string, object?>>();

        foreach (DataRow row in dataTable.Rows)
        {
            var rowDict = columns.ToDictionary(
                col => col,
                col => row[col] == DBNull.Value ? null : row[col]);
            rows.Add(rowDict);
        }

        return JsonSerializer.Serialize(new
        {
            filePath,
            sheetName,
            query = query.ToString(),
            columnCount = columns.Count,
            columns,
            rowCount = rows.Count,
            rows
        });
    }

    private static string QueryCsvWithFilter(string filePath, string selectColumns, string? whereClause,
        string? orderBy, bool descending, int maxRows)
    {
        // Read CSV first
        var lines = File.ReadAllLines(filePath);
        if (lines.Length == 0)
        {
            return JsonSerializer.Serialize(new
            {
                filePath,
                query = "N/A",
                columnCount = 0,
                columns = Array.Empty<string>(),
                rowCount = 0,
                rows = Array.Empty<object>()
            });
        }

        var allColumns = ParseCsvLine(lines[0], ',');
        var selectedCols = selectColumns == "*"
            ? allColumns
            : selectColumns.Split(',').Select(c => c.Trim()).ToList();

        var allRows = new List<Dictionary<string, object?>>();
        for (int i = 1; i < lines.Length; i++)
        {
            var values = ParseCsvLine(lines[i], ',');
            var rowDict = new Dictionary<string, object?>();
            
            for (int j = 0; j < allColumns.Count; j++)
            {
                var value = j < values.Count ? values[j] : null;
                rowDict[allColumns[j]] = string.IsNullOrEmpty(value) ? null : ParseValue(value);
            }
            
            allRows.Add(rowDict);
        }

        IEnumerable<Dictionary<string, object?>> filteredRows = allRows;
        
        if (!string.IsNullOrEmpty(whereClause))
        {
            filteredRows = ApplySimpleFilter(allRows, whereClause);
        }

        if (!string.IsNullOrEmpty(orderBy) && allColumns.Contains(orderBy, StringComparer.OrdinalIgnoreCase))
        {
            filteredRows = descending
                ? filteredRows.OrderByDescending(r => r.GetValueOrDefault(orderBy))
                : filteredRows.OrderBy(r => r.GetValueOrDefault(orderBy));
        }

        var resultRows = filteredRows
            .Take(maxRows)
            .Select(row => selectedCols.ToDictionary(
                col => col,
                col => row.GetValueOrDefault(col)))
            .ToList();

        return JsonSerializer.Serialize(new
        {
            filePath,
            query = $"SELECT {selectColumns}" + 
                   (whereClause != null ? $" WHERE {whereClause}" : "") +
                   (orderBy != null ? $" ORDER BY {orderBy}" + (descending ? " DESC" : "") : ""),
            columnCount = selectedCols.Count,
            columns = selectedCols,
            rowCount = resultRows.Count,
            rows = resultRows
        });
    }

    private static IEnumerable<Dictionary<string, object?>> ApplySimpleFilter(
        List<Dictionary<string, object?>> rows, string whereClause)
    {

        var parts = whereClause.Split(['=', '>', '<'], 2);
        if (parts.Length != 2) return rows;

        var column = parts[0].Trim().Trim('"', '\'', '[', ']');
        var valueStr = parts[1].Trim().Trim('"', '\'');
        var op = whereClause.Contains(">=") ? ">=" :
                 whereClause.Contains("<=") ? "<=" :
                 whereClause.Contains('>') ? ">" :
                 whereClause.Contains('<') ? "<" : "=";

        return rows.Where(row =>
        {
            if (!row.TryGetValue(column, out var cellValue) || cellValue == null)
                return false;

            var cellStr = cellValue.ToString() ?? "";

            return op switch
            {
                "=" => cellStr.Equals(valueStr, StringComparison.OrdinalIgnoreCase),
                ">" when double.TryParse(cellStr, out var cv) && double.TryParse(valueStr, out var fv) => cv > fv,
                "<" when double.TryParse(cellStr, out var cv) && double.TryParse(valueStr, out var fv) => cv < fv,
                ">=" when double.TryParse(cellStr, out var cv) && double.TryParse(valueStr, out var fv) => cv >= fv,
                "<=" when double.TryParse(cellStr, out var cv) && double.TryParse(valueStr, out var fv) => cv <= fv,
                _ => false
            };
        });
    }

    private static string GetExcelConnectionString(string filePath, bool hasHeaders)
    {
        var extension = Path.GetExtension(filePath).ToLowerInvariant();
        var hdr = hasHeaders ? "Yes" : "No";

        return extension switch
        {
            ".xlsx" => $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties='Excel 12.0 Xml;HDR={hdr};IMEX=1'",
            ".xls" => $"Provider=Microsoft.ACE.OLEDB.12.0;Data Source={filePath};Extended Properties='Excel 8.0;HDR={hdr};IMEX=1'",
            _ => throw new ArgumentException($"Unsupported Excel format: {extension}")
        };
    }

    private static List<string> ParseCsvLine(string line, char delimiter)
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
            else if (c == delimiter && !inQuotes)
            {
                result.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        result.Add(current.ToString());
        return result;
    }

    private static object ParseValue(string value)
    {
        if (double.TryParse(value, out var d))
            return d;
        if (DateTime.TryParse(value, out var dt))
            return dt;
        if (bool.TryParse(value, out var b))
            return b;
        return value;
    }
}
