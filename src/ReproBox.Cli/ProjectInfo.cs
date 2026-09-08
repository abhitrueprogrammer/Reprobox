internal record ProcessInfo(
    string? Name,
    int? Pid,
    int? ParentPid,
    string? CommandLine,
    string? Executable,
    string? WorkingDirectory,
    uint? UserId,
    int? Threads,
    long? MemoryKb
);
