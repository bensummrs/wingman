using System.ComponentModel;
using System.Text;
using System.Text.Json;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;

namespace Wingman.Agent.Tools;

public static class PdfTools
{
    [Description("Opens and reads the text content from a PDF file. Returns the extracted text from all pages or specific pages.")]
    public static string ReadPdf(
        [Description("The full path to the PDF file to read.")] string filePath,
        [Description("Optional: Starting page number (1-based). If not provided, reads from the first page.")] int? startPage = null,
        [Description("Optional: Ending page number (1-based). If not provided, reads to the last page.")] int? endPage = null)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is required.", nameof(filePath));

        var resolvedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(filePath));

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"PDF file not found: {resolvedPath}");

        using var document = PdfDocument.Open(resolvedPath);

        int totalPages = document.NumberOfPages;
        int fromPage = Math.Max(1, startPage ?? 1);
        int toPage = Math.Min(totalPages, endPage ?? totalPages);

        if (fromPage > toPage)
            throw new ArgumentException($"startPage ({fromPage}) cannot be greater than endPage ({toPage}).");

        var pages = new List<object>();
        var fullText = new StringBuilder();

        for (int i = fromPage; i <= toPage; i++)
        {
            Page page = document.GetPage(i);
            string pageText = page.Text;

            pages.Add(new
            {
                pageNumber = i,
                text = pageText,
                wordCount = page.GetWords().Count()
            });

            fullText.AppendLine(pageText);
        }

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            totalPages,
            pagesRead = new { from = fromPage, to = toPage },
            pages,
            combinedText = fullText.ToString().Trim()
        });
    }

    [Description("Gets metadata and information about a PDF file without reading all the content.")]
    public static string GetPdfInfo(
        [Description("The full path to the PDF file.")] string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("filePath is required.", nameof(filePath));

        var resolvedPath = Path.GetFullPath(Environment.ExpandEnvironmentVariables(filePath));

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"PDF file not found: {resolvedPath}");

        using var document = PdfDocument.Open(resolvedPath);

        var info = document.Information;

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            totalPages = document.NumberOfPages,
            title = info.Title,
            author = info.Author,
            subject = info.Subject,
            keywords = info.Keywords,
            creator = info.Creator,
            producer = info.Producer,
            creationDate = info.CreationDate,
            modifiedDate = info.ModifiedDate
        });
    }
}
