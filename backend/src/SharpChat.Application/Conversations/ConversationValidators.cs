using FluentValidation;
using Microsoft.Extensions.Options;
using SharpChat.Domain.Conversations;

namespace SharpChat.Application.Conversations;

public sealed class CreateConversationRequestValidator : AbstractValidator<CreateConversationRequest>
{
    public CreateConversationRequestValidator(IOptions<ChatOptions> options)
    {
        var maxInitialMembers = options.Value.MaxGroupMembers - 1;

        RuleFor(x => x.Name)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Name is required.")
            .MaximumLength(Conversation.NameMaxLength);

        RuleFor(x => x.MemberUserNames)
            .Must(names => names is null || names.Count <= maxInitialMembers)
            .WithMessage($"A conversation can have at most {maxInitialMembers} members besides the creator.");

        RuleForEach(x => x.MemberUserNames).NotEmpty();
    }
}

public sealed class AddMemberRequestValidator : AbstractValidator<AddMemberRequest>
{
    public AddMemberRequestValidator()
    {
        RuleFor(x => x.UserName).NotEmpty();
    }
}

public sealed class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator()
    {
        RuleFor(x => x.Role).IsInEnum();
    }
}
