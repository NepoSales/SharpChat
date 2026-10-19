using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using SharpChat.Application.Conversations;
using SharpChat.Domain.Conversations;
using SharpChat.IntegrationTests.Auth;
using SharpChat.IntegrationTests.Infrastructure;

namespace SharpChat.IntegrationTests.Conversations;

[Collection(ApiTestGroup.Name)]
public sealed class ConversationTests(SharpChatApiFactory factory)
{
    [Fact]
    public async Task Owner_creates_a_conversation_visible_to_its_members()
    {
        var owner = await TestUser.CreateAsync(factory);
        var member = await TestUser.CreateAsync(factory);

        var response = await owner.CreateConversationAsync("backend team", member);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.ReadAsAsync<ConversationDto>();
        Assert.Equal($"/api/conversations/{created.Id}", response.Headers.Location?.AbsolutePath);
        Assert.Equal("backend team", created.Name);
        Assert.Contains(created.Members, m => m.UserId == owner.Id && m.UserName == owner.UserName && m.Role == ConversationRole.Owner);
        Assert.Contains(created.Members, m => m.UserId == member.Id && m.Role == ConversationRole.Member);

        var memberView = await member.GetConversationAsync(created.Id);
        Assert.Equal(HttpStatusCode.OK, memberView.StatusCode);

        var memberList = await member.Client.GetFromJsonAsync<List<ConversationSummaryDto>>("/api/conversations", TestUser.Json);
        var summary = Assert.Single(memberList!);
        Assert.Equal(2, summary.MemberCount);
        Assert.Equal(ConversationRole.Member, summary.MyRole);
    }

    [Fact]
    public async Task Non_members_cannot_see_or_change_a_conversation()
    {
        var owner = await TestUser.CreateAsync(factory);
        var outsider = await TestUser.CreateAsync(factory);
        var conversation = await owner.CreateConversationOkAsync("private");

        var responses = new[]
        {
            await outsider.GetConversationAsync(conversation.Id),
            await outsider.AddMemberAsync(conversation.Id, outsider.UserName),
            await outsider.RemoveMemberAsync(conversation.Id, owner.Id),
            await outsider.ChangeRoleAsync(conversation.Id, owner.Id, ConversationRole.Member),
        };

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.NotFound, r.StatusCode));
        Assert.Empty((await outsider.Client.GetFromJsonAsync<List<ConversationSummaryDto>>("/api/conversations", TestUser.Json))!);
    }

    [Fact]
    public async Task Conversation_endpoints_require_authentication()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/conversations")).StatusCode);
    }

    [Fact]
    public async Task Create_with_unknown_members_returns_validation_errors()
    {
        var owner = await TestUser.CreateAsync(factory);
        var ghost = new TestUser(Guid.NewGuid(), "ghost_user_does_not_exist", owner.Client);

        var response = await owner.CreateConversationAsync("team", ghost);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("ghost_user_does_not_exist", await response.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Owner_and_admins_add_members_but_members_cannot()
    {
        var owner = await TestUser.CreateAsync(factory);
        var admin = await TestUser.CreateAsync(factory);
        var member = await TestUser.CreateAsync(factory);
        var newcomer = await TestUser.CreateAsync(factory);
        var conversation = await owner.CreateConversationOkAsync("team", admin, member);
        await owner.ChangeRoleAsync(conversation.Id, admin.Id, ConversationRole.Admin);

        var byMember = await member.AddMemberAsync(conversation.Id, newcomer.UserName);
        var byAdmin = await admin.AddMemberAsync(conversation.Id, newcomer.UserName);
        var again = await owner.AddMemberAsync(conversation.Id, newcomer.UserName);

        Assert.Equal(HttpStatusCode.Forbidden, byMember.StatusCode);
        Assert.Equal("cannot_manage_members", await byMember.ErrorCodeAsync());
        Assert.Equal(HttpStatusCode.Created, byAdmin.StatusCode);
        Assert.Equal(newcomer.UserName, (await byAdmin.ReadAsAsync<MemberDto>()).UserName);
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("already_member", await again.ErrorCodeAsync());
    }

    [Fact]
    public async Task Members_leave_and_lower_roles_are_removed()
    {
        var owner = await TestUser.CreateAsync(factory);
        var admin = await TestUser.CreateAsync(factory);
        var member = await TestUser.CreateAsync(factory);
        var leaver = await TestUser.CreateAsync(factory);
        var conversation = await owner.CreateConversationOkAsync("team", admin, member, leaver);
        await owner.ChangeRoleAsync(conversation.Id, admin.Id, ConversationRole.Admin);

        Assert.Equal(HttpStatusCode.Forbidden, (await member.RemoveMemberAsync(conversation.Id, admin.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await admin.RemoveMemberAsync(conversation.Id, member.Id)).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await leaver.RemoveMemberAsync(conversation.Id, leaver.Id)).StatusCode);

        var ownerLeaves = await owner.RemoveMemberAsync(conversation.Id, owner.Id);
        Assert.Equal(HttpStatusCode.Conflict, ownerLeaves.StatusCode);
        Assert.Equal("owner_cannot_leave", await ownerLeaves.ErrorCodeAsync());

        Assert.Equal(HttpStatusCode.NotFound, (await member.GetConversationAsync(conversation.Id)).StatusCode);
        var remaining = await (await owner.GetConversationAsync(conversation.Id)).ReadAsAsync<ConversationDto>();
        Assert.Equal([owner.Id, admin.Id], remaining.Members.Select(m => m.UserId));
    }

    [Fact]
    public async Task Only_the_owner_changes_roles()
    {
        var owner = await TestUser.CreateAsync(factory);
        var admin = await TestUser.CreateAsync(factory);
        var member = await TestUser.CreateAsync(factory);
        var conversation = await owner.CreateConversationOkAsync("team", admin, member);

        var promoted = await owner.ChangeRoleAsync(conversation.Id, admin.Id, ConversationRole.Admin);
        var byAdmin = await admin.ChangeRoleAsync(conversation.Id, member.Id, ConversationRole.Admin);

        Assert.Equal(HttpStatusCode.OK, promoted.StatusCode);
        Assert.Equal(ConversationRole.Admin, (await promoted.ReadAsAsync<MemberDto>()).Role);
        Assert.Equal(HttpStatusCode.Forbidden, byAdmin.StatusCode);
    }

    [Fact]
    public async Task Member_limit_holds_even_under_concurrent_adds()
    {
        await using var smallGroups = factory.WithWebHostBuilder(b => b.UseSetting("Chat:MaxGroupMembers", "3"));
        var owner = await TestUser.CreateAsync(smallGroups);
        var member = await TestUser.CreateAsync(smallGroups);
        var first = await TestUser.CreateAsync(smallGroups);
        var second = await TestUser.CreateAsync(smallGroups);
        var conversation = await owner.CreateConversationOkAsync("small", member);

        // One slot left, two requests racing for it.
        var responses = await Task.WhenAll(
            owner.AddMemberAsync(conversation.Id, first.UserName),
            owner.AddMemberAsync(conversation.Id, second.UserName));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Conflict);
        var final = await (await owner.GetConversationAsync(conversation.Id)).ReadAsAsync<ConversationDto>();
        Assert.Equal(3, final.Members.Count);
    }
}
