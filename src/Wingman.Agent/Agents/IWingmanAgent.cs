namespace Wingman.Agent.Agents;

/// <summary>
/// Interface for specialized Wingman agents.
/// </summary>
public interface IWingmanAgent
{
    /// <summary>
    /// Gets the agent's name/identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets a description of what this agent specializes in.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Runs the agent with streaming output.
    /// </summary>
    /// <param name="prompt">The user's prompt/request.</param>
    /// <param name="workingDirectory">Optional working directory context.</param>
    /// <param name="onTextUpdate">Callback for streaming text updates.</param>
    Task RunStreamingAsync(string prompt, string? workingDirectory = null, Action<string, bool>? onTextUpdate = null);
}
