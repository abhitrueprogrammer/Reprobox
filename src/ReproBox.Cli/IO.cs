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
    public static void printProcessStatus(ProcessInfo processStatus)
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

}
