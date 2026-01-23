using System.Runtime.Versioning;
using Anthropic;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Workflows;
using Microsoft.Extensions.AI;
using Wingman.Agent.Agents;
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

    private IChatClient CreateChatClient()
    {
        return new AnthropicClient(new Anthropic.Core.ClientOptions { APIKey = _config.ApiKey })
            .AsIChatClient(_config.Model);
    }

    private Workflow BuildWorkflow()
    {
        var client = CreateChatClient();

        var fileOrganizerDef = new FileOrganizerAgent(_config);
        var spreadsheetDef = new SpreadsheetAgent(_config);
        var spreadsheetCreatorDef = new SpreadsheetCreatorAgent(_config);
        var pdfDef = new PdfAgent(_config);
        var triageInstructions = $@"You are Wingman, a helpful AI assistant. Your job is to understand the user's request and route them to the appropriate specialist agent.

        Available specialists:
        - {fileOrganizerDef.Name.ToLowerInvariant()}: {fileOrganizerDef.Description}
        - {spreadsheetDef.Name.ToLowerInvariant()}: {spreadsheetDef.Description}
        - {spreadsheetCreatorDef.Name.ToLowerInvariant()}: {spreadsheetCreatorDef.Description}
        - {pdfDef.Name.ToLowerInvariant()}: {pdfDef.Description}

        ALWAYS handoff to the appropriate specialist based on the user's request. If the request doesn't clearly fit a specialist, use {fileOrganizerDef.Name.ToLowerInvariant()} as the default.";

        var triageAgent = new ChatClientAgent(
            client,
            instructions: triageInstructions,
            name: "triage",
            description: "Routes user requests to the appropriate specialist agent");

        var fileOrganizerAgent = new ChatClientAgent(
            client,
            instructions: fileOrganizerDef.Instructions,
            name: fileOrganizerDef.Name.ToLowerInvariant(),
            description: fileOrganizerDef.Description,
            tools: fileOrganizerDef.Tools.ToList());

        var spreadsheetAgent = new ChatClientAgent(
            client,
            instructions: spreadsheetDef.Instructions,
            name: spreadsheetDef.Name.ToLowerInvariant(),
            description: spreadsheetDef.Description,
            tools: spreadsheetDef.Tools.ToList());

        var spreadsheetCreatorAgent = new ChatClientAgent(
            client,
            instructions: spreadsheetCreatorDef.Instructions,
            name: spreadsheetCreatorDef.Name.ToLowerInvariant(),
            description: spreadsheetCreatorDef.Description,
            tools: spreadsheetCreatorDef.Tools.ToList());

        var pdfAgent = new ChatClientAgent(
            client,
            instructions: pdfDef.Instructions,
            name: pdfDef.Name.ToLowerInvariant(),
            description: pdfDef.Description,
            tools: pdfDef.Tools.ToList());

        return AgentWorkflowBuilder.CreateHandoffBuilderWith(triageAgent)
            .WithHandoffs(triageAgent, [fileOrganizerAgent, spreadsheetAgent, spreadsheetCreatorAgent, pdfAgent])
            .WithHandoff(fileOrganizerAgent, triageAgent)
            .WithHandoff(spreadsheetAgent, triageAgent)
            .WithHandoff(spreadsheetCreatorAgent, triageAgent)
            .WithHandoff(pdfAgent, triageAgent)
            .Build();
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

    public void ClearHistory() => _messages.Clear();
}
