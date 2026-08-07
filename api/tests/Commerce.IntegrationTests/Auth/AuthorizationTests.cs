using System.Net;
using System.Net.Http.Json;
using Commerce.Api.Features.Auth.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Commerce.IntegrationTests.Auth;

/// Faz 11'de admin endpoint'leri gelince bu dosya büyüyecek.
/// Şimdi tek admin endpoint'i (revoke-sessions) üzerinden yetkilendirme
/// politikalarının gerçekten çalıştığını doğruluyoruz.
public class AuthorizationTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("/api/products")]
    [InlineData("/api/categories")]
    [InlineData("/api/search?q=kitap")]
    [InlineData("/health")]
    public async Task PublicEndpoints_AreAccessibleWithoutToken(string url)
    {
        // Katalog/arama herkese açık olmalı — auth eklerken kazara kapatmadığımızı doğruluyoruz.
        var response = await Client.GetAsync(url, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminUser_HasAdminRoleInToken()
    {
        await AuthenticateAsync("admin@test.com", role: AppRoles.Admin);

        var user = await Client.GetFromJsonAsync<UserDto>("/api/auth/me", Ct);

        user!.Roles.ShouldContain(AppRoles.Admin);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithoutToken_Returns401()
    {
        var targetUserId = await CreateUserAsync("hedef@test.com", "Test1234", AppRoles.Customer);

        var response = await Client.PostAsync(
            $"/api/auth/admin/users/{targetUserId}/revoke-sessions", content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithCustomerToken_Returns403()
    {
        var targetUserId = await CreateUserAsync("hedef2@test.com", "Test1234", AppRoles.Customer);

        // Test'in kendi Client'ını AuthenticateAsync ile ezmek yerine ayrı bir
        // müşteri istemcisi kuruyoruz — hedef kullanıcıyı ikinci kez oluşturmaya
        // çalışırsak CreateUserAsync patlar.
        var customerClient = await CreateAuthenticatedClientAsync("musteri2@test.com");

        var response = await customerClient.PostAsync(
            $"/api/auth/admin/users/{targetUserId}/revoke-sessions", content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithAdminToken_Returns204()
    {
        var targetUserId = await CreateUserAsync("hedef3@test.com", "Test1234", AppRoles.Customer);
        // Giriş yaparak hedef kullanıcı için aktif bir refresh token oluştur —
        // aksi hâlde "tüm oturumlar kapandı" iddiasını doğrulayacak bir şey olmaz.
        await GetAccessTokenAsync("hedef3@test.com", "Test1234");

        var adminClient = await CreateAuthenticatedClientAsync("admin2@test.com", role: AppRoles.Admin);

        var response = await adminClient.PostAsync(
            $"/api/auth/admin/users/{targetUserId}/revoke-sessions", content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var activeTokens = await ExecuteDbAsync(db =>
            db.RefreshTokens.CountAsync(t => t.UserId == targetUserId && t.RevokedAt == null, Ct));
        activeTokens.ShouldBe(0);
    }
}
