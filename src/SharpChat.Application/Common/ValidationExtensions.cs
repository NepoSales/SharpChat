using FluentValidation.Results;

namespace SharpChat.Application.Common;

internal static class ValidationExtensions
{
    public static Error ToError(this ValidationResult result) =>
        Error.Validation(result.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray()));
}
