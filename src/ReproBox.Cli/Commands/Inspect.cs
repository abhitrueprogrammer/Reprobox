using System.CommandLine;

internal static class InspectCommand
{
    public static Command Create()
    {
        var pidArgument = new Argument<int>("pid")
        {
            Description = "PID of the process to inspect"
        };

        var fdsOption = new Option<bool>("--fds", "-f")
        {
            Description = "Show open file descriptors"
        };

        var command = new Command(
            "inspect",
            "Inspect a running process");

        command.Arguments.Add(pidArgument);

        command.Options.Add(fdsOption);

        command.SetAction(parseResult =>
        {
            int pid = parseResult.GetValue(pidArgument);
            bool showFds = parseResult.GetValue(fdsOption);

            return Execute(
                pid,
                showFds);
        });

        return command;
    }

    private static int Execute(
        int pid,
        bool showFds
        )
    {
        ProcessInfo processInfo = ReadProc.ReadProcessInfo(pid);

        if (processInfo.Pid is null)
            return 1;

        IO.PrintProcessStatus(processInfo);

        if (showFds)
            FS.PrintFileDescriptors(pid);

        return 0;
    }
}