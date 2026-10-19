using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Conversations;

public sealed record CreateConversationRequest(string Name, IReadOnlyList<string>? MemberUserNames);

public sealed record AddMemberRequest(string UserName);

public sealed record ChangeRoleRequest(ConversationRole Role);

public sealed record ConversationSummaryDto(
    Guid Id,
    string Name,
    int MemberCount,
    ConversationRole MyRole,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ConversationDto(
    Guid Id,
    string Name,
    Guid CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<MemberDto> Members);

public sealed record MemberDto(Guid UserId, string UserName, ConversationRole Role, DateTimeOffset JoinedAt);
