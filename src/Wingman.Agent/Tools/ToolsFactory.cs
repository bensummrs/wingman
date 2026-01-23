using System.Runtime.Versioning;
using Microsoft.Extensions.AI;

namespace Wingman.Agent.Tools;

[SupportedOSPlatform("windows")]
public static class ToolsFactory
{
    public static IReadOnlyList<AITool> CreateDefaultTools()
    {
        return [.. CreateFileOrganizerTools(), .. CreatePdfTools(), .. CreateSpreadsheetTools()];
    }

    public static IReadOnlyList<AITool> CreatePdfTools()
    {
        return
        [
            AIFunctionFactory.Create(PdfTools.ReadPdf),
            AIFunctionFactory.Create(PdfTools.GetPdfInfo),
        ];
    }

    public static IReadOnlyList<AITool> CreateFileOrganizerTools()
    {
        return
        [
            AIFunctionFactory.Create(FileOrganizerTools.ResolvePath),
            AIFunctionFactory.Create(FileOrganizerTools.FindDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.ListDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.SearchFiles),
            
            AIFunctionFactory.Create(FileOrganizerTools.MoveItem),
            AIFunctionFactory.Create(FileOrganizerTools.CopyFile),
            AIFunctionFactory.Create(FileOrganizerTools.CreateDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.DeleteItem),
            AIFunctionFactory.Create(FileOrganizerTools.WriteFile),
            AIFunctionFactory.Create(FileOrganizerTools.ReadFile),
            
            AIFunctionFactory.Create(FileOrganizerTools.PreviewOrganizeByExtension),
            AIFunctionFactory.Create(FileOrganizerTools.ApplyOrganizationPlan),
        ];
    }

    public static IReadOnlyList<AITool> CreateSpreadsheetTools()
    {
        return
        [
            AIFunctionFactory.Create(SpreadsheetTools.ReadExcel),
            AIFunctionFactory.Create(SpreadsheetTools.GetExcelInfo),
            AIFunctionFactory.Create(SpreadsheetTools.ReadCsv),
            AIFunctionFactory.Create(SpreadsheetTools.GetCsvInfo),
            AIFunctionFactory.Create(SpreadsheetTools.QuerySpreadsheet),
        ];
    }

    public static IReadOnlyList<AITool> CreateSpreadsheetCreatorTools()
    {
        return
        [
            AIFunctionFactory.Create(ExcelWriterTools.WriteCsvAsExcel),
            AIFunctionFactory.Create(FileOrganizerTools.ListDirectory),
            AIFunctionFactory.Create(FileOrganizerTools.ResolvePath),
        ];
    }
}
