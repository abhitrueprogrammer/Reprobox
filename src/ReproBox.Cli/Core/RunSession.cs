using System.ComponentModel;
using System.Diagnostics;

class Session
{
    private string command;
    private string[] arguments;

    public Session(string command,
        string[] arguments
)
    {
        this.command = command;
        this.arguments = arguments;
    }

    public Process CreateProcess()
    {
        var process = new Process();
        process.StartInfo.FileName = this.command;
        foreach (var argument in this.arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        return process;
    }

    public async Task<RunResult> RunSession(CancellationToken ct = default)
    {
        using Process process = CreateProcess();


        try
        {
            process.Start();
        }
        catch (Win32Exception e)
        {

            int code = e.NativeErrorCode == 2 ? 127 : 126;
            return new RunResult(
                null,
                DateTimeOffset.Now,
                DateTimeOffset.Now,
                code,
                "",
                "",
                RunStatus.Failed);
        }

        DateTimeOffset startTime = DateTimeOffset.Now;

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
            // Failed to clean up
        }
    }
}