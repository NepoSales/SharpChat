using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SharpChat.Application.Auth;

namespace SharpChat.IntegrationTests.Auth;

/// <summary>Small helpers over the auth endpoints, so tests read as the flow they check.</summary>
internal static class AuthApi
{
    public const string Password = "Secret123";

    public static RegisterRequest NewUser()
    {
        var id = Guid.NewGuid().ToString("N")[..12];
        return new RegisterRequest($"{id}@example.com", $"user_{id}", Password);
    }

    public static Task<HttpResponseMessage> RegisterAsync(this HttpClient client, RegisterRequest request) =>
        client.PostAsJsonAsync("/auth/register", request);

    public static Task<HttpResponseMessage> LoginAsync(this HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/auth/login", new LoginRequest(email, password));

    public static Task<HttpResponseMessage> RefreshAsync(this HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync("/auth/refresh", new RefreshRequest(refreshToken));

    public static async Task<AuthResponse> RegisterAndLoginAsync(this HttpClient client, RegisterRequest? user = null)
    {
        user ??= NewUser();
        (await client.RegisterAsync(user)).EnsureSuccessStatusCode();

        var login = await client.LoginAsync(user.Email, user.Password);
        login.EnsureSuccessStatusCode();
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

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
}
