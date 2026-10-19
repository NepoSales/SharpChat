using Microsoft.AspNetCore.Mvc;
using SharpChat.Api.Common;
using SharpChat.Application.Conversations;

namespace SharpChat.Api.Controllers;

[ApiController]
[Route("api/conversations")]
public sealed class ConversationsController(ConversationService conversations) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ConversationDto>> Create(CreateConversationRequest request, CancellationToken cancellationToken)
    {
        var result = await conversations.CreateAsync(request, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Get), new { id = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ConversationSummaryDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConversationSummaryDto>>> List(CancellationToken cancellationToken) =>
        Ok(await conversations.ListMineAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    [ProducesResponseType<ConversationDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConversationDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var result = await conversations.GetAsync(id, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("{id:guid}/members")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<MemberDto>> AddMember(Guid id, AddMemberRequest request, CancellationToken cancellationToken)
    {
        var result = await conversations.AddMemberAsync(id, request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [HttpPatch("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType<MemberDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MemberDto>> ChangeRole(
        Guid id, Guid userId, ChangeRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await conversations.ChangeRoleAsync(id, userId, request, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    /// <summary>Removes a member. Passing your own user id leaves the conversation.</summary>
    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken cancellationToken)
    {
        var result = await conversations.RemoveMemberAsync(id, userId, cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
