using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Net.Http.Headers;
using SharpChat.Api.Auth;
using SharpChat.Application.Auth;

namespace SharpChat.IntegrationTests.Auth;

internal sealed record Session(string AccessToken, string RefreshToken);

internal static class AuthApi
{
    public const string Password = "Secret123";
    public const string RefreshCookieName = "__Secure-sharpchat-refresh";

    public static RegisterRequest NewUser()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return new RegisterRequest($"{id}@example.com", $"user_{id}", Password);
    }

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, RegisterRequest request) =>
        client.PostAsJsonAsync("/auth/register", request);

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string? refreshToken) =>
        client.PostWithRefreshCookieAsync("/auth/refresh", refreshToken);

    public static Task<HttpResponseMessage> LogoutAsync(this HttpClient client, string? refreshToken) =>
        client.PostWithRefreshCookieAsync("/auth/logout", refreshToken);

    public static async Task<Session> RegisterAndLoginAsync(this HttpClient client, RegisterRequest? user = null)
    {
        user ??= NewUser();
        (await client.RegisterAsync(user)).EnsureSuccessStatusCode();

        var login = await client.LoginAsync(user.Email, user.Password);
        login.EnsureSuccessStatusCode();
        return await login.ReadSessionAsync();
    }

    public static async Task<Session> ReadSessionAsync(this HttpResponseMessage response)
    {
        var body = (await response.Content.ReadFromJsonAsync<SessionResponse>())!;
        return new Session(body.AccessToken, response.RefreshCookie()?.Value.Value ?? string.Empty);
    }

    public static SetCookieHeaderValue? RefreshCookie(this HttpResponseMessage response) =>
        response.Headers.TryGetValues(HeaderNames.SetCookie, out var values)
            ? SetCookieHeaderValue.ParseList(values.ToList()).SingleOrDefault(c => c.Name == RefreshCookieName)
            : null;

    public static Task<HttpResponseMessage> GetMeAsync(this HttpClient client, string? accessToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/users/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        return client.SendAsync(request);
    }

    public static async Task<string?> ErrorCodeAsync(this HttpResponseMessage response)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    private static Task<HttpResponseMessage> PostWithRefreshCookieAsync(this HttpClient client, string path, string? refreshToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        if (refreshToken is not null)
        {
            request.Headers.Add(HeaderNames.Cookie, $"{RefreshCookieName}={refreshToken}");
        }

        return client.SendAsync(request);
    }
}
