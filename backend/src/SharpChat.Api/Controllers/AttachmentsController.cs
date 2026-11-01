using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using SharpChat.Api.Common;
using SharpChat.Api.RateLimiting;
using SharpChat.Application.Attachments;
using SharpChat.Application.Conversations;
using SharpChat.Domain.Attachments;

namespace SharpChat.Api.Controllers;

[ApiController]
[Route("conversations/{conversationId:guid}/attachments")]
public sealed class AttachmentsController(AttachmentService attachments) : ControllerBase
{
    private const long MultipartOverhead = 64 * 1024;

    [HttpPost]
    [Consumes("multipart/form-data")]
    [EnableRateLimiting(RateLimitingSetup.UploadPolicy)]
    [RequestSizeLimit(ChatOptions.MaxAttachmentSizeLimit + MultipartOverhead)]
    [RequestFormLimits(MultipartBodyLengthLimit = ChatOptions.MaxAttachmentSizeLimit + MultipartOverhead)]
    [ProducesResponseType<AttachmentDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge)]
    public async Task<ActionResult<AttachmentDto>> Upload(
        Guid conversationId, IFormFile file, CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var result = await attachments.UploadAsync(conversationId, file.FileName, file.Length, content, cancellationToken);
        return result.IsSuccess
            ? CreatedAtAction(nameof(Download), new { conversationId, attachmentId = result.Value.Id }, result.Value)
            : this.ToProblem(result.Error);
    }

    [HttpGet("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Download(Guid conversationId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var result = await attachments.OpenAsync(conversationId, attachmentId, cancellationToken);
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error);
        }

        var (attachment, content) = result.Value;
        var disposition = new ContentDispositionHeaderValue(attachment.Kind == AttachmentKind.Image ? "inline" : "attachment");
        disposition.SetHttpFileName(attachment.FileName);

        Response.Headers.ContentDisposition = disposition.ToString();
        Response.Headers.CacheControl = "private, max-age=31536000, immutable";
        Response.Headers.XContentTypeOptions = "nosniff";
        Response.Headers.ContentSecurityPolicy = "default-src 'none'; sandbox";
        return File(content, attachment.ContentType);
    }
}
