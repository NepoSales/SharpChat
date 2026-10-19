using Microsoft.AspNetCore.Mvc;
using SharpChat.Api.Common;
using SharpChat.Application.Messages;

namespace SharpChat.Api.Controllers;

[ApiController]
[Route("conversations/{conversationId:guid}/messages")]
public sealed class MessagesController(MessageService messages) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<MessagePage>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<MessagePage>> GetHistory(
        Guid conversationId, [FromQuery] string? before, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var result = await messages.GetHistoryAsync(conversationId, before, limit, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
