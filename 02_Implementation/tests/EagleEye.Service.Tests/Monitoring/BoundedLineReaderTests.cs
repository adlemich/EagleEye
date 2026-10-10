using System.Text;
using EagleEye.Service.Monitoring;
using Xunit;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class BoundedLineReaderTests
{
    [Fact]
    public async Task ReadLineAsync_LinesWithLfAndCrLf_ThenNullAtEnd()
    {
        var reader = Reader("one\ntwo\r\n\nthree");

        string?[] actual = [await Read(reader), await Read(reader), await Read(reader), await Read(reader), await Read(reader)];

        Assert.Equal<IEnumerable<string?>>(["one", "two", "", "three", null], actual);
    }

    [Fact]
    public async Task ReadLineAsync_EmptyStream_ReturnsNull()
    {
        Assert.Null(await Read(Reader("")));
    }

    [Fact]
    public async Task ReadLineAsync_LineAcrossBufferBoundaries_IsComplete()
    {
        var line = new string('x', 10_000);

        Assert.Equal(line, await Read(Reader(line + "\n")));
    }

    [Fact]
    public async Task ReadLineAsync_ExactlyAtLimit_IsAccepted()
    {
        var reader = new BoundedLineReader(new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 100) + "\n")), 100);

        Assert.Equal(100, (await reader.ReadLineAsync(CancellationToken.None))!.Length);
    }

    [Fact]
    public async Task ReadLineAsync_TooLong_ThrowsProtocolException()
    {
        var reader = new BoundedLineReader(new MemoryStream(Encoding.UTF8.GetBytes(new string('x', 101) + "\n")), 100);

        await Assert.ThrowsAsync<AgentProtocolException>(() => reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadLineAsync_DefaultLimitIs64Kilobytes()
    {
        var reader = new BoundedLineReader(new MemoryStream(Encoding.UTF8.GetBytes(new string('x', BoundedLineReader.MaxLineBytes + 1))));

        await Assert.ThrowsAsync<AgentProtocolException>(() => reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadLineAsync_InvalidUtf8_ThrowsProtocolException()
    {
        var reader = new BoundedLineReader(new MemoryStream([0x61, 0xFF, 0xFE, 0x0A]));

        await Assert.ThrowsAsync<AgentProtocolException>(() => reader.ReadLineAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ReadLineAsync_Utf8_IsDecoded()
    {
        Assert.Equal("Ärger", await Read(Reader("Ärger\n")));
    }

    private static BoundedLineReader Reader(string text) => new(new MemoryStream(Encoding.UTF8.GetBytes(text)));

    private static Task<string?> Read(BoundedLineReader reader) => reader.ReadLineAsync(CancellationToken.None);
}
