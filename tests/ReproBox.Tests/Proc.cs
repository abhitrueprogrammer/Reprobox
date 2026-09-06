namespace ReproBox.Tests;

public class UnitTestReadProc
{
    [Fact]
    public void ParseOneLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Name: test");

        // Assert
        Assert.Equal("test", result.Name);
    }
    [Fact]
    public void ParsePidLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Pid: 1234");

        // Assert
        Assert.Equal(1234, result.Pid);
    }
    [Fact]
    public void ParseParentPidLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "PPid: 5678");

        // Assert
        Assert.Equal(5678, result.ParentPid);
    }
    [Fact]
    public void ParseThreadsLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Threads: 10");

        // Assert
        Assert.Equal(10, result.Threads);
    }
    [Fact]
    public void ParseMemoryLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "VmRSS: 1024 kB");

        // Assert
        Assert.Equal(1024, result.MemoryKb);
    }
    [Fact]
    public void ParseUidLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Uid: 1000");

        // Assert
        Assert.NotNull(result.UserId);
    }
    [Fact]
    public void ParseNameLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Name: test");

        // Assert
        Assert.Equal("test", result.Name);
    }
    [Fact]
    public void ParseEmptyLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "");

        // Assert
        Assert.Null(result.Name);
    }
    
    [Fact]
    public void ParseInvalidLine()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Invalid line");

        // Assert
        Assert.Null(result.Name);
    }
    [Fact]
    public void ParseAllLines()
    {
        // Arrange
        ProcessInfo status = new(null, null, null, null, null, null, null, null, null);

        // Act
        ProcessInfo result = ReadProc.ParseLine(status, "Name: test");
        result = ReadProc.ParseLine(result, "Pid: 1234");
        result = ReadProc.ParseLine(result, "PPid: 5678");
        result = ReadProc.ParseLine(result, "Threads: 10");
        result = ReadProc.ParseLine(result, "VmRSS: 1024 kB");
        result = ReadProc.ParseLine(result, "Uid: 1000");

        // Assert
        Assert.Equal("test", result.Name);
        Assert.Equal(1234, result.Pid);
        Assert.Equal(5678, result.ParentPid);
        Assert.Equal(10, result.Threads);
        Assert.Equal(1024, result.MemoryKb);
        Assert.NotNull(result.UserId);
    }
}
