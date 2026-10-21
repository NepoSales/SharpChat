using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace SharpChat.Application.Messages;

public sealed record MessageCursor(long Seq)
{
    private const string Prefix = "s";

    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(string.Create(CultureInfo.InvariantCulture, $"{Prefix}{Seq}")));

    public static bool TryDecode(string? value, [NotNullWhen(true)] out MessageCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            var text = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value));
            if (text.StartsWith(Prefix, StringComparison.Ordinal)
                && long.TryParse(text.AsSpan(Prefix.Length), NumberStyles.None, CultureInfo.InvariantCulture, out var seq)
                && seq > 0)
            {
                cursor = new MessageCursor(seq);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}
