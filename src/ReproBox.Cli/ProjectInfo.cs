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
)
{
    public string? State { get; init; }
    public IReadOnlyList<string>? Environment { get; init; }
}
