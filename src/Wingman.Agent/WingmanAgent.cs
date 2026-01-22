using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;
using Wingman.Agent.Configuration;
using Wingman.Agent.Tools;

namespace Wingman.Agent;

public class WingmanAgent
{
    private readonly WingmanConfig config;

    private readonly string systemPrompt = @"You are Wingman, a helpful AI assistant specialized in file organization.
    You help users organize their files, analyze directories, and manage their file systems efficiently.
    Be concise and helpful in your responses.";

    public WingmanAgent(WingmanConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        this.config.Validate();
    }

    private async Task<AIAgent> CreateAgent(string prompt, List<AITool> tools)
    {
        var chatClient = new OpenAIClient(config.ApiKey!).GetChatClient(config.Model);
        return chatClient.AsAIAgent(prompt, tools: tools);
    }

    public async Task<ChatResponse> RunWithToolsAsync(string prompt, string? workingDirectory = null)
    {
        var basePrompt = $@"{this.systemPrompt}
        {(workingDirectory != null ? $"Current working directory: {workingDirectory}" : "")}

        When asked to organize files:
        1. First analyze the directory to understand what's there
        2. Suggest an organization strategy
        3. Ask for confirmation before making changes
        4. Use preview mode first, then apply changes if approved

        Be helpful, safe, and always confirm before moving or modifying files.";

        var agent = await CreateAgent(basePrompt, ToolsFactory.CreateDefaultTools().ToList());
        var response = await agent.RunAsync(prompt);
        return response.AsChatResponse();
    }
}
