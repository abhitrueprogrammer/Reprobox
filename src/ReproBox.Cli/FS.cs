class FS
{
    private static FileDescriptorType DetermineFileDescriptorType(string? target)
    {
        if (target == null)
            return FileDescriptorType.Other;

        if (target.StartsWith("socket:"))
            return FileDescriptorType.Socket;

        if (target.StartsWith("pipe:"))
            return FileDescriptorType.Pipe;

        if (Directory.Exists(target))
            return FileDescriptorType.Directory;

        if (File.Exists(target))
            return FileDescriptorType.File;

        return FileDescriptorType.Other;
    }

    private static IEnumerable<FileDescriptor> DiscoverFileDescriptors(int pid)
    {
        var fdPath = $"/proc/{pid}/fd";

        foreach (var path in Directory.EnumerateFileSystemEntries(fdPath))
        {
            var target = File.ResolveLinkTarget(path, false);
            var targetPath = target?.FullName;

            yield return new FileDescriptor(
                Number: int.Parse(Path.GetFileName(path)),
                Target: targetPath ?? "Unknown",
                Type: DetermineFileDescriptorType(targetPath)
            );
        }
    }

    public static void PrintFileDescriptors(int pid)
    {
        foreach (var fd in DiscoverFileDescriptors(pid))
        {
            Console.WriteLine(
                $"FD: {fd.Number}, Target: {fd.Target}, Type: {fd.Type}"
            );
        }
    }
}