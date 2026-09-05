
internal class Program
{


    private static void Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--inspect")
        {
            var pid = args.Length > 1 ? int.Parse(args[1]) : IO.askPID();
            ProcessInfo processStatus = ReadProc.ReadProcessInfo(pid);
            IO.printProcessStatus(processStatus);
        }
    }
}