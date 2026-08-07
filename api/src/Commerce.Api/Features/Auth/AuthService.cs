using System.Text;
using Commerce.Api.Common.Email;
using Commerce.Api.Common.Exceptions;
using Commerce.Api.Features.Auth.Dtos;
using Commerce.Api.Persistence;
using Commerce.Api.Persistence.Identity;
using Commerce.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Commerce.Api.Features.Auth;

public sealed class AuthService(
    UserManager<ApplicationUser> userManager,
    RoleManager<ApplicationRole> roleManager,
    AppDbContext db,
    TokenService tokens,
    IEmailService email,
    TimeProvider clock,
    IOptions<WebAppSettings> webOptions,
    ILogger<AuthService> logger)
{
    // ─────────────────────────────────────────────────────────
    // Kayıt
    // ─────────────────────────────────────────────────────────
    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var existing = await userManager.FindByEmailAsync(request.Email);
        // 409 değil — PLAN.md Faz 5 sözleşmesi "aynı e-postayla ikinci kayıt → 400" diyor.
        if (existing is not null)
            throw new BusinessRuleException("Bu e-posta adresi zaten kayıtlı.");

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            CreatedAt = clock.GetUtcNow().UtcDateTime
        };

        var result = await userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            throw new BusinessRuleException(
                string.Join(" ", result.Errors.Select(e => e.Description)));

        // Rol tablosu boşsa AddToRoleAsync InvalidOperationException atar (500).
        // Entegrasyon testlerinde Respawn her testten sonra AspNetRoles'u siliyor,
        // bu yüzden rolü burada garanti ediyoruz.
        await EnsureRoleAsync(AppRoles.Customer);
        await userManager.AddToRoleAsync(user, AppRoles.Customer);

        await SendEmailConfirmationAsync(user, ct);

        return await CreateAuthResponseAsync(user, ct);
    }

    private async Task EnsureRoleAsync(string role)
    {
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new ApplicationRole { Name = role });
    }

    // ─────────────────────────────────────────────────────────
    // Giriş
    // ─────────────────────────────────────────────────────────
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);

        // KRİTİK: Kullanıcı yoksa da 401 dön, 404 DEĞİL.
        // 404 dönmek "bu e-posta kayıtlı değil" bilgisini sızdırır;
        // saldırgan e-posta listesi çıkarabilir (user enumeration).
        if (user is null)
        {
            // Zamanlama saldırısını da zorlaştırmak için sahte bir hash doğrulaması
            // yapılabilir; bu ölçekte gerek yok ama bilerek atlıyoruz.
            throw new UnauthorizedException();
        }

        if (await userManager.IsLockedOutAsync(user))
            throw new UnauthorizedException("Hesabınız geçici olarak kilitlendi. Daha sonra tekrar deneyin.");

        if (!await userManager.CheckPasswordAsync(user, request.Password))
        {
            // Başarısız denemeyi say; eşiği aşınca Identity hesabı kilitler.
            await userManager.AccessFailedAsync(user);
            throw new UnauthorizedException();
        }

        await userManager.ResetAccessFailedCountAsync(user);

        return await CreateAuthResponseAsync(user, ct);
    }

    // ─────────────────────────────────────────────────────────
    // Token yenileme — ROTASYON + YENİDEN KULLANIM TESPİTİ
    // ─────────────────────────────────────────────────────────
    public async Task<AuthResponse> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(refreshToken);
        var now = clock.GetUtcNow().UtcDateTime;

        // AsNoTracking ZORUNLU: aşağıdaki ExecuteUpdateAsync bu satırı DB'de
        // güncelleyecek. Takip edilen bir kopya tutarsak change tracker bayatlar
        // ve son SaveChangesAsync beklenmedik bir UPDATE üretebilir.
        var stored = await db.RefreshTokens
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Token == hash, ct);

        if (stored is null)
            throw new UnauthorizedException("Geçersiz yenileme anahtarı.");

        // İptal edilmiş bir token tekrar kullanıldı.
        // Bu, token'ın çalındığı anlamına gelir: meşru istemci zaten yenisini aldı,
        // eskisini kullanan taraf saldırgandır (ya da tersi — hangisi olduğunu
        // bilemeyiz). Güvenli davranış: kullanıcının TÜM oturumlarını kapat.
        if (stored.IsRevoked)
        {
            logger.LogWarning(
                "Refresh token yeniden kullanıldı. Kullanıcının tüm oturumları kapatılıyor. UserId: {UserId}",
                stored.UserId);

            await RevokeAllTokensAsync(stored.UserId, now, ct);
            throw new UnauthorizedException("Oturumunuz güvenlik nedeniyle sonlandırıldı. Lütfen tekrar giriş yapın.");
        }

        if (stored.IsExpired(now))
            throw new UnauthorizedException("Yenileme anahtarının süresi dolmuş.");

        var user = await userManager.FindByIdAsync(stored.UserId.ToString())
            ?? throw new UnauthorizedException();

        // Yeni token üret ama henüz kaydetme — önce eskiyi atomik biçimde iptal edeceğiz.
        var response = await CreateAuthResponseAsync(user, ct, persist: false);
        var newHash = TokenService.HashRefreshToken(response.RefreshToken);

        // ATOMİK ROTASYON (K6): aynı token'la eşzamanlı iki istek gelirse
        // Postgres satır kilidi sayesinde koşula uyan sadece BİRİ güncellenir.
        // stored.RevokedAt = now; SaveChangesAsync() ile yazılırsa iki isteğin de
        // ön kontrolleri geçip ikisinin de yeni token alması mümkün olur —
        // reuse detection o zaman asla tetiklenmez.
        var affected = await db.RefreshTokens
            .Where(t => t.Token == hash && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(t => t.RevokedAt, now)
                .SetProperty(t => t.ReplacedByToken, newHash), ct);

        if (affected == 0)
            throw new UnauthorizedException("Geçersiz yenileme anahtarı.");

        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            Token = newHash,
            ExpiresAt = response.RefreshTokenExpiresAt,
            CreatedAt = now
        });

        await db.SaveChangesAsync(ct);
        return response;
    }

    // ─────────────────────────────────────────────────────────
    // Çıkış
    // ─────────────────────────────────────────────────────────
    public async Task LogoutAsync(string refreshToken, CancellationToken ct)
    {
        var hash = TokenService.HashRefreshToken(refreshToken);
        var stored = await db.RefreshTokens
            .FirstOrDefaultAsync(t => t.Token == hash, ct);

        // Olmayan token için de 204 dön. "Bu token var mıydı?" bilgisini vermenin
        // bir faydası yok, zararı olabilir.
        if (stored is null || stored.IsRevoked) return;

        stored.RevokedAt = clock.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct);
    }

    // ─────────────────────────────────────────────────────────
    // Şifre sıfırlama
    // ─────────────────────────────────────────────────────────
    public async Task ForgotPasswordAsync(string emailAddress, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(emailAddress);

        // Kullanıcı yoksa bile 204 dön ve hiçbir şey yapma.
        // Aksi hâlde "bu e-posta kayıtlı mı" sorgusu için endpoint sağlamış olursun.
        if (user is null) return;

        var token = await userManager.GeneratePasswordResetTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = webOptions.Value.BaseUrl;

        await TrySendEmailAsync(
            user.Email!,
            "Şifre sıfırlama",
            $"""
             <p>Merhaba {user.FirstName},</p>
             <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın:</p>
             <p><a href="{baseUrl}/sifre-sifirla?email={Uri.EscapeDataString(user.Email!)}&token={encoded}">Şifremi sıfırla</a></p>
             <p>Bu talebi siz yapmadıysanız bu maili yok sayın.</p>
             """,
            ct);
    }

    public async Task ResetPasswordAsync(ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new BusinessRuleException("Şifre sıfırlama isteği geçersiz.");

        var decoded = DecodeToken(request.Token);

        var result = await userManager.ResetPasswordAsync(user, decoded, request.NewPassword);
        if (!result.Succeeded)
            throw new BusinessRuleException(
                string.Join(" ", result.Errors.Select(e => e.Description)));

        // Şifre değişti → mevcut tüm oturumları kapat.
        // Şifresini "birileri girmiş olabilir" diye değiştiren kullanıcı,
        // o birilerinin hâlâ içeride olmasını beklemez.
        // RevokeAllTokensAsync ExecuteUpdateAsync kullanıyor; değişiklik zaten
        // veritabanına uygulanmış oluyor, ekstra SaveChangesAsync gereksiz.
        await RevokeAllTokensAsync(user.Id, clock.GetUtcNow().UtcDateTime, ct);
    }

    // ─────────────────────────────────────────────────────────
    // E-posta doğrulama
    // ─────────────────────────────────────────────────────────
    public async Task VerifyEmailAsync(VerifyEmailRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email)
            ?? throw new BusinessRuleException("Doğrulama isteği geçersiz.");

        var result = await userManager.ConfirmEmailAsync(user, DecodeToken(request.Token));
        if (!result.Succeeded)
            throw new BusinessRuleException("Doğrulama bağlantısı geçersiz veya süresi dolmuş.");
    }

    // ─────────────────────────────────────────────────────────
    // Admin: bir kullanıcının tüm oturumlarını kapat
    // ─────────────────────────────────────────────────────────
    public Task RevokeAllSessionsAsync(Guid userId, CancellationToken ct)
        => RevokeAllTokensAsync(userId, clock.GetUtcNow().UtcDateTime, ct);

    public async Task<UserDto> GetCurrentUserAsync(Guid userId, CancellationToken ct)
    {
        // Geçerli imzalı token'ın işaret ettiği kullanıcı yoksa (silinmiş),
        // 404 değil 401: "bu token artık geçerli değil" demek daha doğru.
        var user = await userManager.FindByIdAsync(userId.ToString())
            ?? throw new UnauthorizedException("Oturumunuz artık geçerli değil.");

        return await ToDtoAsync(user);
    }

    // ─────────────────────────────────────────────────────────
    // Yardımcılar
    // ─────────────────────────────────────────────────────────
    private async Task<AuthResponse> CreateAuthResponseAsync(
        ApplicationUser user, CancellationToken ct, bool persist = true)
    {
        var roles = await userManager.GetRolesAsync(user);
        var (accessToken, accessExpiry) = tokens.CreateAccessToken(user, roles);

        var refreshToken = tokens.CreateRefreshToken();
        var refreshExpiry = tokens.RefreshTokenExpiryUtc();

        if (persist)
        {
            db.RefreshTokens.Add(new RefreshToken
            {
                UserId = user.Id,
                Token = TokenService.HashRefreshToken(refreshToken),
                ExpiresAt = refreshExpiry,
                CreatedAt = clock.GetUtcNow().UtcDateTime
            });
            await db.SaveChangesAsync(ct);
        }

        return new AuthResponse(
            accessToken, accessExpiry, refreshToken, refreshExpiry,
            await ToDtoAsync(user, roles));
    }

    private async Task<UserDto> ToDtoAsync(ApplicationUser user, IList<string>? roles = null)
    {
        roles ??= await userManager.GetRolesAsync(user);
        return new UserDto(
            user.Id, user.Email!, user.FirstName ?? "", user.LastName ?? "",
            user.EmailConfirmed, roles.ToList());
    }

    private async Task RevokeAllTokensAsync(Guid userId, DateTime now, CancellationToken ct)
    {
        await db.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.RevokedAt, now), ct);
    }

    private async Task SendEmailConfirmationAsync(ApplicationUser user, CancellationToken ct)
    {
        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        var encoded = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
        var baseUrl = webOptions.Value.BaseUrl;

        await TrySendEmailAsync(
            user.Email!,
            "E-posta adresinizi doğrulayın",
            $"""
             <p>Merhaba {user.FirstName}, aramıza hoş geldiniz.</p>
             <p><a href="{baseUrl}/eposta-dogrula?email={Uri.EscapeDataString(user.Email!)}&token={encoded}">E-postamı doğrula</a></p>
             """,
            ct);
    }

    /// Mail gönderimi auth akışını asla kıramaz (K11). Bugün ConsoleEmailService
    /// hiç atmıyor, ama Faz 9'da SMTP hatası kayıt olmayı/şifre sıfırlamayı
    /// engellememeli — kullanıcı zaten oluşturuldu/token üretildi, mail yeniden
    /// gönderilebilir.
    private async Task TrySendEmailAsync(string to, string subject, string htmlBody, CancellationToken ct)
    {
        try
        {
            await email.SendAsync(to, subject, htmlBody, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Mail gönderilemedi. Alıcı: {To}, Konu: {Subject}", to, subject);
        }
    }

    private static string DecodeToken(string encoded)
    {
        try
        {
            return Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(encoded));
        }
        catch (FormatException)
        {
            throw new BusinessRuleException("Bağlantı geçersiz.");
        }
    }
}
