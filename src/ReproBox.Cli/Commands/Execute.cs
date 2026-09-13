using System.CommandLine;

internal static class RunCommand
{
    public static Command Create()
    {
        var commandName = new Argument<string>("command")
        {
            Description = "Command name of the process to run"
        };
        var argumentsArgument = new Argument<string[]>("arguments")
        {
            Description = "Arguments passed to the program",
            Arity = ArgumentArity.ZeroOrMore
        };

        var command = new Command(
            "run",
            "Run a process");

        command.Arguments.Add(commandName);
        command.Arguments.Add(argumentsArgument);

        return command;
    }
    
}