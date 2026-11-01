using System.Text;

namespace SharpChat.Application.Attachments;

public static class FileNames
{
    public const int MaxLength = 200;

    private const string Fallback = "file";

    public static string Sanitize(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? string.Empty).Replace('\\', '/'));
        var builder = new StringBuilder(name.Length);
        foreach (var c in name.Normalize(NormalizationForm.FormC))
        {
            if (!char.IsControl(c) && c is not ('/' or '\\' or '"' or '<' or '>' or ':' or '|' or '?' or '*')
                && char.GetUnicodeCategory(c) is not System.Globalization.UnicodeCategory.Format)
            {
                builder.Append(c);
            }
        }

        var clean = builder.ToString().Trim().TrimEnd('.', ' ');
        if (clean.Length == 0)
        {
            return Fallback;
        }

        if (clean.Length <= MaxLength)
        {
            return clean;
        }

        var extension = Path.GetExtension(clean);
        if (extension.Length >= 20)
        {
            extension = string.Empty;
        }

        var stem = clean[..(MaxLength - extension.Length)];
        if (char.IsHighSurrogate(stem[^1]))
        {
            stem = stem[..^1];
        }

        return stem + extension;
    }
}
