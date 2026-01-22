using Wingman.Agent.Configuration;
using Wingman.Cli;


var load = WingmanCliConfigLoader.TryLoad();
if (!string.IsNullOrEmpty(load.ErrorMessage))
{
    Console.Error.WriteLine(load.ErrorMessage);
    return 1;
}

WingmanConfig config = load.Config!;
var repl = new WingmanRepl(config, initialWorkingDirectory: Directory.GetCurrentDirectory());
return await repl.RunAsync();
