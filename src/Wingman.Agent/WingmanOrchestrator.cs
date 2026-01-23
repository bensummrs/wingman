using System.Runtime.Versioning;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Wingman.Agent.Configuration;
using Wingman.Agent.Tools.Extensions;

namespace Wingman.Agent;

[SupportedOSPlatform("windows")]
public class WingmanOrchestrator
{
    private readonly WingmanConfig _config;
    private readonly List<ChatMessage> _messages = [];

    public WingmanOrchestrator(WingmanConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));
        _config.Validate();
    }

    public async Task RunStreamingAsync(string userInput, string? workingDirectory = null, Action<string, bool>? onTextUpdate = null)
    {
        PathExtensions.CurrentWorkingDirectory = workingDirectory;

        _messages.Add(new ChatMessage(ChatRole.User, userInput));

        var workflow = BuildWorkflow();
        var run = await InProcessExecution.StreamAsync(workflow, _messages);
        await run.TrySendMessageAsync(new TurnToken(emitEvents: true));

        await foreach (var evt in run.WatchStreamAsync())
        {
            if (evt is AgentResponseUpdateEvent e)
            {
                onTextUpdate?.Invoke(e.Data?.ToString() ?? "", false);
            }
            else if (evt is WorkflowOutputEvent outputEvt && outputEvt.Data is List<ChatMessage> newMessages)
            {
                foreach (var msg in newMessages.Skip(_messages.Count))
                {
                    _messages.Add(msg);
                }
                break;
            }
        }
    }

    private Workflow BuildWorkflow()
    {
        var client = CreateChatClient();
        var definitions = AgentDefinitions.All;

        var agentList = string.Join("\n", definitions.Select(d => $"- {d.Name}: {d.Description}"));
        var triageInstructions = $"""
        You are Wingman, a helpful AI assistant. Your job is to understand the user's request and route them to the appropriate specialist agent.

        Available specialists:
        {agentList}

        ALWAYS handoff to the appropriate specialist based on the user's request. If the request doesn't clearly fit a specialist, use fileorganizer as the default.
        """;

        var triageAgent = new ChatClientAgent(
            client,
            instructions: triageInstructions,
            name: "triage",
            description: "Routes user requests to the appropriate specialist agent");

        var specialists = definitions
            .Select(d => new ChatClientAgent(
                client,
                instructions: d.Instructions,
                name: d.Name,
                description: d.Description,
                tools: d.GetTools().ToList()))
            .ToArray();

        var builder = AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent).WithHandoffs(triageAgent, specialists);

        foreach (var specialist in specialists)
            builder.WithHandoff(specialist, triageAgent);

        return builder.Build();
    }

    private IChatClient CreateChatClient()
    {
        return new AnthropicClient(new Anthropic.Core.ClientOptions { APIKey = _config.ApiKey }).AsIChatClient(_config.Model);
    }

    public void ClearHistory() => _messages.Clear();
}
