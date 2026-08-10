using Commerce.Api.Persistence.Identity;
using Hangfire.Dashboard;

namespace Commerce.Api.Common.Handlers;

/// Hangfire 1.8'in varsayılanı zaten LocalRequestsOnly (ölçüldü) — ama
/// container/reverse-proxy arkasında "yerel" tanımı güvenilmez.
///
/// TARAYICI SORUNU: proje yalnızca JWT bearer kullanıyor; tarayıcı adres
/// çubuğundan gelen istek Authorization başlığı TAŞIMAZ. Bu yüzden
/// Development'ta yerel isteklere de izin veriliyor, yoksa dashboard
/// geliştirme sırasında hiçbir şekilde açılamaz.
public sealed class HangfireDashboardAuthorizationFilter(bool yerelIsteklereIzinVer)
    : IDashboardAuthorizationFilter
{
    private static readonly LocalRequestsOnlyAuthorizationFilter Local = new();

    public bool Authorize(DashboardContext context)
    {
        var http = context.GetHttpContext();

        if (http.User.Identity?.IsAuthenticated == true && http.User.IsInRole(AppRoles.Admin))
            return true;

        return yerelIsteklereIzinVer && Local.Authorize(context);
    }
}
