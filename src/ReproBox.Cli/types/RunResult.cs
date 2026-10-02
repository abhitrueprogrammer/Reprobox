public sealed record RunResult(
    int Pid,
    DateTimeOffset StartTime,
    DateTimeOffset EndTime,
    int ExitCode,
    string Stdout,
    string Stderr);