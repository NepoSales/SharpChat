using System.Net.Http.Json;
using SharpChat.Application.Conversations;
using SharpChat.Domain.Conversations;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Conversations;

internal static class ConversationApi
{
    public static Task<HttpResponseMessage> CreateConversationAsync(this TestUser user, string name, params TestUser[] members) =>
        user.Client.PostAsJsonAsync(
            "/conversations",
            new CreateConversationRequest(name, [.. members.Select(m => m.UserName)]),
            TestUser.Json);

    public static async Task<ConversationDto> CreateConversationOkAsync(this TestUser user, string name, params TestUser[] members)
    {
        var response = await user.CreateConversationAsync(name, members);
        response.EnsureSuccessStatusCode();
        return await response.ReadAsAsync<ConversationDto>();
    }

    public static Task<HttpResponseMessage> GetConversationAsync(this TestUser user, Guid conversationId) =>
        user.Client.GetAsync($"/conversations/{conversationId}");

    public static Task<HttpResponseMessage> AddMemberAsync(this TestUser user, Guid conversationId, string userName) =>
        user.Client.PostAsJsonAsync($"/conversations/{conversationId}/members", new AddMemberRequest(userName), TestUser.Json);

    public static Task<HttpResponseMessage> RemoveMemberAsync(this TestUser user, Guid conversationId, Guid userId) =>
        user.Client.DeleteAsync($"/conversations/{conversationId}/members/{userId}");

    public static Task<HttpResponseMessage> ChangeRoleAsync(this TestUser user, Guid conversationId, Guid userId, ConversationRole role) =>
        user.Client.PatchAsJsonAsync(
            $"/conversations/{conversationId}/members/{userId}", new ChangeRoleRequest(role), TestUser.Json);
}
