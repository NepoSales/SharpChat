using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SharpChat.Application.Auth;
using SharpChat.Application.Users;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Auth;

[Collection(ApiTestGroup.Name)]
public sealed class AuthFlowTests(SharpChatApiFactory factory)
{
    private readonly HttpClient _client = factory.CreateClient();

    [Fact]
    public async Task Register_login_refresh_and_logout()
    {
        var user = AuthApi.NewUser();

        var register = await _client.RegisterAsync(user);
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);

        var tokens = await (await _client.LoginAsync(user.Email, user.Password)).Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(tokens);

        var me = await _client.GetMeAsync(tokens.AccessToken);
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var profile = await me.Content.ReadFromJsonAsync<UserDto>();
        Assert.Equal(user.UserName, profile?.UserName);
        Assert.Equal(user.Email, profile?.Email);

        var refresh = await _client.RefreshAsync(tokens.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetMeAsync(rotated.AccessToken)).StatusCode);

        var logout = await _client.PostAsJsonAsync("/auth/logout", new LogoutRequest(rotated.RefreshToken));
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _client.RefreshAsync(rotated.RefreshToken)).StatusCode);
    }

    [Fact]
    public async Task Register_with_a_taken_email_or_username_conflicts()
    {
        var user = AuthApi.NewUser();
        await _client.RegisterAsync(user);

        var sameEmail = await _client.RegisterAsync(AuthApi.NewUser() with { Email = user.Email });
        var sameUserName = await _client.RegisterAsync(AuthApi.NewUser() with { UserName = user.UserName });

        Assert.Equal(HttpStatusCode.Conflict, sameEmail.StatusCode);
        Assert.Equal("email_taken", await sameEmail.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Conflict, sameUserName.StatusCode);
        Assert.Equal("username_taken", await sameUserName.ErrorCodeAsync());
    }

    [Theory]
    [InlineData("Email", "not-an-email")]
    [InlineData("UserName", "bad name!")]
    [InlineData("Password", "weakpassword")] // passes the validator, rejected by Identity's password rules
    public async Task Register_with_invalid_data_returns_validation_errors(string field, string value)
    {
        var user = AuthApi.NewUser();
        var request = field switch
        {
            "Email" => user with { Email = value },
            "UserName" => user with { UserName = value },
            _ => user with { Password = value },
        };

        var response = await _client.RegisterAsync(request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.GetProperty("errors").TryGetProperty(field, out _), $"Expected an error for {field}");
    }

    [Fact]
    public async Task Login_failures_do_not_reveal_whether_the_email_exists()
    {
        var user = AuthApi.NewUser();
        await _client.RegisterAsync(user);

        var wrongPassword = await _client.LoginAsync(user.Email, "Wrong1234");
        var unknownEmail = await _client.LoginAsync("nobody@example.com", "Wrong1234");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        Assert.Equal(await wrongPassword.ErrorCodeAsync(), await unknownEmail.ErrorCodeAsync());
    }

    [Fact]
    public async Task Account_is_locked_after_repeated_failed_logins()
    {
        var user = AuthApi.NewUser();
        await _client.RegisterAsync(user);

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await _client.LoginAsync(user.Email, "Wrong1234");
        }

        var correctPassword = await _client.LoginAsync(user.Email, user.Password);

        Assert.Equal(HttpStatusCode.Unauthorized, correctPassword.StatusCode);
    }

    [Fact]
    public async Task Reusing_a_rotated_refresh_token_ends_the_session()
    {
        var first = await _client.RegisterAndLoginAsync();
        var second = await (await _client.RefreshAsync(first.RefreshToken)).Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(second);

        var reuse = await _client.RefreshAsync(first.RefreshToken);
        var afterReuse = await _client.RefreshAsync(second.RefreshToken);

        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Sessions_are_independent()
    {
        var user = AuthApi.NewUser();
        var laptop = await _client.RegisterAndLoginAsync(user);
        var phone = await (await _client.LoginAsync(user.Email, user.Password)).Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(phone);

        await _client.PostAsJsonAsync("/auth/logout", new LogoutRequest(laptop.RefreshToken));

        Assert.Equal(HttpStatusCode.OK, (await _client.RefreshAsync(phone.RefreshToken)).StatusCode);
    }
}
