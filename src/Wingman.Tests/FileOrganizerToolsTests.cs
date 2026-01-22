using System.Text.Json;
using Wingman.Agent.Tools;

namespace Wingman.Tests;

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
