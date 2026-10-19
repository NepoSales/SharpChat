using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc.Testing;
using SharpChat.Application.Users;
using SharpChat.IntegrationTests.Auth;

namespace SharpChat.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string UserName, HttpClient Client, string AccessToken = "")
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static async Task<TestUser> CreateAsync<TEntryPoint>(WebApplicationFactory<TEntryPoint> factory)
        where TEntryPoint : class
    {
        var client = factory.CreateClient();
        var tokens = await client.RegisterAndLoginAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);

        var me = (await client.GetFromJsonAsync<UserDto>("/users/me"))!;
        return new TestUser(me.Id, me.UserName, client, tokens.AccessToken);
    }
}

internal static class HttpResponseJsonExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TestUser.Json))!;
}
