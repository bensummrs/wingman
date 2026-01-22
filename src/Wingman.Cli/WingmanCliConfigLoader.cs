using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public static class WingmanCliConfigLoader
{
    public static ConfigLoadResult TryLoadFromEnvironment()
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
            return ConfigLoadResult.FailureResult(
                "Missing API key. Set ANTHROPIC_API_KEY (or OPENAI_API_KEY) and try again.");
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

