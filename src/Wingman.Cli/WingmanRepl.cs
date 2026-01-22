using Wingman.Agent;
using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public sealed class WingmanRepl
{
    private readonly WingmanConfig config;
    private readonly ConversationHistory history = new();
    private WingmanAgent agent;
    private string workingDirectory;

    public WingmanRepl(WingmanConfig config, string initialWorkingDirectory)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
        workingDirectory = initialWorkingDirectory ?? throw new ArgumentNullException(nameof(initialWorkingDirectory));
        agent = new WingmanAgent(this.config);
    }

    public async Task<int> RunAsync()
    {
        Console.WriteLine("Wingman CLI (interactive). Type /help for commands.");

        while (true)
        {
            Console.Write($"wingman ({workingDirectory})> ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;

            if (input.Length == 0)
                continue;

            if (input.StartsWith('/'))
            {
                var parsed = ReplCommands.Parse(input);
                var handled = ReplCommands.TryHandle(parsed, config, history, ref agent, ref workingDirectory);
                if (handled.ShouldExit)
                    return 0;
                continue;
            }

            var composedPrompt = history.ComposePrompt(input);

            try
            {
                Console.WriteLine();
                var fullResponse = new System.Text.StringBuilder();

                await agent.RunStreamingWithToolsAsync(composedPrompt, workingDirectory, onTextUpdate: text =>
                {
                    Console.Write(text);
                    fullResponse.Append(text);
                });

                Console.WriteLine();
                Console.WriteLine();

                var text = fullResponse.ToString();
                if (string.IsNullOrWhiteSpace(text))
                    text = "(no text content returned)";

                history.AddUser(input);
                history.AddAssistant(text);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                Console.WriteLine();
            }
        }

        return 0;
    }
}

