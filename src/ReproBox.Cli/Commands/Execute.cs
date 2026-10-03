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
        CancellationToken ct = default
        )
    {
        // library used to run the process, providing abstraction over the platform-specific details of process execution
        using var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = command;
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;


        var currentTime = DateTimeOffset.Now;
        try
        {
            process.Start();

        }
        catch (Win32Exception e)
        {
            if (e.NativeErrorCode == 2)
            {

                return new RunResult(
                    0,
                    currentTime,
                    DateTimeOffset.Now,
                    127,
                    "",
                    e.Message
                );
            }
            else
            {
                return new RunResult(
                    0,
                    currentTime,
                    DateTimeOffset.Now,
                    126,
                    "",
                    e.Message
                );
            }
        }
        currentTime = DateTimeOffset.Now;
        Task<String> readStd = process.StandardOutput.ReadToEndAsync();
        Task<String> readErr = process.StandardError.ReadToEndAsync();
        try
        {





            await Task.WhenAll(readStd, readErr, process.WaitForExitAsync(ct));

            var standardOutput = await readStd;
            var standardError = await readErr;

            return new RunResult(
                process.Id,
                currentTime,
                DateTimeOffset.Now,
                process.ExitCode,
                standardOutput,
                standardError
            );
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await Task.WhenAll(readStd, readErr, process.WaitForExitAsync());

            var standardOutput = await readStd;
            var standardError = await readErr;
            return new RunResult(
                process.Id,
                currentTime,
                DateTimeOffset.Now,
                137,
                standardOutput,
                standardError
            );
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }

    }

}