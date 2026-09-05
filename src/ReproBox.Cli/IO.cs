internal class IO
{
    public static int askPID()
    {
        Console.Write("Please enter a PID: ");
        string input = Console.ReadLine();
        int number = int.Parse(input);
        return number;
    }
    public static string? ReadLink(string path)
    {
        try
        {
            return File.ResolveLinkTarget(path, returnFinalTarget: false)?.FullName;
        }
        catch (Exception e)
        {
            Console.WriteLine($"Error reading link {path}: {e.Message}");
            return null;
        }
    }
    public static void printProcessStatus(ProcessInfo processStatus)
    {
        Console.WriteLine($"Process Name: {processStatus.Name}");
        Console.WriteLine($"PID: {processStatus.Pid}");
        Console.WriteLine($"Parent PID: {processStatus.ParentPid}");
        Console.WriteLine($"User ID: {processStatus.UserId}");
        Console.WriteLine($"Threads: {processStatus.Threads}");
        Console.WriteLine($"Memory (KB): {processStatus.MemoryKb}");
        Console.WriteLine($"Command Line: {processStatus.CommandLine}");
        Console.WriteLine($"Executable: {processStatus.Executable}");
        Console.WriteLine($"Working Directory: {processStatus.WorkingDirectory}");
    }

}