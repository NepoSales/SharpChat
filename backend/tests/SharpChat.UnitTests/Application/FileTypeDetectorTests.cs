using System.Text;
using SharpChat.Application.Attachments;
using SharpChat.Domain.Attachments;

namespace SharpChat.UnitTests.Application;

public sealed class FileTypeDetectorTests
{
    public static TheoryData<byte[], string, string> Images => new()
    {
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "a.png", "image/png" },
        { [0xFF, 0xD8, 0xFF, 0xE0, 0x00], "a.jpg", "image/jpeg" },
        { "GIF89a\u0001\u0000"u8.ToArray(), "a.gif", "image/gif" },
        { "RIFF\u0000\u0000\u0000\u0000WEBPVP8 "u8.ToArray(), "a.webp", "image/webp" },
        { [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00], "no-extension", "image/png" },
    };

    [Theory]
    [MemberData(nameof(Images))]
    public void Images_are_detected_by_content_not_by_name(byte[] header, string fileName, string expected)
    {
        var type = FileTypeDetector.Detect(header, fileName, isWholeFile: true);

        Assert.Equal((expected, AttachmentKind.Image), (type?.ContentType, type?.Kind));
    }

    [Fact]
    public void Pdf_and_zip_are_files()
    {
        var pdf = FileTypeDetector.Detect("%PDF-1.7"u8, "doc.pdf", isWholeFile: true);
        var zip = FileTypeDetector.Detect([0x50, 0x4B, 0x03, 0x04, 0x14], "src.zip", isWholeFile: true);

        Assert.Equal(("application/pdf", AttachmentKind.File), (pdf?.ContentType, pdf?.Kind));
        Assert.Equal(("application/zip", AttachmentKind.File), (zip?.ContentType, zip?.Kind));
    }

    [Theory]
    [InlineData("build.log")]
    [InlineData("README.MD")]
    [InlineData("Program.cs")]
    [InlineData("Dockerfile")]
    [InlineData(".gitignore")]
    public void Utf8_text_with_a_known_extension_is_plain_text(string fileName)
    {
        var type = FileTypeDetector.Detect(Encoding.UTF8.GetBytes("olá, mundo\n"), fileName, isWholeFile: true);

        Assert.Equal(FileTypeDetector.TextContentType, type?.ContentType);
    }

    [Theory]
    [InlineData("page.html", "<html></html>")]
    [InlineData("logo.svg", "<svg></svg>")]
    [InlineData("script.bat", "echo hi")]
    [InlineData("noext", "plain words")]
    public void Text_with_other_extensions_is_rejected(string fileName, string content)
    {
        Assert.Null(FileTypeDetector.Detect(Encoding.UTF8.GetBytes(content), fileName, isWholeFile: true));
    }

    [Fact]
    public void Binary_content_is_not_text_even_with_a_text_extension()
    {
        Assert.Null(FileTypeDetector.Detect([0x4D, 0x5A, 0x90, 0x00], "notes.txt", isWholeFile: true));
        Assert.Null(FileTypeDetector.Detect([0xC3, 0x28], "notes.txt", isWholeFile: true));
    }

    [Fact]
    public void A_zip_signature_needs_the_zip_extension()
    {
        Assert.Null(FileTypeDetector.Detect([0x50, 0x4B, 0x03, 0x04], "report.docx", isWholeFile: true));
    }

    [Fact]
    public void A_multibyte_character_cut_at_the_end_of_the_header_is_still_text()
    {
        var bytes = Encoding.UTF8.GetBytes("açã");

        Assert.NotNull(FileTypeDetector.Detect(bytes.AsSpan(0, bytes.Length - 1), "a.txt", isWholeFile: false));
        Assert.Null(FileTypeDetector.Detect(bytes.AsSpan(0, bytes.Length - 1), "a.txt", isWholeFile: true));
    }
}
