using System.Buffers.Binary;
using System.Text;

namespace EagleEye.Service.Tests.Monitoring;

/// <summary>Builds minimal PE32+ files and <c>VS_VERSIONINFO</c> blobs for the version-resource parser tests.</summary>
internal static class TestPeFiles
{
    public const int SectionRva = 0x1000;

    // Offsets inside the resource section built by ResourceSection.
    public const int TypeEntryId = 0x10;
    public const int TypeEntryTarget = 0x14;
    public const int NameEntryTarget = 0x2C;
    public const int LanguageEntryTarget = 0x44;
    public const int DataEntry = 0x48;
    public const int Data = 0x58;

    private const int FileAlignment = 0x200;

    /// <summary>A resource section with one RT_VERSION resource (name 1, language 0x409) containing <paramref name="data"/>.</summary>
    public static byte[] ResourceSection(byte[] data)
    {
        var section = new byte[Data + data.Length];
        Directory(section, 0x00, 1, 16, 0x8000_0000 | 0x18);       // type level → name directory
        Directory(section, 0x18, 1, 1, 0x8000_0000 | 0x30);        // name level → language directory
        Directory(section, 0x30, 1, 0x409, DataEntry);             // language level → data entry
        U32(section, DataEntry, (uint)(SectionRva + Data));
        U32(section, DataEntry + 4, (uint)data.Length);
        data.CopyTo(section, Data);
        return section;
    }

    /// <summary>A PE32+ file with one section (<c>.rsrc</c>) at <see cref="SectionRva"/>.</summary>
    public static byte[] Pe(byte[] section, int resourceDirectorySize = -1)
    {
        var rawSize = Align(Math.Max(section.Length, 1), FileAlignment);
        var file = new byte[FileAlignment + rawSize];
        file[0] = (byte)'M';
        file[1] = (byte)'Z';
        U32(file, 0x3C, 0x40);
        U32(file, 0x40, 0x0000_4550);                // "PE\0\0"
        var coff = 0x44;
        U16(file, coff, 0x8664);                      // AMD64
        U16(file, coff + 2, 1);                       // one section
        U16(file, coff + 16, 240);                    // size of optional header
        U16(file, coff + 18, 0x22);
        var optional = coff + 20;
        U16(file, optional, 0x20B);                   // PE32+
        U32(file, optional + 32, SectionRva);         // section alignment
        U32(file, optional + 36, FileAlignment);      // file alignment
        U32(file, optional + 56, (uint)(SectionRva + Align(rawSize, SectionRva))); // size of image
        U32(file, optional + 60, FileAlignment);      // size of headers
        U32(file, optional + 108, 16);                // number of data directories
        U32(file, optional + 112 + 16, SectionRva);   // resource directory RVA
        U32(file, optional + 112 + 20, (uint)(resourceDirectorySize < 0 ? section.Length : resourceDirectorySize));
        var header = optional + 240;
        Encoding.ASCII.GetBytes(".rsrc").CopyTo(file, header);
        U32(file, header + 8, (uint)section.Length);  // virtual size
        U32(file, header + 12, SectionRva);
        U32(file, header + 16, (uint)rawSize);
        U32(file, header + 20, FileAlignment);
        U32(file, header + 36, 0x4000_0040);
        section.CopyTo(file, FileAlignment);
        return file;
    }

    /// <summary>A <c>VS_VERSIONINFO</c> with a string table and, optionally, a translation.</summary>
    public static byte[] VersionInfo(string? description, string tableKey = "040904B0", (ushort Language, ushort CodePage)? translation = null)
    {
        var strings = new List<byte[]> { Block("CompanyName", Text("Contoso"), 1) };
        if (description is not null)
        {
            strings.Add(Block("FileDescription", Text(description), 1));
        }

        var children = new List<byte[]> { Block("StringFileInfo", [], 1, Block(tableKey, [], 1, [.. strings])) };
        if (translation is { } t)
        {
            var value = new byte[4];
            BinaryPrimitives.WriteUInt16LittleEndian(value, t.Language);
            BinaryPrimitives.WriteUInt16LittleEndian(value.AsSpan(2), t.CodePage);
            children.Insert(0, Block("VarFileInfo", [], 1, Block("Translation", value, 0)));
        }

        return Block("VS_VERSION_INFO", new byte[52], 0, [.. children]);
    }

    /// <summary>One version block: header, key, padding, value, padding, children (each padded to 4 bytes).</summary>
    public static byte[] Block(string key, byte[] value, int type, params byte[][] children)
    {
        var bytes = new List<byte>(new byte[6]);
        bytes.AddRange(Encoding.Unicode.GetBytes(key + "\0"));
        Pad(bytes);
        bytes.AddRange(value);
        foreach (var child in children)
        {
            Pad(bytes);
            bytes.AddRange(child);
        }

        var result = bytes.ToArray();
        BinaryPrimitives.WriteUInt16LittleEndian(result, (ushort)result.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(2), (ushort)(type == 1 ? value.Length / 2 : value.Length));
        BinaryPrimitives.WriteUInt16LittleEndian(result.AsSpan(4), (ushort)type);
        return result;
    }

    public static byte[] Text(string text) => Encoding.Unicode.GetBytes(text + "\0");

    public static void U16(byte[] target, int offset, ushort value) => BinaryPrimitives.WriteUInt16LittleEndian(target.AsSpan(offset), value);

    public static void U32(byte[] target, int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(target.AsSpan(offset), value);

    private static void Directory(byte[] section, int offset, int idEntries, uint id, uint target)
    {
        U16(section, offset + 14, (ushort)idEntries);
        U32(section, offset + 16, id);
        U32(section, offset + 20, target);
    }

    private static void Pad(List<byte> bytes)
    {
        while (bytes.Count % 4 != 0)
        {
            bytes.Add(0);
        }
    }

    private static int Align(int value, int alignment) => (value + alignment - 1) / alignment * alignment;
}
