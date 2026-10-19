using Microsoft.AspNetCore.Mvc;
using SharpChat.Api.Common;
using SharpChat.Application.Users;

namespace SharpChat.Api.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UsersController(UserService users) : ControllerBase
{
    [HttpGet("me")]
    [ProducesResponseType<UserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> Me(CancellationToken cancellationToken)
    {
        var result = await users.GetCurrentAsync(cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }
}
