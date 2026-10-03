public enum RunStatus
{
    Exited,
    Failed,
    Cancelled
}

public sealed record RunResult(
    int? Pid,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int ExitCode,
    string Stdout,
    string Stderr,
    RunStatus Status
);