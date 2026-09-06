internal static class ReadProc

{

    public static ProcessInfo ReadProcessInfo(int pid)
    {

        ProcessInfo processStatus = ReadProcessStatus(pid);
        if (processStatus.Pid is null)
            return processStatus;

        processStatus = processStatus with
        {
            CommandLine = GetProcessCommandLine(pid),
            Executable = GetProcessExecutable(pid),
            WorkingDirectory = GetProcessCWD(pid),
            State = ParseStatState(ReadOptionalFile($"/proc/{pid}/stat")),
            Environment = ParseEnvironment(ReadOptionalFile($"/proc/{pid}/environ"))
        };

        return processStatus;
    }

    internal static string? ParseStatState(string? stat)
    {
        // comm is parenthesized and may itself contain spaces or parentheses.
        int end = stat?.LastIndexOf(')') ?? -1;
        if (end < 0)
            return null;
        return stat![(end + 1)..].Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
    }

    internal static IReadOnlyList<string>? ParseEnvironment(string? environment) =>
        environment?.Split('\0', StringSplitOptions.RemoveEmptyEntries);

    private static string? ReadOptionalFile(string path)
    {
        try
        {
            return File.ReadAllText(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error reading {path}: {e.Message}");
            return null;
        }
    }

    private static string? GetProcessCWD(int pid) => IO.ReadLink($"/proc/{pid}/cwd");

    private static string? GetProcessExecutable(int pid) => IO.ReadLink($"/proc/{pid}/exe");

    private static string? GetProcessCommandLine(int pid)
    {
        string filePath = $"/proc/{pid}/cmdline";
        string? cmd = null;
        try
        {
            cmd = File.ReadAllText(filePath)
                        .TrimEnd('\0')
                        .Replace('\0', ' ');
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error reading Command Line for PID {pid}: {e.Message}");
        }
        return cmd;
    }

    internal static ProcessInfo ParseLine(
        ProcessInfo status,
        string line)
    {
        if (line.StartsWith("Name:", StringComparison.Ordinal))
        {
            return status with
            {
                Name = line["Name:".Length..].Trim()
            };
        }

        if (line.StartsWith("Pid:", StringComparison.Ordinal))
        {
            return status with
            {
                Pid = int.TryParse(line["Pid:".Length..].Trim(), out int pid) ? pid : null
            };
        }

        if (line.StartsWith("PPid:", StringComparison.Ordinal))
        {
            return status with
            {
                ParentPid = int.TryParse(line["PPid:".Length..].Trim(), out int ppid) ? ppid : null
            };
        }

        if (line.StartsWith("Uid:", StringComparison.Ordinal))
        {
            return status with
            {
                UserId = uint.TryParse(line["Uid:".Length..].Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).FirstOrDefault(), out uint uid) ? uid : null
            };
        }

        if (line.StartsWith("Threads:", StringComparison.Ordinal))
        {
            return status with
            {
                Threads = int.TryParse(line["Threads:".Length..].Trim(), out int threads) ? threads : null
            };
        }
        if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
        {
            return status with
            {
                MemoryKb = long.TryParse(line["VmRSS:".Length..].Trim().Split(' ')[0], out long memoryKb) ? memoryKb : null
            };
        }




        return status;
    }
    private static ProcessInfo ReadProcessStatus(int pid)
    {
        string filePath = $"/proc/{pid}/status";

        ProcessInfo processStatus =
            new(null, null, null, null, null, null, null, null, null);

        try
        {
            foreach (string line in File.ReadLines(filePath))
            {
                processStatus = ParseLine(processStatus, line);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine(
                $"Error reading status for PID {pid}: {e.Message}"
            );
        }

        return processStatus;
    }
}