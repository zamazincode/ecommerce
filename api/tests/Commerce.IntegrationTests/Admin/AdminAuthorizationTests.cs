using System.Net;
using System.Net.Http.Json;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests.Admin;

/// `/api/admin/*`'ın TAMAMI tek yetkilendirme noktasından geçiyor
/// (AdminEndpoints.MapAdminEndpoints). Yeni bir admin ucu eklendiğinde bu
/// listeye BİR satır eklenir — PLAN.md'nin "kural haline getir" isteği budur.
public class AdminAuthorizationTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// Tek kaynak: hem [Theory] verisi hem manuel döngü buradan besleniyor.
    private static readonly (string Method, string Url)[] Routes =
    [
        ("GET", "/api/admin/products"),
        ("GET", "/api/admin/products/1"),
        ("POST", "/api/admin/products"),
        ("PUT", "/api/admin/products/1"),
        ("DELETE", "/api/admin/products/1"),
        ("POST", "/api/admin/products/1/restore"),
        ("PATCH", "/api/admin/products/1/stock"),
        ("POST", "/api/admin/products/bulk-price"),
        ("GET", "/api/admin/categories"),
        ("POST", "/api/admin/categories"),
        ("PUT", "/api/admin/categories/1"),
        ("GET", "/api/admin/orders"),
        ("PATCH", "/api/admin/orders/ORD-TEST/status"),
        ("GET", "/api/admin/coupons"),
        ("POST", "/api/admin/coupons"),
        ("PATCH", "/api/admin/coupons/1"),
        ("GET", "/api/admin/dashboard"),
        ("GET", "/api/admin/reports/sales"),
        ("GET", "/api/admin/reports/top-searches"),
        ("GET", "/api/admin/audit-logs"),
        ("GET", "/api/admin/reviews"),
        ("PATCH", "/api/admin/reviews/1/approve"),
        ("DELETE", "/api/admin/reviews/1"),
        ("POST", "/api/admin/images/signature"),
        ("GET", "/api/admin/products/1/images"),
        ("POST", "/api/admin/products/1/images"),
        ("DELETE", "/api/admin/product-images/1")
    ];

    public static TheoryData<string, string> AdminRoutes()
    {
        var data = new TheoryData<string, string>();
        foreach (var (method, url) in Routes)
            data.Add(method, url);
        return data;
    }

    [Theory]
    [MemberData(nameof(AdminRoutes))]
    public async Task AdminRoute_WithoutToken_Returns401(string method, string url)
    {
        var response = await SendAsync(method, url);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task AllAdminRoutes_WithCustomerToken_Return403()
    {
        await AuthenticateAsync("musteri-admin-yetki@test.com");

        var failing = new List<string>();
        foreach (var (method, url) in Routes)
        {
            var response = await SendAsync(method, url);
            if (response.StatusCode != HttpStatusCode.Forbidden)
                failing.Add($"{method} {url} -> {(int)response.StatusCode}");
        }

        // 25 ayrı test yerine tek Fact: 25 kullanıcı + 25 login + 25 Respawn
        // maliyetinden kaçınıyoruz. Hata mesajına HANGİ route'ların
        // başarısız olduğu yazılıyor.
        failing.ShouldBeEmpty($"403 dönmeyenler: {string.Join(", ", failing)}");
    }

    private async Task<HttpResponseMessage> SendAsync(string method, string url)
    {
        var httpMethod = new HttpMethod(method);
        var request = new HttpRequestMessage(httpMethod, url);

        // Gövde gerektiren route'larda boş bir JSON gönderiyoruz — yetkilendirme
        // middleware'i (UseAuthorization) model binding'den ÖNCE çalışır, o
        // yüzden gövdenin içeriği 401/403 sonucunu etkilemez.
        if (httpMethod != HttpMethod.Get && httpMethod != HttpMethod.Delete)
            request.Content = JsonContent.Create(new { });

        return await Client.SendAsync(request, Ct);
    }
}
