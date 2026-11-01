using System.Buffers;
using System.Text.Unicode;
using SharpChat.Domain.Attachments;

namespace SharpChat.Application.Attachments;

public sealed record DetectedFileType(string ContentType, AttachmentKind Kind);

public static class FileTypeDetector
{
    public const int HeaderSize = 8192;

    public const string TextContentType = "text/plain; charset=utf-8";

    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".log", ".csv", ".tsv", ".json", ".yaml", ".yml", ".toml", ".ini", ".sql",
        ".cs", ".csproj", ".sln", ".props", ".ts", ".tsx", ".js", ".jsx", ".mjs", ".cjs", ".css", ".scss",
        ".py", ".go", ".rs", ".java", ".kt", ".c", ".h", ".cpp", ".hpp", ".rb", ".php", ".swift",
        ".sh", ".bash", ".ps1", ".diff", ".patch", ".dockerfile", ".gitignore", ".editorconfig",
    };

    private static ReadOnlySpan<byte> Png => [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    private static ReadOnlySpan<byte> Jpeg => [0xFF, 0xD8, 0xFF];

    private static ReadOnlySpan<byte> Gif87 => "GIF87a"u8;

    private static ReadOnlySpan<byte> Gif89 => "GIF89a"u8;

    private static ReadOnlySpan<byte> Riff => "RIFF"u8;

    private static ReadOnlySpan<byte> Webp => "WEBP"u8;

    private static ReadOnlySpan<byte> Pdf => "%PDF-"u8;

    private static ReadOnlySpan<byte> ZipEntry => [0x50, 0x4B, 0x03, 0x04];

    private static ReadOnlySpan<byte> ZipEmpty => [0x50, 0x4B, 0x05, 0x06];

    public static DetectedFileType? Detect(ReadOnlySpan<byte> header, string fileName, bool isWholeFile)
    {
        if (header.StartsWith(Png))
        {
            return new("image/png", AttachmentKind.Image);
        }

        if (header.StartsWith(Jpeg))
        {
            return new("image/jpeg", AttachmentKind.Image);
        }

        if (header.StartsWith(Gif87) || header.StartsWith(Gif89))
        {
            return new("image/gif", AttachmentKind.Image);
        }

        if (header.Length >= 12 && header.StartsWith(Riff) && header[8..12].SequenceEqual(Webp))
        {
            return new("image/webp", AttachmentKind.Image);
        }

        if (header.StartsWith(Pdf))
        {
            return new("application/pdf", AttachmentKind.File);
        }

        var extension = Extension(fileName);

        if ((header.StartsWith(ZipEntry) || header.StartsWith(ZipEmpty)) && extension == ".zip")
        {
            return new("application/zip", AttachmentKind.File);
        }

        if (TextExtensions.Contains(extension) && IsUtf8Text(header, isWholeFile))
        {
            return new(TextContentType, AttachmentKind.File);
        }

        return null;
    }

    private static string Extension(string fileName)
    {
        var name = Path.GetFileName(fileName);
        if (name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase))
        {
            return ".dockerfile";
        }

        return name.StartsWith('.') && !name[1..].Contains('.')
            ? name.ToLowerInvariant()
            : Path.GetExtension(name).ToLowerInvariant();
    }

    private static bool IsUtf8Text(ReadOnlySpan<byte> header, bool isWholeFile)
    {
        if (header.Contains((byte)0))
        {
            return false;
        }

        var chars = ArrayPool<char>.Shared.Rent(header.Length);
        try
        {
            var status = Utf8.ToUtf16(
                header, chars, out _, out _, replaceInvalidSequences: false, isFinalBlock: isWholeFile);
            return status is OperationStatus.Done || (status is OperationStatus.NeedMoreData && !isWholeFile);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(chars);
        }
    }
}
