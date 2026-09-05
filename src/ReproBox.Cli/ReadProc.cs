internal static class ReadProc

{

    public static ProcessInfo ReadProcessInfo(int pid)
    {

        ProcessInfo processStatus = ReadProcessStatus(pid);
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
        catch (Exception e)
        {
            Console.WriteLine($"Error reading Command Line for PID {pid}: {e.Message}");
        }
        return cmd;
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
                if (line.StartsWith("Name:"))
                {
                    processStatus = processStatus with
                    {
                        Name = line["Name:".Length..].Trim()
                    };
                }
                else if (line.StartsWith("Pid:"))
                {
                    processStatus = processStatus with
                    {
                        Pid = int.Parse(line["Pid:".Length..].Trim())
                    };
                }
                else if (line.StartsWith("PPid:"))
                {
                    processStatus = processStatus with
                    {
                        ParentPid = int.Parse(line["PPid:".Length..].Trim())
                    };
                }
                else if (line.StartsWith("Uid:"))
                {
                    processStatus = processStatus with
                    {
                        UserId = UnixUser.GetUsername(uint.Parse(
                            line["Uid:".Length..].Trim().Split('\t')[0]
                        ))
                    };
                }
                else if (line.StartsWith("Threads:"))
                {
                    processStatus = processStatus with
                    {
                        Threads = int.Parse(
                            line["Threads:".Length..].Trim()
                        )
                    };
                }
                else if (line.StartsWith("VmRSS:"))
                {
                    processStatus = processStatus with
                    {
                        MemoryKb = long.Parse(
                            line["VmRSS:".Length..]
                                .Trim()
                                .Split(
                                    (char[]?)null,
                                    StringSplitOptions.RemoveEmptyEntries
                                )[0]
                        )
                    };
                }
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(
                $"Error reading status for PID {pid}: {e.Message}"
            );
        }

        return processStatus;
    }
}