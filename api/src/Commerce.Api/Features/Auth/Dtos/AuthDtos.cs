namespace Commerce.Api.Features.Auth.Dtos;

public sealed record RegisterRequest(
    string Email, string Password, string FirstName, string LastName);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record ForgotPasswordRequest(string Email);

public sealed record ResetPasswordRequest(string Email, string Token, string NewPassword);

public sealed record VerifyEmailRequest(string Email, string Token);

public sealed record UserDto(
    Guid Id, string Email, string FirstName, string LastName,
    bool EmailConfirmed, IReadOnlyList<string> Roles);

public sealed record AuthResponse(
    string AccessToken,
    DateTime AccessTokenExpiresAt,
    string RefreshToken,
    DateTime RefreshTokenExpiresAt,
    UserDto User);
