namespace Wingman.Agent.Configuration;

public class WingmanConfig
{
    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "claude-3-5-sonnet-20241022";

    public int MaxTokens { get; set; } = 4096;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
            throw new InvalidOperationException("ApiKey is required. Set the ANTHROPIC_API_KEY environment variable.");
    }
}
