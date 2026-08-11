using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Commerce.Api.Common.OpenApi;

/// .NET 10'da OpenAPI belgesi kimlik şemalarını otomatik yazmıyor;
/// bu transformer olmadan Scalar'da "Authorize" kutusu çıkmaz.
internal sealed class BearerSecuritySchemeTransformer(
    IAuthenticationSchemeProvider schemeProvider) : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        var schemes = await schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == JwtBearerDefaults.AuthenticationScheme)) return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            In = ParameterLocation.Header,
            BearerFormat = "JWT",
            Description = "Login cevabındaki accessToken değerini yapıştırın."
        };

        // Belge (document) seviyesinde "Security" YAZILMIYOR — bilerek.
        // OpenAPI'de belge seviyesindeki security her operasyona VARSAYILAN
        // olarak miras geçer, operasyon kendi security'sini tanımlamadıkça.
        // Bu yüzden anonim endpoint'ler (login, register, logout, ürün
        // listesi...) de Scalar'da "Authorization: Bearer" istiyormuş gibi
        // görünürdü. Doğru gereksinim EndpointAuthorizationOperationTransformer
        // ile operasyon başına, gerçek [Authorize]/RequireAuthorization
        // durumuna bakılarak yazılıyor.
    }
}
