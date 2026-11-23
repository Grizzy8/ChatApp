using ChatShared;

namespace ChatShared.Test;

public class BoundedLineReaderTests
{
    private static BoundedLineReader CreateReader(string content, int maxLength = 100) =>
        new(new StringReader(content), maxLength);

    [Fact]
    public async Task ReadLineAsync_ReturnsLinesInOrder()
    {
        var reader = CreateReader("bir\niki\nüç\n");

        Assert.Equal("bir", await reader.ReadLineAsync());
        Assert.Equal("iki", await reader.ReadLineAsync());
        Assert.Equal("üç", await reader.ReadLineAsync());
        Assert.Null(await reader.ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_StripsCarriageReturn()
    {
        var reader = CreateReader("satır\r\nsonraki\r\n");

        Assert.Equal("satır", await reader.ReadLineAsync());
        Assert.Equal("sonraki", await reader.ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsEmptyString_ForBlankLine()
    {
        var reader = CreateReader("\nx\n");

        Assert.Equal("", await reader.ReadLineAsync());
        Assert.Equal("x", await reader.ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsNull_WhenStreamEndsWithoutNewline()
    {
        // Bağlantı satırın ortasında koptu: yarım satır teslim edilmez.
        var reader = CreateReader("tamam\nyarım");

        Assert.Equal("tamam", await reader.ReadLineAsync());
        Assert.Null(await reader.ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_ReturnsNull_ForEmptyStream()
    {
        Assert.Null(await CreateReader("").ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_AcceptsLineOfExactlyMaxLength()
    {
        var reader = CreateReader(new string('a', 10) + "\n", maxLength: 10);

        Assert.Equal(new string('a', 10), await reader.ReadLineAsync());
    }

    [Fact]
    public async Task ReadLineAsync_Throws_WhenLineExceedsMaxLength()
    {
        var reader = CreateReader(new string('a', 11) + "\n", maxLength: 10);

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadLineAsync().AsTask());
    }

    [Fact]
    public async Task ReadLineAsync_Throws_WhenNoNewlineAndTooMuchData()
    {
        // Satır sonu hiç gelmese bile bellek sınırsız büyümemeli.
        var reader = CreateReader(new string('a', 5000), maxLength: 100);

        await Assert.ThrowsAsync<InvalidDataException>(() => reader.ReadLineAsync().AsTask());
    }

    [Fact]
    public async Task ReadLineAsync_HandlesLinesLongerThanInternalBuffer()
    {
        var longLine = new string('x', 3000);
        var reader = CreateReader(longLine + "\nson\n", maxLength: 4000);

        Assert.Equal(longLine, await reader.ReadLineAsync());
        Assert.Equal("son", await reader.ReadLineAsync());
    }

    [Fact]
    public void Constructor_Throws_ForNonPositiveMaxLength()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => CreateReader("x", maxLength: 0));
    }
}
