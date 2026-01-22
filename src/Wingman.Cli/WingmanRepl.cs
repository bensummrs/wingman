using Spectre.Console;
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
        ShowWelcomeBanner();

        while (true)
        {
            var shortPath = GetShortPath(workingDirectory);
            AnsiConsole.Markup($"[bold cyan]wingman[/] [dim]{shortPath}[/] [bold green]❯[/] ");
            var input = Console.ReadLine()?.Trim() ?? string.Empty;

            if (input.Length == 0)
                continue;

            if (input.StartsWith('/'))
            {
                var parsed = ReplCommands.Parse(input);
                var handled = ReplCommands.TryHandle(parsed, config, history, ref agent, ref workingDirectory);
                if (handled.ShouldExit)
                {
                    AnsiConsole.MarkupLine("[dim]Goodbye! 👋[/]");
                    return 0;
                }
                continue;
            }

            var composedPrompt = history.ComposePrompt(input);

            try
            {
                AnsiConsole.WriteLine();
                var fullResponse = new System.Text.StringBuilder();

                await agent.RunStreamingWithToolsAsync(composedPrompt, workingDirectory, onTextUpdate: text =>
                {
                    AnsiConsole.Markup($"[grey]{Markup.Escape(text)}[/]");
                    fullResponse.Append(text);
                });

                AnsiConsole.WriteLine();
                AnsiConsole.WriteLine();

                var text = fullResponse.ToString();
                if (string.IsNullOrWhiteSpace(text))
                    text = "(no text content returned)";

                history.AddUser(input);
                history.AddAssistant(text);
            }
            catch (Exception ex)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[red bold]✗[/] [red]Error:[/] {Markup.Escape(ex.Message)}");
                if (ex.InnerException != null)
                {
                    AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.InnerException.Message)}[/]");
                }
                AnsiConsole.WriteLine();
            }
        }
    }

    private void ShowWelcomeBanner()
    {
        var panel = new Panel(new Markup(
            "[bold cyan]Wingman AI Assistant[/]\n" +
            $"[dim]Model:[/] [yellow]{config.Model}[/]\n" +
            "[dim]Type [cyan]/help[/] for commands[/]"))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(foreground: Color.Cyan),
            Padding = new Padding(1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private static string GetShortPath(string path)
    {
        try
        {
            var currentDir = new DirectoryInfo(path);
            var parentName = currentDir.Parent?.Name;
            
            if (parentName != null)
                return $"{parentName}/{currentDir.Name}";
            
            return currentDir.Name;
        }
        catch
        {
            return path;
        }
    }
}

