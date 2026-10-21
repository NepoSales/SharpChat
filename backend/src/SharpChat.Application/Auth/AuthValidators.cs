using FluentValidation;

namespace SharpChat.Application.Auth;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int UserNameMinLength = 3;
    public const int UserNameMaxLength = 32;
    public const string UserNameAllowedCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789._-";

    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(256).EmailAddress();

        RuleFor(x => x.UserName)
            .NotEmpty()
            .Length(UserNameMinLength, UserNameMaxLength)
            .Must(name => name.All(UserNameAllowedCharacters.Contains))
            .WithMessage("Username can only contain letters, digits, '.', '_' and '-'.");

        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty();
        RuleFor(x => x.Password).NotEmpty();
    }
}
