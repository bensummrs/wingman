using Spectre.Console;
using Wingman.Agent.Configuration;
using Wingman.Cli;

Console.BackgroundColor = ConsoleColor.Black;
Console.Clear();

var load = WingmanCliConfigLoader.TryLoad();
if (!string.IsNullOrEmpty(load.ErrorMessage))
{
    AnsiConsole.MarkupLine($"[red bold]✗ Configuration Error[/]\n[red]{Markup.Escape(load.ErrorMessage)}[/]");
    return 1;
}

WingmanConfig config = load.Config!;
var repl = new WingmanRepl(config, initialWorkingDirectory: Directory.GetCurrentDirectory());
return await repl.RunAsync();
