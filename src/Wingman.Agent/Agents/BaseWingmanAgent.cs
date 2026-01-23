using System.Runtime.Versioning;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Wingman.Agent.Configuration;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent.Agents;

/// <summary>
/// Base class for specialized Wingman agents with common functionality.
/// </summary>
[SupportedOSPlatform("windows")]
public abstract class BaseWingmanAgent : IWingmanAgent
{
    protected readonly WingmanConfig Config;

    protected BaseWingmanAgent(WingmanConfig config)
    {
        Config = config ?? throw new ArgumentNullException(nameof(config));
        Config.Validate();
    }

    /// <inheritdoc />
    public abstract string Name { get; }

    /// <inheritdoc />
    public abstract string Description { get; }

    /// <summary>
    /// Gets the system prompt for this specialized agent.
    /// </summary>
    protected abstract string SystemPrompt { get; }

    /// <summary>
    /// Gets the tools available to this agent.
    /// </summary>
    protected abstract IReadOnlyList<AITool> GetTools();

    /// <summary>
    /// Gets additional context to append to the system prompt based on the working directory.
    /// </summary>
    protected virtual string GetContextualPrompt(string? workingDirectory)
    {
        return workingDirectory != null
            ? $"\n\nCurrent working directory: {workingDirectory}"
            : string.Empty;
    }

    /// <inheritdoc />
    public async Task RunStreamingAsync(string prompt, string? workingDirectory = null, Action<string, bool>? onTextUpdate = null)
    {
        PathExtensions.CurrentWorkingDirectory = workingDirectory;

        var fullPrompt = SystemPrompt + GetContextualPrompt(workingDirectory);
        var agent = CreateAgent(fullPrompt, GetTools().ToList());

        await foreach (var update in agent.RunStreamingAsync(prompt))
        {
            var text = update.Text;
            var isThinking = update.Contents.Any(c => 
                c.GetType().Name.Contains("Thinking", StringComparison.OrdinalIgnoreCase));
            onTextUpdate?.Invoke(text, isThinking);
        }
    }

    private AIAgent CreateAgent(string prompt, List<AITool> tools)
    {
        var chatClient = new AnthropicClient(new Anthropic.Core.ClientOptions { APIKey = Config.ApiKey })
            .AsIChatClient(Config.Model);
        return chatClient.AsAIAgent(prompt, tools: tools);
    }
}
