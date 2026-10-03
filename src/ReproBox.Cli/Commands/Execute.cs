using System.CommandLine;
using System.ComponentModel;

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

        command.SetAction(async (parseResult, ct) =>
        {
            string command = parseResult.GetValue(commandName);
            string[] arguments = parseResult.GetValue(argumentsArgument)
                ?? Array.Empty<string>();

            var result = await Execute(command, arguments, ct);

            IO.PrintRunResult(result);

            return result.ExitCode;
        });
        return command;
    }

    public static async Task<RunResult> Execute(
        string command,
        string[] arguments,
        CancellationToken ct = default)
    {
        var session = new Session(command, arguments);
        return await session.RunSession( ct);
    }


}