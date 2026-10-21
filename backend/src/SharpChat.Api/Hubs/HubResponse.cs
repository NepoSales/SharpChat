using SharpChat.Domain.Common;

namespace SharpChat.Api.Hubs;

public sealed record HubError(string Code, string Message, IReadOnlyDictionary<string, string[]>? Errors);

public sealed record HubResponse<T>(T? Data, HubError? Error)
{
#pragma warning disable CA1000
    public static HubResponse<T> Ok(T data) => new(data, null);

    public static HubResponse<T> Fail(Error error) =>
        new(default, new HubError(error.Code, error.Message, error.ValidationErrors));

    public static HubResponse<T> Fail(string code, string message) => new(default, new HubError(code, message, null));
#pragma warning restore CA1000
}
