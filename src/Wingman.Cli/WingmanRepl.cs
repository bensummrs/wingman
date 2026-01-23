using System.Runtime.Versioning;
using System.Text;
using Spectre.Console;
using Wingman.Agent;
using Wingman.Agent.Configuration;

namespace Wingman.Cli;

[SupportedOSPlatform("windows")]
public sealed class WingmanRepl
{
    private readonly WingmanConfig config;
    private readonly ConversationHistory history = new();
    private WingmanAgent agent;
    private WingmanOrchestrator orchestrator;
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
            var input = ReadUserInput();
            
            if (string.IsNullOrEmpty(input))
                continue;

            if (input.StartsWith('/'))
            {
                if (HandleCommand(input))
                    return 0;
                continue;
            }

            await ProcessUserMessageAsync(input);
        }
    }

    private string ReadUserInput()
    {
        var shortPath = GetShortPath(workingDirectory);
        AnsiConsole.Markup($"[bold cyan]wingman[/] [dim]{shortPath}[/] [bold green]❯[/] ");
        return Console.ReadLine()?.Trim() ?? string.Empty;
    }

    private bool HandleCommand(string input)
    {
        var parsed = ReplCommands.Parse(input);
        var handled = ReplCommands.TryHandle(parsed, config, history, ref orchestrator, ref workingDirectory);
        
        if (handled.ShouldExit)
            AnsiConsole.MarkupLine("[dim]Goodbye! 👋[/]");
        
        return handled.ShouldExit;
    }

    private async Task ProcessUserMessageAsync(string input)
    {
        var composedPrompt = history.ComposePrompt(input);

        try
        {
            AnsiConsole.WriteLine();
            var fullResponse = new StringBuilder();

            await agent.RunStreamingWithToolsAsync(composedPrompt, workingDirectory, onTextUpdate: (text, isThinking) =>
            {
                var color = isThinking ? Color.Grey : Color.White;
                AnsiConsole.Write(new Text(text, new Style(foreground: color)));
                fullResponse.Append(text);
            });

            AnsiConsole.WriteLine();
            AnsiConsole.WriteLine();

            var responseText = fullResponse.ToString();
            history.AddUser(input);
            history.AddAssistant(string.IsNullOrWhiteSpace(responseText) ? "(no text content returned)" : responseText);
        }
        catch (Exception ex)
        {
            DisplayError(ex);
        }
    }

    private static void DisplayError(Exception ex)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine($"[red bold]✗[/] [red]Error:[/] {Markup.Escape(ex.Message)}");
        
        if (ex.InnerException != null)
            AnsiConsole.MarkupLine($"[dim]{Markup.Escape(ex.InnerException.Message)}[/]");
        
        AnsiConsole.WriteLine();
    }

    private void ShowWelcomeBanner()
    {
        var bannerLines = new[]
        {
            "██╗    ██╗██╗███╗   ██╗ ██████╗ ███╗   ███╗ █████╗ ███╗   ██╗",
            "██║    ██║██║████╗  ██║██╔════╝ ████╗ ████║██╔══██╗████╗  ██║",
            "██║ █╗ ██║██║██╔██╗ ██║██║  ███╗██╔████╔██║███████║██╔██╗ ██║",
            "██║███╗██║██║██║╚██╗██║██║   ██║██║╚██╔╝██║██╔══██║██║╚██╗██║",
            "╚███╔███╔╝██║██║ ╚████║╚██████╔╝██║ ╚═╝ ██║██║  ██║██║ ╚████║",
            " ╚══╝╚══╝ ╚═╝╚═╝  ╚═══╝ ╚═════╝ ╚═╝     ╚═╝╚═╝  ╚═╝╚═╝  ╚═══╝"
        };

        AnsiConsole.WriteLine();
        foreach (var line in bannerLines)
        {
            AnsiConsole.MarkupLine($"[#00CED1]{line}[/]");
        }
        AnsiConsole.WriteLine();
        
        AnsiConsole.MarkupLine($"  [dim]Model:[/] [yellow]{config.Model}[/]  [dim]•[/]  [dim]Type[/] [cyan]/help[/] [dim]for commands[/]");
        AnsiConsole.WriteLine();
    }

    private static string GetShortPath(string path)
    {
        try
        {
            var currentDir = new DirectoryInfo(path);
            var parentName = currentDir.Parent?.Name;
            return parentName != null ? $"{parentName}/{currentDir.Name}" : currentDir.Name;
        }
        catch
        {
            return path;
        }
    }
}

