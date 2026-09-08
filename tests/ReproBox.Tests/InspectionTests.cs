namespace ReproBox.Tests;

public class InspectionTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("abc")]
    [InlineData("2147483648")]
    public void RejectsInvalidPid(string? input) => Assert.False(IO.TryParsePid(input, out _));

    [Theory]
    [InlineData("Uid:\t1000\t1001\t1002\t1003")]
    [InlineData("Uid: 1000 1001 1002 1003")]
    public void ReadsRealUid(string line)
    {
        ProcessInfo empty = new(null, null, null, null, null, null, null, null, null);
        Assert.Equal(1000u, ReadProc.ParseLine(empty, line).UserId);
    }

}
