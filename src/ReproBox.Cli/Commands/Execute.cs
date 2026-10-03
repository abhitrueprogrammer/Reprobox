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
        using var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = command;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;

        var startTime = DateTimeOffset.Now;
        try
        {
            process.Start();
        }
        catch (Win32Exception e)
        {

            int code = e.NativeErrorCode == 2 ? 127 : 126;
            return new RunResult(
                null,
                startTime,
                DateTimeOffset.Now,
                code,
                "",
                "",
                RunStatus.Failed);
        }

        startTime = DateTimeOffset.Now;

        // No ct token here, so partial output survives a cancel
        Task<string> readStd = process.StandardOutput.ReadToEndAsync();
        Task<string> readErr = process.StandardError.ReadToEndAsync();

        try
        {
            try
            {
                await process.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                KillTree(process);
                await process.WaitForExitAsync();
            }

            string standardOutput = await readStd;
            string standardError = await readErr;

            var status = ct.IsCancellationRequested
                ? RunStatus.Cancelled
                : RunStatus.Exited;

            return new RunResult(
                process.Id,
                startTime,
                DateTimeOffset.Now,
                process.ExitCode,
                standardOutput,
                standardError,
                status);
        }
        finally
        {
            KillTree(process); // safety net
        }
    }

    private static void KillTree(System.Diagnostics.Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
            Console.WriteLine("Failed to kill process tree");
        }
    }
}