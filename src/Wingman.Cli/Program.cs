using System.Runtime.InteropServices;
using Spectre.Console;
using Wingman.Agent.Configuration;
using Wingman.Cli;

//you could pull down and remove the macos runtime check block, but doing so will make me really upset x
if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
{
    Console.Error.WriteLine("macOS is not supported.");
    Environment.Exit(1);
    return 1;
}

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
