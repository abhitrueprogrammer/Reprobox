public enum FileDescriptorType
{
    File,
    Socket,
    Pipe,
    Directory,
    Other
}

public record FileDescriptor(
    int Number,
    string Target,
    FileDescriptorType Type
);