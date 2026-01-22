using System.Text.RegularExpressions;

namespace Wingman.Agent.Tools.Extensions;

internal static class PathExtensions
{
    internal static string ResolveDirectoryPath(this string pathOrDescription)
    {
        if (Directory.Exists(pathOrDescription))
            return Path.GetFullPath(pathOrDescription);

        var knownFolder = TryResolveKnownFolder(pathOrDescription);
        if (knownFolder != null)
            return knownFolder;

        var expandedPath = Environment.ExpandEnvironmentVariables(pathOrDescription);
        if (Directory.Exists(expandedPath))
            return Path.GetFullPath(expandedPath);

        var matches = FileOrganizerTools.SearchForDirectories(pathOrDescription, searchRoot: null, maxResults: 1);
        if (matches.Count > 0)
            return matches[0].Path;

        return pathOrDescription;
    }

    internal static string ResolvePathWithFileName(this string pathOrDescription)
    {
        if (File.Exists(pathOrDescription) || Directory.Exists(pathOrDescription))
            return Path.GetFullPath(pathOrDescription);

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

        var expandedPath = Environment.ExpandEnvironmentVariables(pathOrDescription);
        if (File.Exists(expandedPath) || Directory.Exists(expandedPath))
            return Path.GetFullPath(expandedPath);

        return pathOrDescription;
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
