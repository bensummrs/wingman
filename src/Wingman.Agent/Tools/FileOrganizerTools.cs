using System.ComponentModel;
using System.Text.Json;

namespace Wingman.Agent.Tools;

public static class FileOrganizerTools
{
    private const string ApprovalPhrase = "I_APPROVE_FILE_CHANGES";

    [Description("Lists the files in a directory (non-recursive) with basic metadata.")]
    public static string ListDirectory(
        [Description("Full path to the directory to list.")] string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath is required.", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var files = Directory.EnumerateFiles(directoryPath)
            .Select(p => new FileInfo(p))
            .Select(f => new
            {
                name = f.Name,
                fullPath = f.FullName,
                extension = f.Extension,
                sizeBytes = f.Length,
                lastWriteTimeUtc = f.LastWriteTimeUtc,
            })
            .OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            directoryPath,
            fileCount = files.Length,
            files,
        });
    }

    [Description("Creates a preview plan to organize files in a directory by extension. Does not modify the file system.")]
    public static string PreviewOrganizeByExtension(
        [Description("Full path to the directory to organize.")] string directoryPath,
        [Description("If true, include hidden files (Windows hidden attribute).")] bool includeHidden = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
            throw new ArgumentException("directoryPath is required.", nameof(directoryPath));

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var moves = new List<FileMove>();

        foreach (var filePath in Directory.EnumerateFiles(directoryPath))
        {
            var info = new FileInfo(filePath);

            if (!includeHidden && info.Attributes.HasFlag(FileAttributes.Hidden))
                continue;

            var bucket = ExtensionToFolder(info.Extension);
            var destinationDir = Path.Combine(directoryPath, bucket);
            var destinationPath = Path.Combine(destinationDir, info.Name);

            if (string.Equals(Path.GetDirectoryName(info.FullName), destinationDir, StringComparison.OrdinalIgnoreCase))
                continue;

            moves.Add(new FileMove(info.FullName, destinationPath));
        }

        var summary = moves
            .GroupBy(m => Path.GetDirectoryName(m.Destination) ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { destinationFolder = g.Key, count = g.Count() })
            .OrderByDescending(x => x.count)
            .ThenBy(x => x.destinationFolder, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var plan = new OrganizationPlan(
            DirectoryPath: directoryPath,
            Strategy: "by_extension",
            Moves: moves.ToArray());

        return JsonSerializer.Serialize(new
        {
            plan,
            summary,
            safety = new
            {
                note = "This is a preview. To apply, call ApplyOrganizationPlan with the planJson and the required approvalPhrase.",
                requiredApprovalPhrase = ApprovalPhrase,
            },
        });
    }

    [Description("Applies a previously generated organization plan (moves files). Requires explicit approval phrase.")]
    public static string ApplyOrganizationPlan(
        [Description("The JSON returned by PreviewOrganizeByExtension (or its 'plan' sub-object serialized).")] string planJson,
        [Description("Must exactly equal the required approval phrase to perform file changes.")] string approvalPhrase)
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to modify files. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        if (string.IsNullOrWhiteSpace(planJson))
            throw new ArgumentException("planJson is required.", nameof(planJson));

        OrganizationPlan? plan = TryDeserializeWrappedPlan(planJson) ?? JsonSerializer.Deserialize<OrganizationPlan>(planJson);
        if (plan is null)
            throw new InvalidOperationException("Could not parse planJson into an OrganizationPlan.");

        if (!Directory.Exists(plan.DirectoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {plan.DirectoryPath}");

        var applied = new List<AppliedMove>();

        foreach (var move in plan.Moves)
        {
            if (!File.Exists(move.Source))
                continue;

            var destinationDir = Path.GetDirectoryName(move.Destination);
            if (string.IsNullOrWhiteSpace(destinationDir))
                throw new InvalidOperationException($"Invalid destination path: {move.Destination}");

            Directory.CreateDirectory(destinationDir);

            var finalDestination = GetNonCollidingPath(move.Destination);
            File.Move(move.Source, finalDestination);

            applied.Add(new AppliedMove(move.Source, finalDestination));
        }

        return JsonSerializer.Serialize(new
        {
            planDirectoryPath = plan.DirectoryPath,
            appliedCount = applied.Count,
            appliedMoves = applied,
        });
    }

    private static OrganizationPlan? TryDeserializeWrappedPlan(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("plan", out var planEl))
            {
                return planEl.Deserialize<OrganizationPlan>();
            }
        }
        catch
        {
        }

        return null;
    }

    private static string ExtensionToFolder(string extension)
    {
        extension = (extension ?? string.Empty).Trim().ToLowerInvariant();

        return extension switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" or ".bmp" or ".tiff" => "Images",
            ".mp4" or ".mov" or ".mkv" or ".avi" or ".wmv" => "Videos",
            ".mp3" or ".wav" or ".flac" or ".m4a" => "Audio",
            ".pdf" or ".doc" or ".docx" or ".txt" or ".rtf" or ".md" => "Documents",
            ".xls" or ".xlsx" or ".csv" => "Spreadsheets",
            ".ppt" or ".pptx" => "Presentations",
            ".zip" or ".7z" or ".rar" or ".tar" or ".gz" => "Archives",
            ".exe" or ".msi" => "Installers",
            ".cs" or ".fs" or ".vb" or ".js" or ".ts" or ".py" or ".go" or ".java" or ".cpp" or ".h" => "Code",
            "" => "NoExtension",
            _ => "Other",
        };
    }

    private static string GetNonCollidingPath(string destinationPath)
    {
        if (!File.Exists(destinationPath))
            return destinationPath;

        var dir = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException("Invalid destination path.");
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
        var ext = Path.GetExtension(destinationPath);

        for (int i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{fileNameWithoutExt} ({i}){ext}");
            if (!File.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Could not find a non-colliding destination filename for {destinationPath}");
    }

    public sealed record FileMove(string Source, string Destination);
    public sealed record OrganizationPlan(string DirectoryPath, string Strategy, FileMove[] Moves);
    public sealed record AppliedMove(string Source, string Destination);
}

