using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharpChat.Api.Auth;
using SharpChat.Api.Common;
using SharpChat.Application.Auth;
using SharpChat.Application.Users;
using SharpChat.Domain.Common;

namespace SharpChat.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("auth")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    [ProducesResponseType<UserDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UserDto>> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.RegisterAsync(request, cancellationToken);
        return result.IsSuccess ? StatusCode(StatusCodes.Status201Created, result.Value) : this.ToProblem(result.Error);
    }

    [HttpPost("login")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request, cancellationToken);
        return StartSession(result);
    }

    [HttpPost("refresh")]
    [ProducesResponseType<SessionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionResponse>> Refresh(CancellationToken cancellationToken)
    {
        var result = await auth.RefreshAsync(new RefreshRequest(RefreshTokenCookie.Read(Request)), cancellationToken);
        if (!result.IsSuccess)
        {
            RefreshTokenCookie.Delete(Response);
        }

        return StartSession(result);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await auth.LogoutAsync(new LogoutRequest(RefreshTokenCookie.Read(Request)), cancellationToken);
        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    private ActionResult<SessionResponse> StartSession(Result<AuthResponse> result)
    {
        if (!result.IsSuccess)
        {
            return this.ToProblem(result.Error);
        }

        RefreshTokenCookie.Append(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(SessionResponse.From(result.Value));
    }
}
