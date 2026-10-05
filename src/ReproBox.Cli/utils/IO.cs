internal class IO
{
    public static bool TryParsePid(string? input, out int pid)
    {
        return int.TryParse(input, out pid) && pid > 0;
    }

    public static int? AskPid()
    {
        while (true)
        {
            Console.Write("Please enter a PID: ");
            string? input = Console.ReadLine();
            if (input is null)
                return null;


            if (TryParsePid(input, out int pid))
            {
                return pid;
            }

            Console.WriteLine("Invalid input. Please enter a valid integer PID.");
        }


    }
    public static string? ReadLink(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error reading link {path}: {e.Message}");
            return null;
        }
    }
    public static void PrintProcessStatus(ProcessInfo processStatus)
    {
        Console.WriteLine($"Process Name: {processStatus.Name}");
        Console.WriteLine($"PID: {processStatus.Pid}");
        Console.WriteLine($"Parent PID: {processStatus.ParentPid}");
        Console.WriteLine($"User ID: {UnixUser.GetUsername(processStatus.UserId)}");
        Console.WriteLine($"Threads: {processStatus.Threads}");
        Console.WriteLine($"Memory (KB): {processStatus.MemoryKb}");
        Console.WriteLine($"Command Line: {processStatus.CommandLine}");
        Console.WriteLine($"Executable: {processStatus.Executable}");
        Console.WriteLine($"Working Directory: {processStatus.WorkingDirectory}");
    }

    public static void PrintRunResult(RunResult result)
    {
        Console.WriteLine($"PID: {result.Pid}");
        Console.WriteLine($"Start Time: {result.StartTime}");
        Console.WriteLine($"End Time: {result.EndTime}");
        Console.WriteLine($"Exit Code: {result.ExitCode}");
        Console.WriteLine("Standard Output:");
        Console.WriteLine(result.Stdout);
        Console.WriteLine("Standard Error:");
        Console.WriteLine(result.Stderr);
    }

    public static int[] ReadChildren(string filePath, int pid)
    {
        int[]? children = null;
        try
        {
            string content = File.ReadAllText(filePath).Trim();
            if (!string.IsNullOrEmpty(content))
            {
                children = content.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"Error reading children for PID {pid}: {e.Message}");
        }
        return children ?? Array.Empty<int>();
    }

    public static void PrintTree(int pid, Dictionary<int, int[]> processTree)
    {
        if (!processTree.ContainsKey(pid))
        {
            Console.WriteLine($"No process tree found for PID {pid}.");
            return;
        }

        Console.WriteLine($"Process Tree for PID {pid}:");
        PrintTreeRecursive(pid, 0, processTree);
    }
    public static void PrintTreeRecursive(int pid, int level, Dictionary<int, int[]> processTree)
    {
        Console.WriteLine($"{new string(' ', level * 2)}- PID: {pid}");
        if (!processTree.ContainsKey(pid))
            return;
        var children = processTree[pid];
        foreach (var childPid in children)
        {

            PrintTreeRecursive(childPid, level + 1, processTree);

        }
    }


}
