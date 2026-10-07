using System.Buffers.Binary;
using System.Reflection.PortableExecutable;
using System.Text;

namespace EagleEye.Service.Monitoring;

/// <summary>
/// Reads the <c>FileDescription</c> of a program from its version resource without loading it (ADR-011 §7 items
/// 12 and 13, T-1): managed and bounds-checked, using <see cref="PEReader"/> for the headers and own checked
/// parsing for the resource directory and <c>VS_VERSIONINFO</c>. Only <c>RT_VERSION</c> is read, at most
/// <see cref="MaxResourceBytes"/>; files over <see cref="MaxFileBytes"/> are not read. Every malformed structure
/// gives <c>null</c>; nothing is thrown out.
/// </summary>
public static class VersionResourceReader
{
    /// <summary>Files larger than this are not read.</summary>
    public const long MaxFileBytes = 512L * 1024 * 1024;

    /// <summary>Largest version resource that is parsed.</summary>
    public const int MaxResourceBytes = 64 * 1024;

    private const uint RtVersion = 16;
    private const uint HighBit = 0x8000_0000;
    private const int DirectoryHeaderSize = 16;
    private const int DirectoryEntrySize = 8;

    /// <summary>Returns the file description of the first translation, or <c>null</c>.</summary>
    public static string? ReadFileDescription(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        try
        {
            if (!stream.CanSeek || stream.Length > MaxFileBytes)
            {
                return null;
            }

            using var pe = new PEReader(stream, PEStreamOptions.LeaveOpen);
            return FindVersionResource(pe) is { } resource ? ParseFileDescription(resource) : null;
        }
        catch (Exception)
        {
            // Parser boundary (coding guidelines §12.4): any malformed file means "unknown".
            return null;
        }
    }

    /// <summary>Finds the first <c>RT_VERSION</c> resource (first name, first language); null if there is none.</summary>
    internal static byte[]? FindVersionResource(PEReader pe)
    {
        var directory = pe.PEHeaders.PEHeader!.ResourceTableDirectory; // PEReader rejects files without an optional header.
        if (directory.Size == 0)
        {
            return null;
        }

        var section = pe.GetSectionData(directory.RelativeVirtualAddress).GetContent().AsSpan();
        var names = SubDirectory(section, 0, RtVersion);
        var languages = names is { } n ? SubDirectory(section, n, null) : null;
        var dataEntry = languages is { } l ? DataEntry(section, l) : null;
        if (dataEntry is not { } entry)
        {
            return null;
        }

        var reader = new SpanReader(section);
        var rva = reader.U32(entry);
        var size = reader.U32(entry + 4);
        if (!reader.Ok || size == 0 || size > MaxResourceBytes)
        {
            return null;
        }

        var data = pe.GetSectionData((int)rva).GetContent();
        return data.Length >= size ? data.AsSpan(0, (int)size).ToArray() : null;
    }

    /// <summary>Parses <c>VS_VERSIONINFO</c> and returns the <c>FileDescription</c> of the first translation.</summary>
    internal static string? ParseFileDescription(ReadOnlySpan<byte> info)
    {
        if (Block.Parse(info, 0, info.Length) is not { Key: "VS_VERSION_INFO" } root)
        {
            return null;
        }

        string? translation = null;
        var tables = new List<Block>();
        foreach (var child in root.Children(info))
        {
            if (child.Key == "VarFileInfo")
            {
                translation ??= Translation(info, child);
            }
            else if (child.Key == "StringFileInfo")
            {
                tables.AddRange(child.Children(info));
            }
        }

        var table = tables.FirstOrDefault(t => string.Equals(t.Key, translation, StringComparison.OrdinalIgnoreCase)) ?? tables.FirstOrDefault();
        var description = table?.Children(info).FirstOrDefault(s => s.Key == "FileDescription");
        return description?.Text(info);
    }

    private static string? Translation(ReadOnlySpan<byte> info, Block varFileInfo)
    {
        foreach (var variable in varFileInfo.Children(info))
        {
            var reader = new SpanReader(info);
            var language = reader.U16(variable.ValueOffset);
            var codePage = reader.U16(variable.ValueOffset + 2);
            if (variable.Key == "Translation" && variable.ValueLength >= 4 && reader.Ok)
            {
                return $"{language:X4}{codePage:X4}";
            }
        }

        return null;
    }

    /// <summary>The offset of the subdirectory of the first id entry (with the given id, if any).</summary>
    private static int? SubDirectory(ReadOnlySpan<byte> section, int offset, uint? requiredId)
    {
        foreach (var (id, target) in Entries(section, offset))
        {
            if ((id & HighBit) == 0 && (requiredId is null || id == requiredId) && (target & HighBit) != 0)
            {
                return (int)(target & ~HighBit);
            }
        }

        return null;
    }

    /// <summary>The offset of the data entry of the first entry that is not a subdirectory.</summary>
    private static int? DataEntry(ReadOnlySpan<byte> section, int offset)
    {
        foreach (var (_, target) in Entries(section, offset))
        {
            if ((target & HighBit) == 0)
            {
                return (int)target;
            }
        }

        return null;
    }

    private static List<(uint Id, uint Target)> Entries(ReadOnlySpan<byte> section, int offset)
    {
        var reader = new SpanReader(section);
        var count = reader.U16(offset + 12) + reader.U16(offset + 14);
        var entries = new List<(uint, uint)>();
        for (var i = 0; i < count && reader.Ok; i++)
        {
            var entry = offset + DirectoryHeaderSize + (i * DirectoryEntrySize);
            var id = reader.U32(entry);
            var target = reader.U32(entry + 4);
            if (reader.Ok)
            {
                entries.Add((id, target));
            }
        }

        return entries;
    }

    /// <summary>Bounds-checked little-endian reads; any read outside the data clears <see cref="Ok"/> and returns 0.</summary>
    private ref struct SpanReader(ReadOnlySpan<byte> data)
    {
        private readonly ReadOnlySpan<byte> _data = data;

        public bool Ok { get; private set; } = true;

        public ushort U16(int offset) => Fits(offset, 2) ? BinaryPrimitives.ReadUInt16LittleEndian(_data[offset..]) : (ushort)0;

        public uint U32(int offset) => Fits(offset, 4) ? BinaryPrimitives.ReadUInt32LittleEndian(_data[offset..]) : 0;

        private bool Fits(int offset, int length)
        {
            Ok &= offset >= 0 && offset <= _data.Length - length;
            return Ok;
        }
    }

    /// <summary>One block of <c>VS_VERSIONINFO</c>: length, value length, type, key, value, children.</summary>
    private sealed record Block(string Key, int ValueOffset, int ValueLength, int ChildrenOffset, int End)
    {
        public static Block? Parse(ReadOnlySpan<byte> info, int offset, int parentEnd)
        {
            var reader = new SpanReader(info);
            var length = reader.U16(offset);
            var valueLength = reader.U16(offset + 2);
            var type = reader.U16(offset + 4);
            var end = offset + length;
            if (!reader.Ok || length < 6 || end > parentEnd)
            {
                return null;
            }

            var keyStart = offset + 6;
            var keyEnd = keyStart;
            while (keyEnd + 1 < end && reader.U16(keyEnd) != 0)
            {
                keyEnd += 2;
            }

            if (keyEnd + 1 >= end)
            {
                return null;
            }

            var key = Encoding.Unicode.GetString(info[keyStart..keyEnd]);
            var valueOffset = Math.Min(Align(keyEnd + 2), end);
            var valueBytes = Math.Min(type == 1 ? valueLength * 2 : valueLength, Math.Max(0, end - valueOffset));
            return new Block(key, valueOffset, valueBytes, Math.Min(Align(valueOffset + valueBytes), end), end);
        }

        public List<Block> Children(ReadOnlySpan<byte> info)
        {
            var children = new List<Block>();
            var offset = ChildrenOffset;
            while (offset < End && Parse(info, offset, End) is { } child)
            {
                children.Add(child);
                offset = Align(child.End);
            }

            return children;
        }

        public string? Text(ReadOnlySpan<byte> info)
        {
            var value = info.Slice(ValueOffset, ValueLength & ~1);
            var text = Encoding.Unicode.GetString(value);
            var nul = text.IndexOf('\0');
            return nul >= 0 ? text[..nul] : text;
        }

        private static int Align(int offset) => (offset + 3) & ~3;
    }
}
