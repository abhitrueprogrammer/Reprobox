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

        command.SetAction(parseResult =>
        {
            string command = parseResult.GetValue(commandName);
            string[] arguments = parseResult.GetValue(argumentsArgument)
                ?? Array.Empty<string>();

            var result = Execute(command, arguments);

            IO.PrintRunResult(result);

            return result.ExitCode;
        });
        return command;
    }

    public static RunResult Execute(
        string command,
        string[] arguments
        )
    {
        var process = new System.Diagnostics.Process();
        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = string.Join(" ", arguments);
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        var stdOutput = "";
        var stdError = "";
        process.OutputDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                stdOutput += e.Data + Environment.NewLine;
        };

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data != null)
                stdError += e.Data + Environment.NewLine;
        };

        var currentTime = DateTimeOffset.Now;
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            process.WaitForExit();
            return new RunResult(
                process.Id,
                currentTime,
                DateTimeOffset.Now,
                process.ExitCode,
                stdOutput,
                stdError
            );
        }
        catch (Exception e)
        {
            Console.Error.WriteLine($"Error starting process: {e.Message}");
            return new RunResult(
                0,
                currentTime,
                DateTimeOffset.Now,
                1,
                "",
                e.Message
            );
        }
    }
    
}