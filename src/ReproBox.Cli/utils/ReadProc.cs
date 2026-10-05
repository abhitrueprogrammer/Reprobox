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
            WorkingDirectory = GetProcessCWD(pid)
        };

        return processStatus;
    }

    private static string? GetProcessCWD(int pid) => IO.ReadLink($"/proc/{pid}/cwd");

    private static string? GetProcessExecutable(int pid) => IO.ReadLink($"/proc/{pid}/exe");

    private static string[]? GetProcessCommandLine(int pid)
    {
        string filePath = $"/proc/{pid}/cmdline";
        string[]? cmd = null;
        try
        {
            cmd = File.ReadAllText(filePath)
                        .TrimEnd('\0')
                        .Split('\0', StringSplitOptions.RemoveEmptyEntries);

        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error reading Command Line for PID {pid}: {e.Message}");
        }
        return cmd;
    }
    public static int[]? GetChildren(int pid)
    {
        
        string filePath = $"/proc/{pid}/task";
        // Get all the thread IDs for the given process
        var directories = new DirectoryInfo(filePath);
        var threadIds = directories.GetDirectories().Select(d => d.Name)
            .Where(name => int.TryParse(name, out _))
            .Select(int.Parse)
            .ToArray();

        int []? children = null;
        foreach (var threadId in threadIds)
        {
            string childrenFilePath = $"/proc/{pid}/task/{threadId}/children";
            var threadChildren = IO.ReadChildren(childrenFilePath, pid);
            if (threadChildren != null)
            {
                if (children == null)
                {
                    children = threadChildren;
                }
                else
                {
                    children = children.Concat(threadChildren).ToArray();
                }
            }
        }
        return children;
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
            new(null, null, null, null, null, null, null, null, null,null);

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