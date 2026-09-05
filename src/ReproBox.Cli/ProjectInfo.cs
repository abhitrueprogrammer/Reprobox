internal record ProcessInfo(
    string? Name,
    int? Pid,
    int? ParentPid,
    string? CommandLine,
    string? Executable,
    string? WorkingDirectory,
    string? UserId,
    int? Threads,
    long? MemoryKb
);

