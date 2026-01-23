using System.ComponentModel;
using System.Data.OleDb;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent.Tools;

[SupportedOSPlatform("windows")]
public static class FileOrganizerTools
{
    private const string ApprovalPhrase = "I_APPROVE_FILE_CHANGES";

    [Description("Quickly resolves a file or directory path. Supports absolute paths, relative paths (from current working directory), environment variables, and pasted paths from Windows Explorer. Use this when the user provides a direct path or file name.")]
    public static string ResolvePath(
        [Description("The path to resolve. Can be absolute (C:\\...), relative (src\\file.txt), just a filename (document.pdf), or a Windows Explorer path.")] string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("path is required.", nameof(path));

        var resolvedPath = path.ResolvePathWithFileName();

        bool exists = File.Exists(resolvedPath) || Directory.Exists(resolvedPath);
        string type = File.Exists(resolvedPath) ? "file" : Directory.Exists(resolvedPath) ? "directory" : "not_found";

        return JsonSerializer.Serialize(new
        {
            originalPath = path,
            resolvedPath,
            exists,
            type,
            isAbsolute = Path.IsPathFullyQualified(resolvedPath),
            workingDirectory = PathExtensions.CurrentWorkingDirectory ?? "(not set)",
        });
    }

    [Description("Searches for directories on the user's machine based on a natural language description or name. Returns matching directory paths. Use this when the user describes a directory rather than providing a direct path.")]
    [SupportedOSPlatform("windows")]
    public static string FindDirectory(
        [Description("Natural language description or name of the directory (e.g., 'downloads folder', 'my documents', 'desktop', or 'projects folder in my user directory').")] string directoryDescription,
        [Description("Optional: Starting search location. If not provided, searches common user locations and all drives.")] string? searchRoot = null,
        [Description("Maximum number of results to return.")] int maxResults = 10)
    {
        if (string.IsNullOrWhiteSpace(directoryDescription))
            throw new ArgumentException("directoryDescription is required.", nameof(directoryDescription));

        var matches = SearchForDirectories(directoryDescription, searchRoot, maxResults);

        return JsonSerializer.Serialize(new
        {
            query = directoryDescription,
            matchCount = matches.Count,
            matches = matches.Select(m => new
            {
                path = m.Path,
                matchScore = m.Score,
                matchReason = m.Reason,
            }),
        });
    }

    [Description("Lists the files and subdirectories in a directory with basic metadata. Can resolve directory from description or exact path.")]
    public static string ListDirectory(
        [Description("Description or exact path of the directory to list (e.g., 'downloads folder', 'C:\\Users\\username\\Documents').")] string directoryPathOrDescription,
        [Description("If true, includes subdirectories in the listing.")] bool includeSubdirectories = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPathOrDescription))
            throw new ArgumentException("directoryPathOrDescription is required.", nameof(directoryPathOrDescription));

        var resolvedPath = directoryPathOrDescription.ResolveDirectoryPath();

        if (!Directory.Exists(resolvedPath))
            throw new DirectoryNotFoundException($"Directory not found: {resolvedPath} (resolved from: {directoryPathOrDescription})");

        var items = new List<FileSystemItem>();

        foreach (var filePath in Directory.EnumerateFiles(resolvedPath))
        {
            var info = new FileInfo(filePath);
            items.Add(new FileSystemItem("file", info.Name, info.FullName, info.Extension, info.Length, info.LastWriteTimeUtc));
        }

        if (includeSubdirectories)
        {
            foreach (var dirPath in Directory.EnumerateDirectories(resolvedPath))
            {
                var info = new DirectoryInfo(dirPath);
                items.Add(new FileSystemItem("directory", info.Name, info.FullName, null, null, info.LastWriteTimeUtc));
            }
        }

        var sorted = items
            .OrderBy(item => item.Type)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileCount = items.Count(i => i.Type == "file");
        var subdirectoryCount = items.Count(i => i.Type == "directory");

        return JsonSerializer.Serialize(new
        {
            resolvedPath,
            originalInput = directoryPathOrDescription,
            fileCount,
            subdirectoryCount,
            items = sorted,
        });
    }

    [Description("Moves a file or directory from one location to another. Supports natural language descriptions for source and destination.")]
    public static string MoveItem(
        [Description("Source path or description (e.g., 'report.pdf in downloads', 'C:\\temp\\file.txt').")] string sourcePathOrDescription,
        [Description("Destination path or description (e.g., 'documents folder', 'C:\\Users\\username\\backup').")] string destinationPathOrDescription,
        [Description("New name for the item (optional, keeps original name if not specified).")] string? newName = null,
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to perform the move.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to modify files. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        var sourcePath = sourcePathOrDescription.ResolvePathWithFileName();
        var destinationPath = sourcePathOrDescription.ResolveDirectoryPath();

        if (!File.Exists(sourcePath) && !Directory.Exists(sourcePath))
            throw new FileNotFoundException($"Source not found: {sourcePath} (resolved from: {sourcePathOrDescription})");

        if (!Directory.Exists(destinationPath))
            throw new DirectoryNotFoundException($"Destination directory not found: {destinationPath} (resolved from: {destinationPathOrDescription})");

        var itemName = newName ?? Path.GetFileName(sourcePath);
        var finalDestination = Path.Combine(destinationPath, itemName);
        finalDestination = GetNonCollidingPath(finalDestination);

        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, finalDestination);
        }
        else
        {
            Directory.Move(sourcePath, finalDestination);
        }

        return JsonSerializer.Serialize(new
        {
            source = sourcePath,
            destination = finalDestination,
            itemName,
            moved = true,
        });
    }

    [Description("Searches for files in a directory based on name pattern, extension, or size criteria.")]
    public static string SearchFiles(
        [Description("Directory path or description to search in.")] string directoryPathOrDescription,
        [Description("File name pattern to match (e.g., '*.pdf', 'report*', 'document.txt'). Use * as wildcard.")] string? fileNamePattern = null,
        [Description("File extension to filter by (e.g., '.pdf', '.docx'). Do not use with fileNamePattern.")] string? extension = null,
        [Description("Minimum file size in bytes.")] long? minSizeBytes = null,
        [Description("Maximum file size in bytes.")] long? maxSizeBytes = null,
        [Description("If true, searches recursively in subdirectories.")] bool recursive = false,
        [Description("Maximum number of results to return.")] int maxResults = 100)
    {
        var directoryPath = directoryPathOrDescription.ResolveDirectoryPath();

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath} (resolved from: {directoryPathOrDescription})");

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var pattern = fileNamePattern ?? "*";

        var fileInfos = Directory.EnumerateFiles(directoryPath, pattern, searchOption).Select(p => new FileInfo(p));
        var filteredFiles = fileInfos.FilterByExtensionAndSize(extension, minSizeBytes, maxSizeBytes);

        var files = filteredFiles
            .Take(maxResults)
            .Select(f => new
            {
                name = f.Name,
                fullPath = f.FullName,
                extension = f.Extension,
                sizeBytes = f.Length,
                lastWriteTimeUtc = f.LastWriteTimeUtc,
                directory = Path.GetDirectoryName(f.FullName),
            })
            .OrderBy(f => f.name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            searchDirectory = directoryPath,
            originalInput = directoryPathOrDescription,
            pattern,
            recursive,
            matchCount = files.Length,
            files,
        });
    }

    [Description("Creates a new directory. Supports natural language descriptions for parent location.")]
    public static string CreateDirectory(
        [Description("Parent directory path or description where the new directory should be created.")] string parentPathOrDescription,
        [Description("Name of the new directory to create.")] string directoryName,
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to create the directory.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to modify file system. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        var parentPath = parentPathOrDescription.ResolveDirectoryPath();

        if (!Directory.Exists(parentPath))
            throw new DirectoryNotFoundException($"Parent directory not found: {parentPath} (resolved from: {parentPathOrDescription})");

        var newDirectoryPath = Path.Combine(parentPath, directoryName);

        if (Directory.Exists(newDirectoryPath))
            throw new InvalidOperationException($"Directory already exists: {newDirectoryPath}");

        Directory.CreateDirectory(newDirectoryPath);

        return JsonSerializer.Serialize(new
        {
            created = true,
            path = newDirectoryPath,
            parentPath,
            directoryName,
        });
    }

    [Description("Deletes a file or directory. Use with caution.")]
    public static string DeleteItem(
        [Description("Path or description of file/directory to delete.")] string pathOrDescription,
        [Description("If true and target is a directory, deletes all contents recursively.")] bool recursive = false,
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to perform deletion.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to delete. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        var resolvedPath = pathOrDescription.ResolvePathWithFileName();

        if (File.Exists(resolvedPath))
        {
            File.Delete(resolvedPath);
            return JsonSerializer.Serialize(new
            {
                deleted = true,
                path = resolvedPath,
                type = "file",
            });
        }
        else if (Directory.Exists(resolvedPath))
        {
            Directory.Delete(resolvedPath, recursive);
            return JsonSerializer.Serialize(new
            {
                deleted = true,
                path = resolvedPath,
                type = "directory",
                recursive,
            });
        }
        else
        {
            throw new FileNotFoundException($"Item not found: {resolvedPath} (resolved from: {pathOrDescription})");
        }
    }

    [Description("Copies a file to another location. Supports natural language descriptions.")]
    public static string CopyFile(
        [Description("Source file path or description.")] string sourcePathOrDescription,
        [Description("Destination directory path or description.")] string destinationPathOrDescription,
        [Description("New name for the copied file (optional).")] string? newName = null,
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to perform the copy.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to modify files. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        var sourcePath = sourcePathOrDescription.ResolvePathWithFileName();
        var destinationPath = destinationPathOrDescription.ResolveDirectoryPath();

        if (!File.Exists(sourcePath))
            throw new FileNotFoundException($"Source file not found: {sourcePath} (resolved from: {sourcePathOrDescription})");

        if (!Directory.Exists(destinationPath))
            throw new DirectoryNotFoundException($"Destination directory not found: {destinationPath} (resolved from: {destinationPathOrDescription})");

        var fileName = newName ?? Path.GetFileName(sourcePath);
        var finalDestination = Path.Combine(destinationPath, fileName);
        finalDestination = GetNonCollidingPath(finalDestination);

        File.Copy(sourcePath, finalDestination);

        return JsonSerializer.Serialize(new
        {
            source = sourcePath,
            destination = finalDestination,
            copied = true,
        });
    }

    [Description("""
        Writes text content to a file. Creates the file if it doesn't exist, or overwrites if it does.
        Use this to create any text-based file: CSV, TXT, JSON, HTML, Markdown, etc.
        For CSV files, format the content with comma-separated values and newlines between rows.
        Example CSV: "Name,Age,City\nAlice,30,NYC\nBob,25,LA"
        """)]
    public static string WriteFile(
        [Description("The full path where the file should be written (e.g., 'C:/Documents/report.csv').")] string filePath,
        [Description("The text content to write to the file.")] string content,
        [Description("Must exactly equal 'I_APPROVE_FILE_CHANGES' to perform the write.")] string approvalPhrase = "")
    {
        if (!string.Equals(approvalPhrase, ApprovalPhrase, StringComparison.Ordinal))
            throw new InvalidOperationException($"Refusing to modify files. approvalPhrase must be exactly '{ApprovalPhrase}'.");

        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(content);

        var resolvedPath = filePath.ResolvePathWithFileName();

        var directory = Path.GetDirectoryName(resolvedPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        bool existed = File.Exists(resolvedPath);
        File.WriteAllText(resolvedPath, content);

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            created = !existed,
            overwritten = existed,
            bytes = new FileInfo(resolvedPath).Length,
        });
    }

    [Description("Reads the text content of a file and returns it.")]
    public static string ReadFile(
        [Description("The path to the file to read.")] string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var resolvedPath = filePath.ResolvePathWithFileName();

        if (!File.Exists(resolvedPath))
            throw new FileNotFoundException($"File not found: {resolvedPath}");

        var content = File.ReadAllText(resolvedPath);

        return JsonSerializer.Serialize(new
        {
            filePath = resolvedPath,
            content,
            bytes = new FileInfo(resolvedPath).Length,
        });
    }

    [Description("Creates a preview plan to organize files in a directory by extension. Does not modify the file system. Supports natural language directory descriptions.")]
    public static string PreviewOrganizeByExtension(
        [Description("Path or description of the directory to organize (e.g., 'downloads folder', 'C:\\Users\\username\\Documents').")] string directoryPathOrDescription,
        [Description("If true, include hidden files (Windows hidden attribute).")] bool includeHidden = false)
    {
        if (string.IsNullOrWhiteSpace(directoryPathOrDescription))
            throw new ArgumentException("directoryPathOrDescription is required.", nameof(directoryPathOrDescription));

        var directoryPath = directoryPathOrDescription.ResolveDirectoryPath();

        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath} (resolved from: {directoryPathOrDescription})");

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

        var plan = new OrganizationPlan(directoryPath, "by_extension", moves.ToArray());

        return JsonSerializer.Serialize(new
        {
            resolvedPath = directoryPath,
            originalInput = directoryPathOrDescription,
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
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("plan", out var planEl))
            return planEl.Deserialize<OrganizationPlan>();

        return null;
    }

    [SupportedOSPlatform("windows")]
    internal static List<DirectoryMatch> SearchForDirectories(string description, string? searchRoot, int maxResults)
    {
        var matches = new List<DirectoryMatch>();
        var searchTerms = ExtractSearchTerms(description);

        if (IsLikelyIndexedLocation(searchRoot))
        {
            try
            {
                var indexedMatches = SearchUsingWindowsSearch(searchTerms, searchRoot, maxResults);
                matches.AddRange(indexedMatches);
                
                if (matches.Count >= maxResults && matches.Any(m => m.Score >= 100))
                    return matches.OrderByDescending(m => m.Score).Take(maxResults).ToList();
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Windows Search unavailable, using manual search: {ex.Message}");
            }
        }

        var searchRoots = new List<string>();
        
        if (!string.IsNullOrEmpty(searchRoot) && Directory.Exists(searchRoot))
        {
            searchRoots.Add(searchRoot);
            
            var parent = Directory.GetParent(searchRoot);
            if (parent != null && parent.Exists)
            {
                searchRoots.Add(parent.FullName);
            }
        }
        
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            searchRoots.Add(userProfile);
            var downloads = Path.Combine(userProfile, "Downloads");
            if (Directory.Exists(downloads)) searchRoots.Add(downloads);
        }

        searchRoots.Add(Environment.GetFolderPath(Environment.SpecialFolder.Desktop));
        searchRoots.Add(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments));

        searchRoots.AddRange(DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Select(d => d.RootDirectory.FullName));

        var processedRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var root in searchRoots)
        {
            if (!Directory.Exists(root) || !processedRoots.Add(root)) 
                continue;

            try
            {
                SearchDirectoryRecursive(root, searchTerms, matches, maxResults, maxDepth: 5, currentDepth: 0);
            }
            catch (UnauthorizedAccessException)
            {
                // Skip root directories we don't have permission to access
                continue;
            }
            catch (IOException)
            {
                // Skip root directories with IO errors
                continue;
            }
            
            if (matches.Count >= maxResults && matches.Any(m => m.Score >= 100)) 
                break;
        }

        return matches.OrderByDescending(m => m.Score).Take(maxResults).ToList();
    }

    private static bool IsLikelyIndexedLocation(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return true;
        }

        path = path.ToLowerInvariant();
        
        var indexedFolders = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile).ToLowerInvariant(),
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop).ToLowerInvariant(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments).ToLowerInvariant(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures).ToLowerInvariant(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyMusic).ToLowerInvariant(),
            Environment.GetFolderPath(Environment.SpecialFolder.MyVideos).ToLowerInvariant(),
        };

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            var downloads = Path.Combine(userProfile, "Downloads").ToLowerInvariant();
            indexedFolders = indexedFolders.Append(downloads).ToArray();
        }

        return indexedFolders.Any(indexed => path.StartsWith(indexed, StringComparison.OrdinalIgnoreCase));
    }

    [SupportedOSPlatform("windows")]
    private static List<DirectoryMatch> SearchUsingWindowsSearch(List<string> searchTerms, string? searchRoot, int maxResults)
    {
        var matches = new List<DirectoryMatch>();
        
        if (searchTerms.Count == 0)
            return matches;

        var connectionString = "Provider=Search.CollatorDSO;Extended Properties='Application=Windows'";
        
        using var connection = new OleDbConnection(connectionString);
        connection.Open();

        var searchConditions = new List<string>();
        foreach (var term in searchTerms)
        {
            var escapedTerm = term.Replace("'", "''");
            searchConditions.Add($"CONTAINS(System.ItemName, '\"{escapedTerm}\"')");
        }
        
        var whereClause = string.Join(" OR ", searchConditions);
        
        var scopeClause = "";
        if (!string.IsNullOrEmpty(searchRoot) && Directory.Exists(searchRoot))
        {
            var escapedPath = searchRoot.Replace("'", "''");
            scopeClause = $"AND System.ItemPathDisplay LIKE '{escapedPath}%'";
        }

        var query = $@"
            SELECT TOP {maxResults} 
                System.ItemPathDisplay, 
                System.ItemName,
                System.DateModified
            FROM SystemIndex 
            WHERE System.Kind = 'folder' 
            AND ({whereClause})
            {scopeClause}
            ORDER BY System.DateModified DESC";

        using var command = new OleDbCommand(query, connection);
        command.CommandTimeout = 5; // 5 second timeout
        
        using var reader = command.ExecuteReader();
        
        while (reader.Read() && matches.Count < maxResults)
        {
            var path = reader["System.ItemPathDisplay"]?.ToString();
            var name = reader["System.ItemName"]?.ToString();
            
            if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
            {
                var score = CalculateMatchScore(name ?? Path.GetFileName(path) ?? "", searchTerms);
                var reason = "Found via Windows Search Index";
                
                matches.Add(new DirectoryMatch(path, score, reason));
            }
        }

        return matches;
    }

    private static int CalculateMatchScore(string directoryName, List<string> searchTerms)
    {
        var score = 0;
        var lowerName = directoryName.ToLowerInvariant();

        foreach (var term in searchTerms)
        {
            if (lowerName.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
            }
            else if (lowerName.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 75;
            }
            else if (lowerName.Contains(term))
            {
                score += 50;
            }
        }

        return score;
    }

    private static void SearchDirectoryRecursive(
        string currentPath,
        List<string> searchTerms,
        List<DirectoryMatch> matches,
        int maxResults,
        int maxDepth,
        int currentDepth)
    {
        if (matches.Count >= maxResults || currentDepth > maxDepth)
            return;

        var dirInfo = new DirectoryInfo(currentPath);
        var dirName = dirInfo.Name.ToLowerInvariant();

        var score = 0;
        var matchReasons = new List<string>();

        foreach (var term in searchTerms)
        {
            if (dirName.Equals(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 100;
                matchReasons.Add($"Exact match: '{term}'");
            }
            else if (dirName.Contains(term))
            {
                score += 50;
                matchReasons.Add($"Contains: '{term}'");
            }
            else if (dirName.StartsWith(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 75;
                matchReasons.Add($"Starts with: '{term}'");
            }
        }

        score -= currentDepth * 5;

        if (score > 0)
            matches.Add(new DirectoryMatch(currentPath, score, string.Join(", ", matchReasons)));

        if (currentDepth < maxDepth)
        {
            try
            {
                foreach (var subDir in Directory.EnumerateDirectories(currentPath))
                {
                    try
                    {
                        SearchDirectoryRecursive(subDir, searchTerms, matches, maxResults, maxDepth, currentDepth + 1);
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // Skip directories we don't have permission to access
                        continue;
                    }
                    catch (DirectoryNotFoundException)
                    {
                        // Skip directories that no longer exist (e.g., junctions)
                        continue;
                    }
                    catch (IOException)
                    {
                        // Skip directories with IO errors
                        continue;
                    }
                }
            }
            catch (UnauthorizedAccessException)
            {
                // Cannot enumerate subdirectories
            }
            catch (DirectoryNotFoundException)
            {
                // Directory no longer exists
            }
        }
    }

    private static List<string> ExtractSearchTerms(string description)
    {
        var stopWords = new HashSet<string> { "the", "my", "folder", "directory", "in", "on", "at", "a", "an" };

        return Regex.Split(description.ToLowerInvariant(), @"\s+")
            .Where(term => !string.IsNullOrWhiteSpace(term) && !stopWords.Contains(term))
            .ToList();
    }

    internal sealed record DirectoryMatch(string Path, int Score, string Reason);
    private sealed record FileSystemItem(string Type, string Name, string FullPath, string? Extension, long? SizeBytes, DateTime LastWriteTimeUtc);

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
        if (!File.Exists(destinationPath) && !Directory.Exists(destinationPath))
            return destinationPath;

        var dir = Path.GetDirectoryName(destinationPath) ?? throw new InvalidOperationException("Invalid destination path.");
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(destinationPath);
        var ext = Path.GetExtension(destinationPath);

        for (int i = 1; i < 10_000; i++)
        {
            var candidate = Path.Combine(dir, $"{fileNameWithoutExt} ({i}){ext}");
            if (!File.Exists(candidate) && !Directory.Exists(candidate))
                return candidate;
        }

        throw new IOException($"Could not find a non-colliding destination path for {destinationPath}");
    }

    public sealed record FileMove(string Source, string Destination);
    public sealed record OrganizationPlan(string DirectoryPath, string Strategy, FileMove[] Moves);
    public sealed record AppliedMove(string Source, string Destination);
}

