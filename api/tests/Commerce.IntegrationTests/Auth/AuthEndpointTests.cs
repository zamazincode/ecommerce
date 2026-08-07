using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using Commerce.Api.Features.Auth;
using Commerce.Api.Features.Auth.Dtos;
using Commerce.Api.Persistence.Identity;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Shouldly;

namespace Commerce.IntegrationTests.Auth;

public class AuthEndpointTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static object ValidRegistration(string email = "yeni@test.com") => new
    {
        email,
        password = "Test1234",
        firstName = "Ali",
        lastName = "Veli"
    };

    /// Mail gövdesindeki <a href="...">'daki bağlantıyı çıkarır.
    private static Uri ExtractLink(string htmlBody)
    {
        var start = htmlBody.IndexOf("href=\"", StringComparison.Ordinal) + "href=\"".Length;
        var end = htmlBody.IndexOf('"', start);
        return new Uri(htmlBody[start..end]);
    }

    // ── Kayıt ────────────────────────────────────────────────

    [Fact]
    public async Task Register_WithValidData_Returns201AndPersistsUser()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Ct);
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Email.ShouldBe("yeni@test.com");
        auth.User.Roles.ShouldContain(AppRoles.Customer);

        var exists = await ExecuteDbAsync(db =>
            db.Users.AnyAsync(u => u.Email == "yeni@test.com", Ct));
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task Register_AssignsCustomerRole_EvenWhenRoleTableIsEmpty()
    {
        // Kılavuzun kırık noktası (Ölçüm 2.6): Respawn her testten sonra
        // AspNetRoles'u siliyor, hiçbir yardımcı çağrılmadan doğrudan API'ye
        // POST atıldığında rol tablosu gerçekten boş.
        var roleTableEmpty = await ExecuteDbAsync(async db => !await db.Roles.AnyAsync(Ct));
        roleTableEmpty.ShouldBeTrue();

        var response = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_Returns400()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var second = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        second.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("kisa1")]        // 8 karakterden kısa
    [InlineData("sadeceharf")]   // rakam yok
    [InlineData("12345678")]     // harf yok
    public async Task Register_WithWeakPassword_Returns400(string password)
    {
        var response = await Client.PostAsJsonAsync("/api/auth/register",
            new { email = "zayif@test.com", password, firstName = "A", lastName = "B" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_SendsEmailConfirmationMail()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        Factory.EmailService.SentEmails.ShouldContain(e => e.To == "yeni@test.com");
    }

    // ── Giriş ────────────────────────────────────────────────

    [Fact]
    public async Task Login_WithCorrectCredentials_ReturnsTokens()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "Test1234" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(Ct);
        auth!.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.RefreshToken.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "YanlisSifre1" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonExistentUser_Returns401_Not404()
    {
        // BU TESTİ ATLAMA.
        // 404 dönmek "bu e-posta kayıtlı değil" bilgisini sızdırır ve
        // saldırganın kullanıcı listesi çıkarmasına yarar (user enumeration).
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "hicvarolmayan@test.com", password = "Test1234" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.StatusCode.ShouldNotBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Login_ErrorMessage_DoesNotRevealWhetherEmailExists()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var wrongPassword = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "Yanlis1234" }, Ct);
        var noSuchUser = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yok@test.com", password = "Yanlis1234" }, Ct);

        var body1 = await wrongPassword.Content.ReadAsStringAsync(Ct);
        var body2 = await noSuchUser.Content.ReadAsStringAsync(Ct);

        // İki durumda da aynı mesaj dönmeli.
        wrongPassword.StatusCode.ShouldBe(noSuchUser.StatusCode);
        body1.ShouldContain("E-posta veya şifre hatalı");
        body2.ShouldContain("E-posta veya şifre hatalı");
    }

    [Fact]
    public async Task Login_AfterFiveFailedAttempts_LocksAccount()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        // Identity varsayılanı: 5 başarısız deneme → kilit.
        for (var i = 0; i < 5; i++)
        {
            await Client.PostAsJsonAsync("/api/auth/login",
                new { email = "yeni@test.com", password = "YanlisSifre1" }, Ct);
        }

        // Şimdi DOĞRU şifreyle bile giriş yapılamaz — hesap kilitli.
        var response = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "Test1234" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldContain("kilitlendi");
    }

    // ── Korumalı endpoint / token doğrulama ──────────────────

    [Fact]
    public async Task Me_WithoutToken_Returns401()
    {
        var response = await Client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithValidToken_ReturnsCurrentUser()
    {
        await AuthenticateAsync("musteri@test.com");

        var user = await Client.GetFromJsonAsync<UserDto>("/api/auth/me", Ct);

        user!.Email.ShouldBe("musteri@test.com");
        user.Roles.ShouldContain(AppRoles.Customer);
    }

    [Fact]
    public async Task Me_WithTamperedSignature_Returns401()
    {
        await AuthenticateAsync();
        var token = Client.DefaultRequestHeaders.Authorization!.Parameter!;

        // İmzanın son karakterini değiştir (Ölçüm 2.8: 300/300 örnekte güvenilir).
        var tampered = token[..^1] + (token[^1] == 'A' ? 'B' : 'A');
        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await Client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithExpiredToken_Returns401()
    {
        var userId = await CreateUserAsync("sureli@test.com", "Test1234", AppRoles.Customer);

        // Thread.Sleep(900000) YAZMIYORUZ.
        // TokenService'i doğrudan çözüp geçmişte biten bir token üretiyoruz.
        string expiredToken;
        using (var scope = Factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var tokens = scope.ServiceProvider.GetRequiredService<TokenService>();

            var user = await users.FindByIdAsync(userId.ToString());
            (expiredToken, _) = tokens.CreateAccessToken(user!, [AppRoles.Customer],
                lifetime: TimeSpan.FromMinutes(-5));
        }

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expiredToken);

        var response = await Client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_WithTokenSignedByAnotherKey_Returns401()
    {
        var userId = await CreateUserAsync("baskaanahtar@test.com", "Test1234", AppRoles.Customer);

        // Claim'ler doğru, ama imza CustomWebApplicationFactory'nin ayarladığı
        // anahtarla değil, başka (yine 32+ karakter) bir anahtarla atılmış.
        var claims = new[]
        {
            new Claim(JwtClaims.Sub, userId.ToString()),
            new Claim(JwtClaims.Email, "baskaanahtar@test.com"),
            new Claim(JwtClaims.Role, AppRoles.Customer)
        };
        var otherKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes("baska-bir-imza-anahtari-en-az-32-karakter"));
        var forged = new JwtSecurityToken(
            issuer: "commerce-api",
            audience: "commerce-clients",
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(15),
            signingCredentials: new SigningCredentials(otherKey, SecurityAlgorithms.HmacSha256));
        var forgedToken = new JwtSecurityTokenHandler().WriteToken(forged);

        Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forgedToken);

        var response = await Client.GetAsync("/api/auth/me", Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Refresh akışı ────────────────────────────────────────

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokensAndInvalidatesOldOne()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var first = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = first!.RefreshToken }, Ct);

        refreshResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var second = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(Ct);
        second!.RefreshToken.ShouldNotBe(first.RefreshToken);

        // ESKİ refresh token artık çalışmamalı (rotasyon).
        var reuse = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = first.RefreshToken }, Ct);

        reuse.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WhenRevokedTokenIsReused_RevokesAllUserTokens()
    {
        // Çalınmış token senaryosu.
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var first = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        var refreshed = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = first!.RefreshToken }, Ct);
        var second = await refreshed.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        // Saldırgan eski (iptal edilmiş) token'ı kullanıyor.
        await Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }, Ct);

        // Meşru kullanıcının GEÇERLİ token'ı da artık çalışmamalı.
        var legitimate = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = second!.RefreshToken }, Ct);

        legitimate.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        var activeCount = await ExecuteDbAsync(db =>
            db.RefreshTokens.CountAsync(t => t.RevokedAt == null, Ct));
        activeCount.ShouldBe(0);
    }

    [Fact]
    public async Task Refresh_WithUnknownToken_Returns401()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "boyle-bir-token-yok" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithEmptyToken_Returns400()
    {
        var response = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = "" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Refresh_StoresOnlyHashOfToken()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        var rawTokenExists = await ExecuteDbAsync(db =>
            db.RefreshTokens.AnyAsync(t => t.Token == auth!.RefreshToken, Ct));
        rawTokenExists.ShouldBeFalse();

        var rows = await ExecuteDbAsync(db => db.RefreshTokens.ToListAsync(Ct));
        rows.Count.ShouldBe(1);
        rows[0].Token.Length.ShouldBe(64); // SHA-256 hex
    }

    [Fact]
    public async Task Refresh_NewAccessTokenWorksOnProtectedEndpoint()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var first = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        var refreshResponse = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = first!.RefreshToken }, Ct);
        var second = await refreshResponse.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", second!.AccessToken);
        var me = await Client.GetAsync("/api/auth/me", Ct);

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Refresh_ConcurrentSameToken_OnlyOneSucceeds()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var first = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        // Aynı refresh token'la eşzamanlı iki istek — atomik rotasyon (K6)
        // sayesinde Postgres satır kilidi tam olarak birini kazandırır.
        var t1 = Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first!.RefreshToken }, Ct);
        var t2 = Client.PostAsJsonAsync("/api/auth/refresh", new { refreshToken = first.RefreshToken }, Ct);
        var results = await Task.WhenAll(t1, t2);

        results.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBe(1);
        results.Count(r => r.StatusCode == HttpStatusCode.Unauthorized).ShouldBe(1);
    }

    [Fact]
    public async Task Logout_RevokesRefreshToken()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var auth = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        var logout = await Client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = auth!.RefreshToken }, Ct);
        logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var refresh = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = auth.RefreshToken }, Ct);
        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── Şifre sıfırlama ──────────────────────────────────────

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_Returns204AndSendsNothing()
    {
        // Kayıtlı olmayan e-posta için de 204 dönmeli — aksi hâlde
        // "bu e-posta kayıtlı mı" sorgulama endpoint'i sağlamış olursun.
        var response = await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "hic-yok@test.com" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        Factory.EmailService.SentEmails.ShouldNotContain(e => e.To == "hic-yok@test.com");
    }

    [Fact]
    public async Task ForgotPassword_ForExistingUser_SendsResetMail()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "yeni@test.com" }, Ct);

        Factory.EmailService.SentEmails.ShouldContain(e =>
            e.To == "yeni@test.com" && e.Subject.Contains("Şifre"));
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_Returns400()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var response = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = "yeni@test.com", token = "gecersiz-token", newPassword = "YeniSifre1" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ChangesPasswordAndRevokesSessions()
    {
        var register = await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);
        var original = await register.Content.ReadFromJsonAsync<AuthResponse>(Ct);

        await Client.PostAsJsonAsync("/api/auth/forgot-password",
            new { email = "yeni@test.com" }, Ct);

        var mail = Factory.EmailService.SentEmails.First(e => e.Subject.Contains("Şifre"));
        var link = ExtractLink(mail.Body);
        var query = QueryHelpers.ParseQuery(link.Query);
        var resetToken = query["token"].ToString();

        var reset = await Client.PostAsJsonAsync("/api/auth/reset-password",
            new { email = "yeni@test.com", token = resetToken, newPassword = "YeniSifre1" }, Ct);
        reset.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Yeni şifreyle giriş çalışmalı.
        var loginNew = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "YeniSifre1" }, Ct);
        loginNew.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Eski şifreyle giriş artık çalışmamalı.
        var loginOld = await Client.PostAsJsonAsync("/api/auth/login",
            new { email = "yeni@test.com", password = "Test1234" }, Ct);
        loginOld.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        // Şifre değiştiğinde tüm oturumlar (eski refresh token dahil) kapanmalı.
        var oldRefresh = await Client.PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = original!.RefreshToken }, Ct);
        oldRefresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // ── E-posta doğrulama ────────────────────────────────────

    [Fact]
    public async Task VerifyEmail_WithValidToken_ConfirmsEmail()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var mail = Factory.EmailService.SentEmails.First(e => e.To == "yeni@test.com");
        var link = ExtractLink(mail.Body);
        var query = QueryHelpers.ParseQuery(link.Query);
        var verifyToken = query["token"].ToString();

        var response = await Client.PostAsJsonAsync("/api/auth/verify-email",
            new { email = "yeni@test.com", token = verifyToken }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var confirmed = await ExecuteDbAsync(db =>
            db.Users.Where(u => u.Email == "yeni@test.com").Select(u => u.EmailConfirmed).SingleAsync(Ct));
        confirmed.ShouldBeTrue();
    }

    [Fact]
    public async Task VerifyEmail_WithInvalidToken_Returns400()
    {
        await Client.PostAsJsonAsync("/api/auth/register", ValidRegistration(), Ct);

        var response = await Client.PostAsJsonAsync("/api/auth/verify-email",
            new { email = "yeni@test.com", token = "gecersiz-token" }, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
