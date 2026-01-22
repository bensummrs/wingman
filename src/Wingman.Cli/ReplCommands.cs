using Wingman.Agent;
using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public static class ReplCommands
{
    public static void PrintHelp()
    {
        Console.WriteLine("""
Commands:
  /help                 Show this help
  /exit, /quit           Exit
  /cwd                  Show current working directory used for tools
  /cd <path>             Change working directory used for tools
  /reset                 Clear chat history
  /clear                 Clear screen
  /model <name>          Set model (recreates agent)

Anything else is sent to Wingman as your prompt.
""");
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
                Console.WriteLine(workingDirectory);
                return CommandHandleResult.Handled();

            case "/cd":
            {
                if (string.IsNullOrWhiteSpace(parsed.Argument))
                {
                    Console.WriteLine("Usage: /cd <path>");
                    return CommandHandleResult.Handled();
                }

                var next = Path.GetFullPath(parsed.Argument);
                if (!Directory.Exists(next))
                {
                    Console.WriteLine($"Directory does not exist: {next}");
                    return CommandHandleResult.Handled();
                }

                workingDirectory = next;
                return CommandHandleResult.Handled();
            }

            case "/reset":
                history.Clear();
                Console.WriteLine("History cleared.");
                return CommandHandleResult.Handled();

            case "/clear":
                Console.Clear();
                return CommandHandleResult.Handled();

            case "/model":
            {
                if (string.IsNullOrWhiteSpace(parsed.Argument))
                {
                    Console.WriteLine("Usage: /model <name>");
                    return CommandHandleResult.Handled();
                }

                config.Model = parsed.Argument;
                agent = new WingmanAgent(config);
                history.Clear();
                Console.WriteLine($"Model set to: {config.Model}");
                return CommandHandleResult.Handled();
            }

            default:
                Console.WriteLine("Unknown command. Type /help.");
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

