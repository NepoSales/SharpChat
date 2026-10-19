using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text;

namespace SharpChat.Application.Messages;

public sealed record MessageCursor(DateTimeOffset SentAt, Guid Id)
{
    public string Encode() =>
        Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{SentAt.UtcTicks}_{Id:N}")));

    public static bool TryDecode(string? value, [NotNullWhen(true)] out MessageCursor? cursor)
    {
        cursor = null;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value)).Split('_');
            if (parts.Length == 2
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks is >= 0 and <= 3155378975999999999
                && Guid.TryParseExact(parts[1], "N", out var id))
            {
                cursor = new MessageCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
                return true;
            }
        }
        catch (FormatException)
        {
        }

        return false;
    }
}
