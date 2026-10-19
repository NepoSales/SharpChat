using SharpChat.Application.Auth;

namespace SharpChat.UnitTests.Application;

public sealed class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Valid_request_passes()
    {
        var result = _validator.Validate(new RegisterRequest("ada@example.com", "ada.lovelace", "Secret123"));

        Assert.True(result.IsValid);
    }

    [Theory]
    [InlineData("not-an-email", "ada", "Secret123", nameof(RegisterRequest.Email))]
    [InlineData("ada@example.com", "ad", "Secret123", nameof(RegisterRequest.UserName))]
    [InlineData("ada@example.com", "ada lovelace", "Secret123", nameof(RegisterRequest.UserName))]
    [InlineData("ada@example.com", "ada<script>", "Secret123", nameof(RegisterRequest.UserName))]
    [InlineData("ada@example.com", "ada", "", nameof(RegisterRequest.Password))]
    public void Invalid_field_is_reported(string email, string userName, string password, string field)
    {
        var result = _validator.Validate(new RegisterRequest(email, userName, password));

        Assert.Contains(result.Errors, error => error.PropertyName == field);
    }
}
