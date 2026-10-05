class PollingObserver
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(10);
    Dictionary<int, int[]> _processes = new();

    public PollingObserver(int pid)
    {
        var processDirectory = $"/proc/{pid}/task/";

        var children = ReadProc.GetChildren(pid);

        _processes[pid] = children;
    }
    public void Observer()
    {
        var timer = new PeriodicTimer(_pollingInterval);
        while (true)
        {
            foreach (var pid in _processes.Keys.ToList())
            {
                


            }
        }
    }
}

// - `PeriodicTimer`, `Stopwatch`, async loops with cancellation.
// - `IAsyncEnumerable<ResourceSample>` if streaming samples genuinely helps; a simple callback or accumulator is fine if it does not.
// - Value types with units instead of raw `long`s.
// - Incremental statistics (peak, average) without unbounded memory.
