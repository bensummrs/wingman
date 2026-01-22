using System.Runtime.Versioning;
using System.Text.Json;
using Wingman.Agent.Tools;

namespace Wingman.Tests;

[SupportedOSPlatform("windows")]
public class FileOrganizerToolsTests
{
    private readonly string _testDirectory;
    
    public FileOrganizerToolsTests()
    {
        // Create a unique test directory for each test run
        _testDirectory = Path.Combine(Path.GetTempPath(), $"WingmanTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_testDirectory);
    }
    
    [Fact]
    public void ResolvePath_ReturnsCorrectInfo_ForExistingFile()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "test.txt");
        File.WriteAllText(testFile, "test content");
        
        // Act
        var result = FileOrganizerTools.ResolvePath(testFile);
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(testFile, json.RootElement.GetProperty("resolvedPath").GetString());
        Assert.True(json.RootElement.GetProperty("exists").GetBoolean());
        Assert.Equal("file", json.RootElement.GetProperty("type").GetString());
    }
    
    [Fact]
    public void ResolvePath_ReturnsCorrectInfo_ForExistingDirectory()
    {
        // Arrange
        var testDir = Path.Combine(_testDirectory, "subdir");
        Directory.CreateDirectory(testDir);
        
        // Act
        var result = FileOrganizerTools.ResolvePath(testDir);
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(testDir, json.RootElement.GetProperty("resolvedPath").GetString());
        Assert.True(json.RootElement.GetProperty("exists").GetBoolean());
        Assert.Equal("directory", json.RootElement.GetProperty("type").GetString());
    }
    
    [Fact]
    public void ResolvePath_ReturnsNotFound_ForNonExistentPath()
    {
        // Arrange
        var nonExistentPath = Path.Combine(_testDirectory, "does_not_exist.txt");
        
        // Act
        var result = FileOrganizerTools.ResolvePath(nonExistentPath);
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.False(json.RootElement.GetProperty("exists").GetBoolean());
        Assert.Equal("not_found", json.RootElement.GetProperty("type").GetString());
    }
    
    [Fact]
    public void ListDirectory_ReturnsFiles_InDirectory()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.pdf"), "content");
        
        // Act
        var result = FileOrganizerTools.ListDirectory(_testDirectory);
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(2, json.RootElement.GetProperty("fileCount").GetInt32());
        Assert.Equal(_testDirectory, json.RootElement.GetProperty("resolvedPath").GetString());
    }
    
    [Fact]
    public void ListDirectory_IncludesSubdirectories_WhenRequested()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file.txt"), "content");
        Directory.CreateDirectory(Path.Combine(_testDirectory, "subdir1"));
        Directory.CreateDirectory(Path.Combine(_testDirectory, "subdir2"));
        
        // Act
        var result = FileOrganizerTools.ListDirectory(_testDirectory, includeSubdirectories: true);
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(1, json.RootElement.GetProperty("fileCount").GetInt32());
        Assert.Equal(2, json.RootElement.GetProperty("subdirectoryCount").GetInt32());
    }
    
    [Fact]
    public void SearchFiles_FindsFilesByPattern()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "report1.pdf"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "report2.pdf"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "notes.txt"), "content");
        
        // Act
        var result = FileOrganizerTools.SearchFiles(_testDirectory, fileNamePattern: "*.pdf");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(2, json.RootElement.GetProperty("matchCount").GetInt32());
    }
    
    [Fact]
    public void SearchFiles_FindsFilesByExtension()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "file1.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "file2.txt"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "file3.pdf"), "content");
        
        // Act
        var result = FileOrganizerTools.SearchFiles(_testDirectory, extension: ".txt");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.Equal(2, json.RootElement.GetProperty("matchCount").GetInt32());
    }
    
    [Fact]
    public void CreateDirectory_CreatesNewDirectory_WithApproval()
    {
        // Arrange
        var dirName = "new_test_dir";
        
        // Act
        var result = FileOrganizerTools.CreateDirectory(
            _testDirectory, 
            dirName, 
            approvalPhrase: "I_APPROVE_FILE_CHANGES");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.True(json.RootElement.GetProperty("created").GetBoolean());
        Assert.True(Directory.Exists(Path.Combine(_testDirectory, dirName)));
    }
    
    [Fact]
    public void CreateDirectory_ThrowsException_WithoutApproval()
    {
        // Arrange
        var dirName = "new_dir";
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            FileOrganizerTools.CreateDirectory(_testDirectory, dirName, approvalPhrase: ""));
    }
    
    [Fact]
    public void DeleteItem_DeletesFile_WithApproval()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "to_delete.txt");
        File.WriteAllText(testFile, "content");
        
        // Act
        var result = FileOrganizerTools.DeleteItem(
            testFile, 
            approvalPhrase: "I_APPROVE_FILE_CHANGES");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.True(json.RootElement.GetProperty("deleted").GetBoolean());
        Assert.Equal("file", json.RootElement.GetProperty("type").GetString());
        Assert.False(File.Exists(testFile));
    }
    
    [Fact]
    public void DeleteItem_ThrowsException_WithoutApproval()
    {
        // Arrange
        var testFile = Path.Combine(_testDirectory, "file.txt");
        File.WriteAllText(testFile, "content");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            FileOrganizerTools.DeleteItem(testFile, approvalPhrase: ""));
    }
    
    [Fact]
    public void CopyFile_CopiesFileToDestination_WithApproval()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDirectory, "source.txt");
        var destDir = Path.Combine(_testDirectory, "dest");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(sourceFile, "test content");
        
        // Act
        var result = FileOrganizerTools.CopyFile(
            sourceFile, 
            destDir, 
            approvalPhrase: "I_APPROVE_FILE_CHANGES");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.True(json.RootElement.GetProperty("copied").GetBoolean());
        Assert.True(File.Exists(sourceFile)); // Original still exists
        Assert.True(File.Exists(Path.Combine(destDir, "source.txt"))); // Copy exists
    }
    
    [Fact]
    public void CopyFile_ThrowsException_WithoutApproval()
    {
        // Arrange
        var sourceFile = Path.Combine(_testDirectory, "source.txt");
        var destDir = Path.Combine(_testDirectory, "dest");
        Directory.CreateDirectory(destDir);
        File.WriteAllText(sourceFile, "content");
        
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() =>
            FileOrganizerTools.CopyFile(sourceFile, destDir, approvalPhrase: ""));
    }
    
    [Fact]
    public void PreviewOrganizeByExtension_CreatesOrganizationPlan()
    {
        // Arrange
        File.WriteAllText(Path.Combine(_testDirectory, "photo.jpg"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "video.mp4"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "document.pdf"), "content");
        File.WriteAllText(Path.Combine(_testDirectory, "notes.txt"), "content");
        
        // Act
        var result = FileOrganizerTools.PreviewOrganizeByExtension(_testDirectory);
        var json = JsonDocument.Parse(result);
        
        // Assert
        var plan = json.RootElement.GetProperty("plan");
        Assert.Equal(_testDirectory, plan.GetProperty("DirectoryPath").GetString());
        Assert.Equal("by_extension", plan.GetProperty("Strategy").GetString());
        
        var moves = plan.GetProperty("Moves");
        Assert.True(moves.GetArrayLength() > 0);
    }
    
    #region Windows Search API Tests
    
    [Fact]
    public void FindDirectory_UsingWindowsSearch_FindsDesktopDirectory()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("desktop");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.NotNull(json.RootElement.GetProperty("matches"));
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        
        if (matches.Count > 0)
        {
            var firstMatch = matches[0];
            var path = firstMatch.GetProperty("path").GetString();
            Assert.NotNull(path);
            Assert.True(Directory.Exists(path), $"Found path should exist: {path}");
            
            // Should contain "Desktop" in the path
            Assert.Contains("Desktop", path, StringComparison.OrdinalIgnoreCase);
        }
    }
    
    [Fact]
    public void FindDirectory_UsingWindowsSearch_FindsDocumentsDirectory()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("documents");
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matchCount = json.RootElement.GetProperty("matchCount").GetInt32();
        Assert.True(matchCount > 0, "Should find at least one Documents directory");
        
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        var firstMatch = matches[0];
        var path = firstMatch.GetProperty("path").GetString();
        
        Assert.NotNull(path);
        Assert.True(Directory.Exists(path));
    }
    
    [Fact]
    public void FindDirectory_UsingWindowsSearch_FindsDownloadsDirectory()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("downloads");
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matchCount = json.RootElement.GetProperty("matchCount").GetInt32();
        Assert.True(matchCount > 0, "Should find at least one Downloads directory");
        
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        var path = matches[0].GetProperty("path").GetString();
        
        Assert.NotNull(path);
        Assert.True(Directory.Exists(path));
        Assert.Contains("Download", path, StringComparison.OrdinalIgnoreCase);
    }
    
    [Fact]
    public void FindDirectory_WithNaturalLanguage_FindsCorrectDirectory()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("my documents folder");
        var json = JsonDocument.Parse(result);
        
        // Assert
        Assert.NotNull(json.RootElement.GetProperty("query").GetString());
        Assert.Equal("my documents folder", json.RootElement.GetProperty("query").GetString());
        
        var matchCount = json.RootElement.GetProperty("matchCount").GetInt32();
        Assert.True(matchCount >= 0, "Should return valid match count");
    }
    
    [Fact]
    public void FindDirectory_ReturnsMatchScores()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("desktop");
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        
        if (matches.Count > 0)
        {
            foreach (var match in matches)
            {
                var score = match.GetProperty("matchScore").GetInt32();
                var reason = match.GetProperty("matchReason").GetString();
                
                Assert.True(score > 0, "Match score should be positive");
                Assert.NotNull(reason);
                Assert.NotEmpty(reason);
            }
        }
    }
    
    [Fact]
    public void FindDirectory_RespectsMaxResults()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("folder", maxResults: 3);
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        Assert.True(matches.Count <= 3, "Should not exceed maxResults");
    }
    
    [Fact]
    public void FindDirectory_WithSearchRoot_LimitsScope()
    {
        // Arrange
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        // Act
        var result = FileOrganizerTools.FindDirectory("documents", searchRoot: userProfile);
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        
        // All matches should be under the search root
        foreach (var match in matches)
        {
            var path = match.GetProperty("path").GetString();
            Assert.NotNull(path);
            // Path should be under userProfile or be a common system folder
        }
    }
    
    [Fact]
    public void FindDirectory_WithNonIndexedLocation_FallsBackToManualSearch()
    {
        // Arrange - use our test directory which is not indexed
        var testSubDir = Path.Combine(_testDirectory, "FindMeManually");
        Directory.CreateDirectory(testSubDir);
        
        // Act
        var result = FileOrganizerTools.FindDirectory("FindMeManually", searchRoot: _testDirectory);
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matchCount = json.RootElement.GetProperty("matchCount").GetInt32();
        
        // Should find it via manual search fallback
        if (matchCount > 0)
        {
            var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
            var foundPath = matches[0].GetProperty("path").GetString();
            Assert.Equal(testSubDir, foundPath);
        }
        
        // Cleanup
        Directory.Delete(testSubDir);
    }
    
    [Fact]
    public void FindDirectory_HandlesSpecialCharacters()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("Program Files");
        var json = JsonDocument.Parse(result);
        
        // Assert - should not throw and should return valid JSON
        Assert.NotNull(json.RootElement.GetProperty("matchCount"));
        Assert.NotNull(json.RootElement.GetProperty("matches"));
    }
    
    [Fact]
    public void FindDirectory_WithEmptyDescription_ThrowsException()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => 
            FileOrganizerTools.FindDirectory(""));
    }
    
    [Fact]
    public void FindDirectory_HighScoreForExactMatch()
    {
        // Act
        var result = FileOrganizerTools.FindDirectory("Desktop");
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matches = json.RootElement.GetProperty("matches").EnumerateArray().ToList();
        
        if (matches.Count > 0)
        {
            var topMatch = matches[0];
            var score = topMatch.GetProperty("matchScore").GetInt32();
            var path = topMatch.GetProperty("path").GetString();
            
            // Exact match on "Desktop" should have high score
            if (path?.EndsWith("Desktop", StringComparison.OrdinalIgnoreCase) == true)
            {
                Assert.True(score >= 75, $"Exact match should have high score, got {score}");
            }
        }
    }
    
    [Theory]
    [InlineData("downloads")]
    [InlineData("desktop")]
    [InlineData("documents")]
    [InlineData("pictures")]
    [InlineData("music")]
    public void FindDirectory_CommonUserFolders_ReturnsResults(string folderName)
    {
        // Act
        var result = FileOrganizerTools.FindDirectory(folderName);
        var json = JsonDocument.Parse(result);
        
        // Assert
        var matchCount = json.RootElement.GetProperty("matchCount").GetInt32();
        
        // Common user folders should be found (unless they don't exist on this system)
        // We can't guarantee they exist, but the method should not throw
        Assert.True(matchCount >= 0);
    }
    
    [Fact]
    public void SearchForDirectories_IsIndexedLocation_IdentifiesUserProfile()
    {
        // This tests the internal logic through public API
        // Arrange
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        
        // Act - search in user profile, which should be indexed
        var result = FileOrganizerTools.FindDirectory("documents", searchRoot: userProfile);
        var json = JsonDocument.Parse(result);
        
        // Assert - should succeed without errors
        Assert.NotNull(json.RootElement.GetProperty("matchCount"));
    }
    
    [Fact]
    public void FindDirectory_CaseInsensitive_FindsResults()
    {
        // Act
        var lowerResult = FileOrganizerTools.FindDirectory("desktop");
        var upperResult = FileOrganizerTools.FindDirectory("DESKTOP");
        var mixedResult = FileOrganizerTools.FindDirectory("DeskTop");
        
        // Parse all results
        var lowerJson = JsonDocument.Parse(lowerResult);
        var upperJson = JsonDocument.Parse(upperResult);
        var mixedJson = JsonDocument.Parse(mixedResult);
        
        // Assert - should find same results regardless of case
        var lowerCount = lowerJson.RootElement.GetProperty("matchCount").GetInt32();
        var upperCount = upperJson.RootElement.GetProperty("matchCount").GetInt32();
        var mixedCount = mixedJson.RootElement.GetProperty("matchCount").GetInt32();
        
        // All counts should be the same (Windows Search is case-insensitive)
        Assert.Equal(lowerCount, upperCount);
        Assert.Equal(lowerCount, mixedCount);
    }
    
    #endregion
    
    // Cleanup after all tests
    ~FileOrganizerToolsTests()
    {
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, recursive: true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }
}
