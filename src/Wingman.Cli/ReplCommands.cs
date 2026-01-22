using Spectre.Console;
using Wingman.Agent;
using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public static class ReplCommands
{
    public static void PrintHelp()
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Grey)
            .AddColumn(new TableColumn("[cyan]Command[/]").LeftAligned())
            .AddColumn(new TableColumn("[cyan]Description[/]").LeftAligned());

        table.AddRow("[yellow]/help[/]", "Show this help");
        table.AddRow("[yellow]/exit[/], [yellow]/quit[/]", "Exit the REPL");
        table.AddRow("[yellow]/cwd[/]", "Show current working directory");
        table.AddRow("[yellow]/cd[/] [dim]<path>[/]", "Change working directory");
        table.AddRow("[yellow]/reset[/]", "Clear conversation history");
        table.AddRow("[yellow]/clear[/]", "Clear screen");
        table.AddRow("[yellow]/model[/] [dim]<name>[/]", "Switch AI model (recreates agent)");

        AnsiConsole.Write(table);
        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[dim]Type anything else to chat with Wingman[/]");
    }

    public static CommandParseResult Parse(string input)
    {
        var parts = input.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1] : null;
        return new CommandParseResult(cmd, arg);
    }

    public static CommandHandleResult TryHandle(
        CommandParseResult parsed,
        WingmanConfig config,
        ConversationHistory history,
        ref WingmanAgent agent,
        ref string workingDirectory)
    {
        switch (parsed.Command)
        {
            case "/help":
                PrintHelp();
                return CommandHandleResult.Handled();

            case "/exit":
            case "/quit":
                return CommandHandleResult.Exit();

            case "/cwd":
                AnsiConsole.MarkupLine($"[cyan]Working directory:[/] [yellow]{Markup.Escape(workingDirectory)}[/]");
                return CommandHandleResult.Handled();

            case "/cd":
            {
                if (string.IsNullOrWhiteSpace(parsed.Argument))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /cd <path>");
                    return CommandHandleResult.Handled();
                }

                var next = Path.GetFullPath(parsed.Argument);
                if (!Directory.Exists(next))
                {
                    AnsiConsole.MarkupLine($"[red]✗[/] Directory does not exist: [yellow]{Markup.Escape(next)}[/]");
                    return CommandHandleResult.Handled();
                }

                workingDirectory = next;
                AnsiConsole.MarkupLine($"[green]✓[/] Changed to: [yellow]{Markup.Escape(workingDirectory)}[/]");
                return CommandHandleResult.Handled();
            }

            case "/reset":
                history.Clear();
                AnsiConsole.MarkupLine("[green]✓[/] Conversation history cleared");
                return CommandHandleResult.Handled();

            case "/clear":
                AnsiConsole.Clear();
                return CommandHandleResult.Handled();

            case "/model":
            {
                if (string.IsNullOrWhiteSpace(parsed.Argument))
                {
                    AnsiConsole.MarkupLine("[yellow]Usage:[/] /model <name>");
                    return CommandHandleResult.Handled();
                }

                config.Model = parsed.Argument;
                agent = new WingmanAgent(config);
                history.Clear();
                AnsiConsole.MarkupLine($"[green]✓[/] Model set to: [yellow]{config.Model}[/]");
                AnsiConsole.MarkupLine("[dim]Conversation history cleared[/]");
                return CommandHandleResult.Handled();
            }

            default:
                AnsiConsole.MarkupLine($"[red]✗[/] Unknown command: [yellow]{Markup.Escape(parsed.Command)}[/]");
                AnsiConsole.MarkupLine("[dim]Type [cyan]/help[/] to see available commands[/]");
                return CommandHandleResult.Handled();
        }
    }
}

public readonly record struct CommandParseResult(string Command, string? Argument);

public readonly record struct CommandHandleResult(bool WasHandled, bool ShouldExit)
{
    public static CommandHandleResult Handled() => new(true, false);
    public static CommandHandleResult Exit() => new(true, true);
}

