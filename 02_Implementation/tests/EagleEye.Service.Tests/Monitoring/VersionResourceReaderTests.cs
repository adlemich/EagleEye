using EagleEye.Service.Monitoring;
using Xunit;
using static EagleEye.Service.Tests.Monitoring.TestPeFiles;

namespace EagleEye.Service.Tests.Monitoring;

public sealed class VersionResourceReaderTests
{
    [Fact]
    public void ReadFileDescription_RealServiceAssembly_ReturnsDescription()
    {
        using var stream = File.OpenRead(typeof(ServicePaths).Assembly.Location);

        Assert.Equal("EagleEye.Service", VersionResourceReader.ReadFileDescription(stream));
    }

    [Fact]
    public void ReadFileDescription_SyntheticPe_ReturnsDescription()
    {
        Assert.Equal("Minecraft Launcher", Read(Pe(ResourceSection(VersionInfo("Minecraft Launcher")))));
    }

    [Fact]
    public void ReadFileDescription_NoResourceDirectory_IsNull()
    {
        Assert.Null(Read(Pe(ResourceSection(VersionInfo("x")), resourceDirectorySize: 0)));
    }

    [Theory]
    [InlineData(TypeEntryId, 17u)]                     // RT_VERSION missing (other type)
    [InlineData(TypeEntryId, 0x8000_0010u)]            // named entry
    [InlineData(TypeEntryTarget, 0x18u)]               // type entry is not a subdirectory
    [InlineData(NameEntryTarget, DataEntry)]           // name level without subdirectory
    [InlineData(LanguageEntryTarget, 0x8000_0048u)]    // language level without data entry
    [InlineData(DataEntry + 4, 0u)]                    // empty resource
    [InlineData(DataEntry + 4, 0x1_0001u)]             // larger than 64 KB
    [InlineData(DataEntry + 4, 0x1000u)]               // larger than the data in the section
    [InlineData(LanguageEntryTarget, 0x7000u)]         // data entry outside the section
    [InlineData(0x0E, 0x0FFFu)]                        // entry count beyond the section
    [InlineData(TypeEntryTarget, 0xFFFF_FFF8u)]        // offset overflows to a negative value
    public void ReadFileDescription_BrokenResourceDirectory_IsNull(int offset, uint value)
    {
        var section = ResourceSection(VersionInfo("x"));
        U32(section, offset, value);

        Assert.Null(Read(Pe(section)));
    }

    [Fact]
    public void ReadFileDescription_SubdirectoryOffsetOutsideSection_IsNull()
    {
        var section = ResourceSection(VersionInfo("x"));
        U32(section, TypeEntryTarget, 0x8000_7000);

        Assert.Null(Read(Pe(section)));
    }

    [Fact]
    public void ReadFileDescription_NotAPeFile_IsNull()
    {
        Assert.Null(Read("MZ this is not a program"u8.ToArray()));
    }

    [Fact]
    public void ReadFileDescription_NotSeekable_IsNull()
    {
        Assert.Null(VersionResourceReader.ReadFileDescription(new TestStream(Pe(ResourceSection(VersionInfo("x")))) { Seekable = false }));
    }

    [Fact]
    public void ReadFileDescription_TooLarge_IsNullWithoutReading()
    {
        Assert.Null(VersionResourceReader.ReadFileDescription(new TestStream([]) { FakeLength = VersionResourceReader.MaxFileBytes + 1 }));
    }

    [Fact]
    public void ReadFileDescription_Null_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => VersionResourceReader.ReadFileDescription(null!));
    }

    [Fact]
    public void ParseFileDescription_FirstTranslationWins()
    {
        var info = Block("VS_VERSION_INFO", new byte[52], 0,
            Block("VarFileInfo", [], 1, Block("Translation", [0x07, 0x04, 0xB0, 0x04], 0)),
            Block("StringFileInfo", [], 1,
                Block("040904B0", [], 1, Block("FileDescription", Text("Notepad"), 1)),
                Block("040704b0", [], 1, Block("FileDescription", Text("Editor"), 1))));

        Assert.Equal("Editor", VersionResourceReader.ParseFileDescription(info));
    }

    [Fact]
    public void ParseFileDescription_TranslationWithoutTable_FirstTable()
    {
        Assert.Equal("Notepad", VersionResourceReader.ParseFileDescription(VersionInfo("Notepad", translation: (0x0407, 0x04B0))));
    }

    [Fact]
    public void ParseFileDescription_NoDescription_IsNull()
    {
        Assert.Null(VersionResourceReader.ParseFileDescription(VersionInfo(null)));
    }

    [Fact]
    public void ParseFileDescription_NoStringFileInfo_IsNull()
    {
        Assert.Null(VersionResourceReader.ParseFileDescription(Block("VS_VERSION_INFO", new byte[52], 0, Block("Other", [], 1))));
    }

    [Fact]
    public void ParseFileDescription_WrongRootKey_IsNull()
    {
        Assert.Null(VersionResourceReader.ParseFileDescription(Block("VS_VERSION_INFX", new byte[52], 0)));
    }

    [Fact]
    public void ParseFileDescription_TranslationTooShortOrOtherVariable_Ignored()
    {
        var info = Block("VS_VERSION_INFO", new byte[52], 0,
            Block("VarFileInfo", [], 1, Block("Other", [0x07, 0x04, 0xB0, 0x04], 0), Block("Translation", [0x07, 0x04], 0)),
            Block("StringFileInfo", [], 1, Block("040904B0", [], 1, Block("FileDescription", Text("Notepad"), 1))));

        Assert.Equal("Notepad", VersionResourceReader.ParseFileDescription(info));
    }

    [Fact]
    public void ParseFileDescription_ValueWithoutTerminator_IsWholeValue()
    {
        var info = Block("VS_VERSION_INFO", new byte[52], 0,
            Block("StringFileInfo", [], 1, Block("040904B0", [], 1, Block("FileDescription", System.Text.Encoding.Unicode.GetBytes("Paint"), 1))));

        Assert.Equal("Paint", VersionResourceReader.ParseFileDescription(info));
    }

    [Fact]
    public void ParseFileDescription_ValueLengthBeyondBlock_IsClamped()
    {
        var info = VersionInfo("Paint");
        var description = Block("FileDescription", Text("Paint"), 1);
        var at = IndexOf(info, description);
        U16(info, at + 2, 500);

        Assert.Equal("Paint", VersionResourceReader.ParseFileDescription(info));
    }

    [Theory]
    [InlineData(0, 4)]           // block shorter than its header
    [InlineData(0, 0xFFFF)]      // block longer than the data
    public void ParseFileDescription_BrokenRootLength_IsNull(int offset, int length)
    {
        var info = VersionInfo("x");
        U16(info, offset, (ushort)length);

        Assert.Null(VersionResourceReader.ParseFileDescription(info));
    }

    [Fact]
    public void ParseFileDescription_KeyWithoutTerminator_IsNull()
    {
        var info = new byte[] { 10, 0, 0, 0, 0, 0, 0x41, 0, 0x42, 0 };

        Assert.Null(VersionResourceReader.ParseFileDescription(info));
    }

    [Fact]
    public void ParseFileDescription_TooShortForHeader_IsNull()
    {
        Assert.Null(VersionResourceReader.ParseFileDescription(new byte[] { 1 }));
    }

    [Fact]
    public void ParseFileDescription_BrokenChild_StopsAtIt()
    {
        var info = VersionInfo("Paint");
        var table = IndexOf(info, Block("CompanyName", Text("Contoso"), 1));
        U16(info, table, 2);

        Assert.Null(VersionResourceReader.ParseFileDescription(info));
    }

    private static string? Read(byte[] file) => VersionResourceReader.ReadFileDescription(new MemoryStream(file));

    private static int IndexOf(byte[] data, byte[] part)
    {
        for (var i = 0; i <= data.Length - part.Length; i++)
        {
            if (data.AsSpan(i, part.Length).SequenceEqual(part))
            {
                return i;
            }
        }

        throw new InvalidOperationException("Part not found.");
    }

    private sealed class TestStream(byte[] data) : MemoryStream(data)
    {
        public bool Seekable { get; init; } = true;

        public long? FakeLength { get; init; }

        public override bool CanSeek => Seekable;

        public override long Length => FakeLength ?? base.Length;
    }
}
