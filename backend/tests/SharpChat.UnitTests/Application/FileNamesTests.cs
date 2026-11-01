using SharpChat.Application.Attachments;

namespace SharpChat.UnitTests.Application;

public sealed class FileNamesTests
{
    [Theory]
    [InlineData("report.pdf", "report.pdf")]
    [InlineData("../../etc/passwd", "passwd")]
    [InlineData(@"C:\Users\me\shot.png", "shot.png")]
    [InlineData("na\u202Egpj.exe", "nagpj.exe")]
    [InlineData("bad\"name<>.txt", "badname.txt")]
    [InlineData("trailing. . ", "trailing")]
    [InlineData(".gitignore", ".gitignore")]
    [InlineData("", "file")]
    [InlineData(null, "file")]
    [InlineData("...", "file")]
    public void Names_are_reduced_to_a_safe_display_name(string? input, string expected)
    {
        Assert.Equal(expected, FileNames.Sanitize(input));
    }

    [Fact]
    public void Long_names_are_cut_keeping_the_extension()
    {
        var name = FileNames.Sanitize(new string('a', 300) + ".tar.gz");

        Assert.Equal(FileNames.MaxLength, name.Length);
        Assert.EndsWith(".gz", name, StringComparison.Ordinal);
    }
}
