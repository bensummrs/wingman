using System.Text.Json;
using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public static class WingmanCliConfigLoader
{
    private static readonly string ConfigFileName = "wingman.config.json";
    
    public static ConfigLoadResult TryLoad()
    {
        // Try 1: Load from config file in current directory
        var currentDirConfig = Path.Combine(Directory.GetCurrentDirectory(), ConfigFileName);
        if (File.Exists(currentDirConfig))
        {
            var result = TryLoadFromFile(currentDirConfig);
            if (result.Success) return result;
        }

        // Try 2: Load from config file in user's home directory
        var homeDirConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ConfigFileName);
        if (File.Exists(homeDirConfig))
        {
            var result = TryLoadFromFile(homeDirConfig);
            if (result.Success) return result;
        }

        // Try 3: Load from environment variables
        var envResult = TryLoadFromEnvironment();
        if (envResult.Success) return envResult;

        // Nothing worked - give helpful error
        return ConfigLoadResult.FailureResult(
            $"Missing API key. Create a '{ConfigFileName}' file with your API key:\n\n" +
            "{\n" +
            "  \"ApiKey\": \"your-api-key-here\",\n" +
            "  \"Model\": \"claude-3-5-sonnet-20241022\"\n" +
            "}\n\n" +
            $"Place it in:\n" +
            $"  - Current directory: {currentDirConfig}\n" +
            $"  - Or your home directory: {homeDirConfig}\n" +
            $"  - Or set ANTHROPIC_API_KEY environment variable");
    }

    private static ConfigLoadResult TryLoadFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var config = JsonSerializer.Deserialize<WingmanConfig>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (config == null || string.IsNullOrWhiteSpace(config.ApiKey))
            {
                return ConfigLoadResult.FailureResult($"Invalid config file: {filePath}");
            }

            return ConfigLoadResult.SuccessResult(config);
        }
        catch (Exception ex)
        {
            return ConfigLoadResult.FailureResult($"Error reading config file {filePath}: {ex.Message}");
        }
    }

    private static ConfigLoadResult TryLoadFromEnvironment()
    {
        var apiKey = GetEnv("ANTHROPIC_API_KEY", "OPENAI_API_KEY");
        var model = GetEnv("WINGMAN_MODEL") ?? "claude-3-5-sonnet-20241022";

        var config = new WingmanConfig
        {
            ApiKey = apiKey ?? string.Empty,
            Model = model,
        };

        if (string.IsNullOrWhiteSpace(config.ApiKey))
        {
            return ConfigLoadResult.FailureResult("No API key in environment variables.");
        }

        return ConfigLoadResult.SuccessResult(config);
    }

    private static string? GetEnv(params string[] names)
    {
        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name);
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }
}

public sealed record ConfigLoadResult(bool Success, WingmanConfig? Config, string? ErrorMessage)
{
    public static ConfigLoadResult SuccessResult(WingmanConfig config) => new(true, config, null);
    public static ConfigLoadResult FailureResult(string errorMessage) => new(false, null, errorMessage);
}

