using System.Diagnostics;

class PollingObserver
{
    private readonly TimeSpan _pollingInterval = TimeSpan.FromMilliseconds(10);
    Dictionary<int, int[]> _processes = new();

    public PollingObserver(int pid)
    {

        var children = ReadProc.GetChildren(pid);

        _processes[pid] = children;
    }
    public async Task<Dictionary<int, int[]>> ObserveAsync(CancellationToken cancellationToken)
    {
        PeriodicTimer timer = new(_pollingInterval);

        while (true)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            UpdateProcesses();

            try
            {
                if (!await timer.WaitForNextTickAsync(cancellationToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                UpdateProcesses();

                break;
            }

        }

        return _processes;
    }
    public void UpdateProcesses()
    {
        foreach (var pid in _processes.Keys.ToList())
        {

            var currentChildren = ReadProc.GetChildren(pid);
            if (currentChildren is null)
            {
                continue;
            }

            var previousChildren = _processes[pid];

            var newChildren = currentChildren.Except(previousChildren).ToArray();
            if (newChildren.Length > 0)
            {
                foreach (var newChild in newChildren)
                {
                    _processes[newChild] = [];
                }
            }

            _processes[pid] = currentChildren;

        }
    }
}


// - `PeriodicTimer`, `Stopwatch`, async loops with cancellation.
// - `IAsyncEnumerable<ResourceSample>` if streaming samples genuinely helps; a simple callback or accumulator is fine if it does not.
// - Value types with units instead of raw `long`s.
// - Incremental statistics (peak, average) without unbounded memory.
