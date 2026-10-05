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
        var cwdArgument = new Option<string>("cwd")
        {
            Description = "Current working directory for the process"
        };

        var command = new Command(
            "run",
            "Run a process");

        command.Arguments.Add(commandName);
        command.Arguments.Add(argumentsArgument);
        command.Options.Add(cwdArgument);

        command.SetAction(async (parseResult, ct) =>
        {
            string command = parseResult.GetValue(commandName);
            string[] arguments = parseResult.GetValue(argumentsArgument)
                ?? Array.Empty<string>();
            string cwd = parseResult.GetValue(cwdArgument) ?? Environment.CurrentDirectory;
            var result = await Execute(command, arguments, cwd, ct);

            IO.PrintRunResult(result);

            return result.ExitCode;
        });
        return command;
    }

    public static async Task<RunResult> Execute(
        string command,
        string[] arguments,
        string cwd,
        CancellationToken ct = default)
    {
        var session = new Session(command, arguments, cwd);

        using var ctPoller = new CancellationTokenSource();
        Task<Dictionary<int, int[]>>? observerTask = null;
        Dictionary<int, int[]>? observed = null;
        RunResult runResult;
        try
        {
            runResult = await session.RunSession(ct, pid =>
           {
               var observer = new PollingObserver(pid);
               observerTask = observer.ObserveAsync(ctPoller.Token);

           });

        }
        finally
        {
            ctPoller.Cancel();
            if (observerTask is not null)
            {
                observed = await observerTask;
            }

        }
        if (observed is not null)
        {
            IO.PrintTree(runResult.Pid ?? -1, observed);
        }

        return runResult;

    }

}