using System.Text;

namespace EagleEye.Service.Monitoring;

/// <summary>A line on the agent channel violated the protocol (too long, not UTF-8).</summary>
public sealed class AgentProtocolException(string message) : IOException(message);

/// <summary>
/// Reads UTF-8 lines from an agent stream with a length limit (ADR-011 §7 item 11, T-8): a line longer than
/// <see cref="MaxLineBytes"/> raises <see cref="AgentProtocolException"/>, so the reader never buffers more.
/// <c>\r\n</c> and <c>\n</c> end a line.
/// </summary>
public sealed class BoundedLineReader(Stream stream, int maxLineBytes = BoundedLineReader.MaxLineBytes)
{
    /// <summary>Default limit of one line (64 KB).</summary>
    public const int MaxLineBytes = 64 * 1024;

    private static readonly UTF8Encoding StrictUtf8 = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);

    private readonly byte[] _buffer = new byte[4096];
    private readonly MemoryStream _line = new();
    private int _start;
    private int _end;

    /// <summary>Returns the next line, or <c>null</c> at the end of the stream (a last line without break is returned).</summary>
    /// <exception cref="AgentProtocolException">The line is too long or not valid UTF-8.</exception>
    public async Task<string?> ReadLineAsync(CancellationToken ct)
    {
        while (true)
        {
            if (_start == _end)
            {
                _start = 0;
                _end = await stream.ReadAsync(_buffer, ct).ConfigureAwait(false);
                if (_end == 0)
                {
                    return _line.Length > 0 ? TakeLine() : null;
                }
            }

            var newline = Array.IndexOf(_buffer, (byte)'\n', _start, _end - _start);
            var take = (newline < 0 ? _end : newline) - _start;
            if (_line.Length + take > maxLineBytes)
            {
                throw new AgentProtocolException($"A line of the session agent is longer than {maxLineBytes} bytes.");
            }

            _line.Write(_buffer, _start, take);
            _start += take;
            if (newline >= 0)
            {
                _start++;
                return TakeLine();
            }
        }
    }

    private string TakeLine()
    {
        var bytes = _line.ToArray();
        _line.SetLength(0);
        var length = bytes.Length > 0 && bytes[^1] == (byte)'\r' ? bytes.Length - 1 : bytes.Length;
        try
        {
            return StrictUtf8.GetString(bytes, 0, length);
        }
        catch (DecoderFallbackException)
        {
            throw new AgentProtocolException("A line of the session agent is not valid UTF-8.");
        }
    }
}
