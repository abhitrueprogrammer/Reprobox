internal class Program
{
    internal static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] is "--help" or "-h")
        {
            Console.WriteLine("Usage: reprobox inspect <PID>");
            return 0;
        }
        if (args.Length == 0 || args.Length > 2 || args[0] is not ("inspect" or "--inspect"))
        {
            Console.Error.WriteLine("Usage: reprobox inspect <PID>");
            return 2;
        }

        int pid;
        if (args.Length == 2)
        {
            if (!IO.TryParsePid(args[1], out pid))
            {
                Console.Error.WriteLine("PID must be a positive integer.");
                return 2;
            }
        }
        else
        {
            int? requestedPid = IO.AskPid();
            if (requestedPid is null)
            {
                Console.Error.WriteLine("No PID supplied: input closed.");
                return 2;
            }
            pid = requestedPid.Value;
        }

        ProcessInfo processStatus = ReadProc.ReadProcessInfo(pid);
        if (processStatus.Pid is null)
            return 1;
        IO.printProcessStatus(processStatus);
        return 0;
    }
}
