using FluentValidation;
using Microsoft.Extensions.Options;
using SharpChat.Application.Abstractions;
using SharpChat.Application.Common;
using SharpChat.Domain.Common;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Conversations;

public sealed class ConversationService(
    IConversationRepository conversations,
    IConversationQueries queries,
    MembershipService membership,
    IIdentityService identity,
    ICurrentUser currentUser,
    IOptions<ChatOptions> options,
    IValidator<CreateConversationRequest> createValidator,
    IValidator<AddMemberRequest> addMemberValidator,
    IValidator<ChangeRoleRequest> changeRoleValidator,
    TimeProvider time)
{
    private static readonly Error ConcurrentUpdate =
        Error.Conflict("concurrent_update", "The conversation was changed by another request. Please try again.");

    private int MaxMembers => options.Value.MaxGroupMembers;

    public async Task<Result<ConversationDto>> CreateAsync(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var validation = await createValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var userNames = (request.MemberUserNames ?? []).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var users = await identity.FindByUserNamesAsync(userNames, cancellationToken);

        var unknown = userNames.Where(name => !users.Any(u => string.Equals(u.UserName, name, StringComparison.OrdinalIgnoreCase))).ToList();
        if (unknown.Count > 0)
        {
            return Error.Validation(new Dictionary<string, string[]>
            {
                [nameof(CreateConversationRequest.MemberUserNames)] = [.. unknown.Select(name => $"User '{name}' was not found.")],
            });
        }

        var created = Conversation.Create(
            request.Name, currentUser.RequiredUserId(), users.Select(u => u.Id), MaxMembers, time.GetUtcNow());
        if (!created.IsSuccess)
        {
            return created.Error;
        }

        conversations.Add(created.Value);
        await conversations.SaveChangesAsync(cancellationToken);

        return (await queries.GetDetailsAsync(created.Value.Id, cancellationToken))!;
    }

    public Task<IReadOnlyList<ConversationSummaryDto>> ListMineAsync(CancellationToken cancellationToken) =>
        queries.ListForUserAsync(currentUser.RequiredUserId(), cancellationToken);

    public async Task<Result<ConversationDto>> GetAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        if (!await membership.IsMemberAsync(conversationId, currentUser.RequiredUserId(), cancellationToken))
        {
            return ConversationErrors.NotFound;
        }

        var conversation = await queries.GetDetailsAsync(conversationId, cancellationToken);
        return conversation is null ? ConversationErrors.NotFound : conversation;
    }

    public async Task<Result<MemberDto>> AddMemberAsync(Guid conversationId, AddMemberRequest request, CancellationToken cancellationToken)
    {
        var validation = await addMemberValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var conversation = await LoadForMemberAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ConversationErrors.NotFound;
        }

        var user = await identity.FindByUserNameAsync(request.UserName, cancellationToken);
        if (user is null)
        {
            return Error.NotFound("user_not_found", $"User '{request.UserName}' was not found.");
        }

        var added = conversation.AddMember(currentUser.RequiredUserId(), user.Id, MaxMembers, time.GetUtcNow());
        if (!added.IsSuccess)
        {
            return added.Error;
        }

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return new MemberDto(user.Id, user.UserName, added.Value.Role, added.Value.JoinedAt);
    }

    public async Task<Result<MemberDto>> ChangeRoleAsync(
        Guid conversationId, Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        var validation = await changeRoleValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return validation.ToError();
        }

        var conversation = await LoadForMemberAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ConversationErrors.NotFound;
        }

        var changed = conversation.ChangeRole(currentUser.RequiredUserId(), userId, request.Role, time.GetUtcNow());
        if (!changed.IsSuccess)
        {
            return changed.Error;
        }

        if (await TrySaveAsync(cancellationToken) is { } conflict)
        {
            return conflict;
        }

        return (await queries.GetMemberAsync(conversationId, userId, cancellationToken))!;
    }

    public async Task<Result> RemoveMemberAsync(Guid conversationId, Guid userId, CancellationToken cancellationToken)
    {
        var conversation = await LoadForMemberAsync(conversationId, cancellationToken);
        if (conversation is null)
        {
            return ConversationErrors.NotFound;
        }

        var removed = conversation.RemoveMember(currentUser.RequiredUserId(), userId, time.GetUtcNow());
        if (!removed.IsSuccess)
        {
            return removed;
        }

        return await TrySaveAsync(cancellationToken) is { } conflict ? conflict : Result.Success();
    }

    private async Task<Conversation?> LoadForMemberAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken);
        return conversation is not null && conversation.IsMember(currentUser.RequiredUserId()) ? conversation : null;
    }

    private async Task<Error?> TrySaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await conversations.SaveChangesAsync(cancellationToken);
            return null;
        }
        catch (ConcurrencyConflictException)
        {
            return ConcurrentUpdate;
        }
    }
}
