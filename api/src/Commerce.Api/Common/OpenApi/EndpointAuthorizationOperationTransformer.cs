using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Commerce.Api.Common.OpenApi;

/// Her endpoint'in OpenAPI belgesinde SADECE gerçekten koruduğu kadar
/// "Authorization gerekiyor" göstermesini sağlar. BearerSecuritySchemeTransformer
/// bunu belge seviyesinde yapsaydı (eskiden yapıyordu), her operasyon bunu miras
/// alır ve Scalar login/register/logout gibi anonim endpoint'lerde bile
/// "Authorization: Bearer ..." istermiş gibi kod örneği/deneme kutusu gösterirdi.
internal sealed class EndpointAuthorizationOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken ct)
    {
        var metadata = context.Description.ActionDescriptor.EndpointMetadata;

        // [Authorize]/RequireAuthorization eklenmiş AMA sonradan AllowAnonymous
        // ile açılmış endpoint'ler de olabilir (bu projede yok ama genel kural) —
        // asıl çalışma zamanı davranışıyla aynı sırayı izliyoruz.
        var requiresAuth = metadata.OfType<IAuthorizeData>().Any()
            && !metadata.OfType<IAllowAnonymous>().Any();

        // OpenApiOperation.Security, belge seviyesindeki varsayılanı EZER.
        // Boş liste = "bu operasyon için güvenlik gerekmiyor" — anonim
        // endpoint'lerde bunu AÇIKÇA yazmak, miras almaya güvenmekten daha
        // güvenilir.
        operation.Security = requiresAuth
            ?
            [
                new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
                }
            ]
            : [];

        return Task.CompletedTask;
    }
}
