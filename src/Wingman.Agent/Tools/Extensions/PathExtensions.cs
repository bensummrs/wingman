using System.Runtime.Versioning;
using System.Text.RegularExpressions;

namespace Wingman.Agent.Tools.Extensions;

internal static class PathExtensions
{
    internal static string? CurrentWorkingDirectory { get; set; }

    [SupportedOSPlatform("windows")]
    internal static string ResolveDirectoryPath(this string pathOrDescription)
    {
        // 1. Check if it's an absolute path that exists
        if (Directory.Exists(pathOrDescription))
            return Path.GetFullPath(pathOrDescription);

        // 2. Try relative to current working directory first (if set)
        if (!string.IsNullOrEmpty(CurrentWorkingDirectory))
        {
            var relativePath = Path.Combine(CurrentWorkingDirectory, pathOrDescription);
            if (Directory.Exists(relativePath))
                return Path.GetFullPath(relativePath);
        }

        // 3. Try known folder shortcuts
        var knownFolder = TryResolveKnownFolder(pathOrDescription);
        if (knownFolder != null)
            return knownFolder;

        // 4. Try environment variables
        var expandedPath = Environment.ExpandEnvironmentVariables(pathOrDescription);
        if (Directory.Exists(expandedPath))
            return Path.GetFullPath(expandedPath);

        // 5. Fall back to directory search as last resort
        var searchRoot = !string.IsNullOrEmpty(CurrentWorkingDirectory) ? CurrentWorkingDirectory : null;
        var matches = FileOrganizerTools.SearchForDirectories(pathOrDescription, searchRoot, maxResults: 1);
        if (matches.Count > 0)
            return matches[0].Path;

        return pathOrDescription;
    }

    [SupportedOSPlatform("windows")]
    internal static string ResolvePathWithFileName(this string pathOrDescription)
    {
        // 1. Check if it's a direct path that exists
        if (File.Exists(pathOrDescription) || Directory.Exists(pathOrDescription))
            return Path.GetFullPath(pathOrDescription);

        // 2. Try relative to current working directory first
        if (!string.IsNullOrEmpty(CurrentWorkingDirectory))
        {
            var relativePath = Path.Combine(CurrentWorkingDirectory, pathOrDescription);
            if (File.Exists(relativePath) || Directory.Exists(relativePath))
                return Path.GetFullPath(relativePath);
        }

        // 3. Handle "filename in directory" pattern
        var match = Regex.Match(pathOrDescription, @"^(.+?)\s+in\s+(.+)$", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var fileName = match.Groups[1].Value.Trim();
            var directoryDescription = match.Groups[2].Value.Trim();
            var directory = ResolveDirectoryPath(directoryDescription);

            var filePath = Path.Combine(directory, fileName);
            if (File.Exists(filePath))
                return filePath;

            var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).Contains(fileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length > 0)
                return files[0];
        }

        // 4. Try environment variables
        var expandedPath = Environment.ExpandEnvironmentVariables(pathOrDescription);
        if (File.Exists(expandedPath) || Directory.Exists(expandedPath))
            return Path.GetFullPath(expandedPath);

        // 5. Try to find file in current working directory by name
        if (!string.IsNullOrEmpty(CurrentWorkingDirectory) && !pathOrDescription.Contains(Path.DirectorySeparatorChar) && !pathOrDescription.Contains(Path.AltDirectorySeparatorChar))
        {
            var foundFile = TryFindFileInDirectory(CurrentWorkingDirectory, pathOrDescription);
            if (foundFile != null)
                return foundFile;
        }

        return pathOrDescription;
    }

    private static string? TryFindFileInDirectory(string directory, string fileName)
    {
        try
        {
            // Try exact match first
            var exactMatch = Path.Combine(directory, fileName);
            if (File.Exists(exactMatch))
                return exactMatch;

            // Try pattern matching with case-insensitive search
            var files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).Equals(fileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (files.Length > 0)
                return files[0];

            // Try partial match as last resort
            files = Directory.EnumerateFiles(directory, "*", SearchOption.TopDirectoryOnly)
                .Where(f => Path.GetFileName(f).Contains(fileName, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return files.Length > 0 ? files[0] : null;
        }
        catch
        {
            return null;
        }
    }

    internal static string? TryResolveKnownFolder(this string description)
    {
        description = description.ToLowerInvariant().Trim();

        var folderMappings = new Dictionary<string, Environment.SpecialFolder>
        {
            ["desktop"] = Environment.SpecialFolder.Desktop,
            ["documents"] = Environment.SpecialFolder.MyDocuments,
            ["my documents"] = Environment.SpecialFolder.MyDocuments,
            ["downloads"] = Environment.SpecialFolder.UserProfile,
            ["pictures"] = Environment.SpecialFolder.MyPictures,
            ["my pictures"] = Environment.SpecialFolder.MyPictures,
            ["music"] = Environment.SpecialFolder.MyMusic,
            ["my music"] = Environment.SpecialFolder.MyMusic,
            ["videos"] = Environment.SpecialFolder.MyVideos,
            ["my videos"] = Environment.SpecialFolder.MyVideos,
            ["appdata"] = Environment.SpecialFolder.ApplicationData,
            ["local appdata"] = Environment.SpecialFolder.LocalApplicationData,
            ["program files"] = Environment.SpecialFolder.ProgramFiles,
            ["temp"] = Environment.SpecialFolder.UserProfile, // We'll append Temp
            ["user"] = Environment.SpecialFolder.UserProfile,
            ["home"] = Environment.SpecialFolder.UserProfile,
        };

        if (description.Contains("download"))
        {
            var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var downloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(downloads))
                return downloads;
        }

        if (description.Contains("temp"))
        {
            var tempPath = Path.GetTempPath();
            if (Directory.Exists(tempPath))
                return tempPath;
        }

        foreach (var (key, folder) in folderMappings)
        {
            if (description.Contains(key))
            {
                var path = Environment.GetFolderPath(folder);
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    return path;
            }
        }

        return null;
    }
}
