using System.Runtime.InteropServices;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Wingman.Agent.Configuration;
using Wingman.Agent.Tools;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent;

public class WingmanAgent
{
    private readonly WingmanConfig config;

    private readonly string systemPrompt = @"You are Wingman, a helpful AI assistant specialized in file organization.
    You help users organize their files, analyze directories, and manage their file systems efficiently.
    Be concise and helpful in your responses.";

    public WingmanAgent(WingmanConfig config)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Environment.Exit(1);
        
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.config.Validate();
    }

    private AIAgent CreateAnthropicAgent(string prompt, List<AITool> tools)
    {
        var chatClient = new AnthropicClient(new Anthropic.Core.ClientOptions { APIKey = config.ApiKey }).AsIChatClient(config.Model);
        return chatClient.AsAIAgent(prompt, tools: tools);
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task RunStreamingWithToolsAsync(string prompt, string? workingDirectory = null, Action<string, bool>? onTextUpdate = null)
    {
        PathExtensions.CurrentWorkingDirectory = workingDirectory;

        var basePrompt = $@"{this.systemPrompt}
        {(workingDirectory != null ? $"Current working directory: {workingDirectory}" : "")}

        When asked to organize files:
        1. First analyze the directory to understand what's there
        2. Suggest an organization strategy
        3. Ask for confirmation before making changes
        4. Use preview mode first, then apply changes if approved

        When the user provides a direct path or says ""here's this file"":
        - Use ResolvePath first to quickly resolve the path
        - Support absolute paths, relative paths, and file names
        - Check current working directory before searching

        Be helpful, safe, and always confirm before moving or modifying files.";

        var agent = CreateAnthropicAgent(basePrompt, ToolsFactory.CreateDefaultTools().ToList());
        
        await foreach (var update in agent.RunStreamingAsync(prompt))
        {
            var text = update.Text;
            onTextUpdate?.Invoke(text, update.Contents.Any(c => c.GetType().Name.Contains("Thinking", StringComparison.OrdinalIgnoreCase)));
        }
    }
}
