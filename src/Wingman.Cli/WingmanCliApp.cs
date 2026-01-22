using Wingman.Agent.Configuration;

namespace Wingman.Cli;

public static class WingmanCliApp
{
    public static async Task<int> RunAsync(string[] args)
    {
        var load = WingmanCliConfigLoader.TryLoadFromEnvironment();
        if (!string.IsNullOrEmpty(load.ErrorMessage))
        {
            Console.Error.WriteLine(load.ErrorMessage);
            return 1;
        }

        WingmanConfig config = load.Config!;
        var repl = new WingmanRepl(config, initialWorkingDirectory: Directory.GetCurrentDirectory());
        return await repl.RunAsync();
    }
}

