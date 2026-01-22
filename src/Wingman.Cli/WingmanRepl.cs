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
                var response = await agent.RunWithToolsAsync(composedPrompt, workingDirectory);
                var text = ChatResponseText.ExtractText(response);
                if (string.IsNullOrWhiteSpace(text))
                    text = "(no text content returned)";

                Console.WriteLine();
                Console.WriteLine(text);
                Console.WriteLine();

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

