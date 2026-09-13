using System.CommandLine;

internal class Program
{
    internal static int Main(string[] args)      
    {
        var rootCommand = new RootCommand(
            "ReproBox - inspect and reproduce process execution");

        rootCommand.Subcommands.Add(InspectCommand.Create());
        rootCommand.Subcommands.Add(RunCommand.Create());

        return rootCommand.Parse(args).Invoke();
    }
}