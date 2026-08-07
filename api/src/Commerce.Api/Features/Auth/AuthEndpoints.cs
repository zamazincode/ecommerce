using System.Security.Claims;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Filters;
using Commerce.Api.Features.Auth.Dtos;
using Microsoft.AspNetCore.RateLimiting;

namespace Commerce.Api.Features.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth")
                       .WithTags("Auth")
                       // Brute-force koruması: IP başına dakikada 10 deneme.
                       .RequireRateLimiting("auth");

        group.MapPost("/register", Register)
             .WithValidation<RegisterRequest>()
             .WithSummary("Yeni kullanıcı kaydı")
             .Produces<AuthResponse>(StatusCodes.Status201Created)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/login", Login)
             .WithValidation<LoginRequest>()
             .WithSummary("Giriş")
             .Produces<AuthResponse>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", Refresh)
             .WithValidation<RefreshRequest>()
             .WithSummary("Access token'ı yeniler (refresh token rotasyonlu)")
             .Produces<AuthResponse>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/logout", Logout)
             .WithValidation<LogoutRequest>()
             .WithSummary("Refresh token'ı iptal eder")
             .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/forgot-password", ForgotPassword)
             .WithValidation<ForgotPasswordRequest>()
             .WithSummary("Şifre sıfırlama maili gönderir")
             .Produces(StatusCodes.Status204NoContent);

        group.MapPost("/reset-password", ResetPassword)
             .WithValidation<ResetPasswordRequest>()
             .WithSummary("Şifreyi sıfırlar")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPost("/verify-email", VerifyEmail)
             .WithValidation<VerifyEmailRequest>()
             .WithSummary("E-posta adresini doğrular")
             .Produces(StatusCodes.Status204NoContent);

        group.MapGet("/me", Me)
             .RequireAuthorization()
             // Web istemcisi her sayfa yüklemesinde çağıracak (PLAN.md Faz W2);
             // sıkı "auth" grubuna tabi olursa kullanıcı sebepsiz 429 görür.
             // IP başına 200/dk'lık GlobalLimiter'a düşer — hâlâ korumalı.
             .DisableRateLimiting()
             .WithSummary("Giriş yapmış kullanıcının bilgileri")
             .Produces<UserDto>()
             .ProducesProblem(StatusCodes.Status401Unauthorized);

        group.MapPost("/admin/users/{userId:guid}/revoke-sessions", RevokeSessions)
             .RequireAuthorization(AuthPolicies.AdminOnly)
             .WithSummary("Bir kullanıcının tüm oturumlarını kapatır (admin)")
             .Produces(StatusCodes.Status204NoContent)
             .ProducesProblem(StatusCodes.Status401Unauthorized)
             .ProducesProblem(StatusCodes.Status403Forbidden);

        return app;
    }

    private static async Task<IResult> Register(
        RegisterRequest request, AuthService service, CancellationToken ct)
    {
        var response = await service.RegisterAsync(request, ct);
        // "/api/users/{id}" diye bir endpoint yok; Location var olmayan
        // kaynağı göstermesin — dönen kaynak zaten /api/auth/me.
        return TypedResults.Created("/api/auth/me", response);
    }

    private static async Task<AuthResponse> Login(
        LoginRequest request, AuthService service, CancellationToken ct)
        => await service.LoginAsync(request, ct);

    private static async Task<AuthResponse> Refresh(
        RefreshRequest request, AuthService service, CancellationToken ct)
        => await service.RefreshAsync(request.RefreshToken, ct);

    private static async Task<IResult> Logout(
        LogoutRequest request, AuthService service, CancellationToken ct)
    {
        await service.LogoutAsync(request.RefreshToken, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ForgotPassword(
        ForgotPasswordRequest request, AuthService service, CancellationToken ct)
    {
        await service.ForgotPasswordAsync(request.Email, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> ResetPassword(
        ResetPasswordRequest request, AuthService service, CancellationToken ct)
    {
        await service.ResetPasswordAsync(request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<IResult> VerifyEmail(
        VerifyEmailRequest request, AuthService service, CancellationToken ct)
    {
        await service.VerifyEmailAsync(request, ct);
        return TypedResults.NoContent();
    }

    private static async Task<UserDto> Me(
        ClaimsPrincipal principal, AuthService service, CancellationToken ct)
        => await service.GetCurrentUserAsync(principal.GetUserId(), ct);

    private static async Task<IResult> RevokeSessions(
        Guid userId, AuthService service, CancellationToken ct)
    {
        await service.RevokeAllSessionsAsync(userId, ct);
        return TypedResults.NoContent();
    }
}
