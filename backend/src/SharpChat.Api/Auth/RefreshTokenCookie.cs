namespace SharpChat.Api.Auth;

internal static class RefreshTokenCookie
{
    public const string Name = "__Secure-sharpchat-refresh";
    public const string Path = "/auth";

    public static void Append(HttpResponse response, string token, DateTimeOffset expiresAt) =>
        response.Cookies.Append(Name, token, Options(expiresAt));

    public static string Read(HttpRequest request) =>
        request.Cookies.TryGetValue(Name, out var token) ? token : string.Empty;

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(expiresAt: null));

    private static CookieOptions Options(DateTimeOffset? expiresAt) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = Path,
        Expires = expiresAt,
        IsEssential = true,
    };
}
