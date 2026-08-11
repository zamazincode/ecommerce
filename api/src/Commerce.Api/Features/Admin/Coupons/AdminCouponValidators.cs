using System.Text.RegularExpressions;
using Commerce.Domain.Common;
using FluentValidation;

namespace Commerce.Api.Features.Admin.Coupons;

public sealed class CreateCouponRequestValidator : AbstractValidator<CreateCouponRequest>
{
    public CreateCouponRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty()
            .Length(3, 50)
            // Kod servis tarafında ToUpperInvariant() ile normalize edildikten
            // sonra kaydediliyor — kontrol burada BÜYÜK/küçük harf duyarsız.
            // CultureInvariant ŞART: sunucu tr-TR kültüründe çalışırken salt
            // IgnoreCase, küçük 'i'yi 'İ' (noktalı büyük I) ile eşleştiriyor —
            // ASCII [A-Z]'deki 'I' ile eşleşmiyor ve "i" harfi geçen HER kod
            // (mesela "yenikupon") sessizce 400 dönüyor (ölçüldü).
            .Matches("^[A-Z0-9_-]+$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            .WithMessage("Kupon kodu yalnızca harf, rakam, '_' ve '-' içerebilir.");

        RuleFor(x => x.Value).GreaterThan(0);
        RuleFor(x => x.Value)
            .LessThanOrEqualTo(100)
            .When(x => x.Type == CouponType.Percentage)
            .WithMessage("Yüzdesel kupon değeri 100'ü geçemez.");

        RuleFor(x => x.MinCartTotal).GreaterThanOrEqualTo(0);
        RuleFor(x => x.ValidTo).GreaterThan(x => x.ValidFrom);
        RuleFor(x => x.UsageLimit).GreaterThan(0).When(x => x.UsageLimit.HasValue);
    }
}

public sealed class UpdateCouponStatusRequestValidator : AbstractValidator<UpdateCouponStatusRequest>
{
    // IsActive bool — kural gerektirmiyor; WithValidation<T>() gövde biçimini
    // (eksik/bozuk JSON) yine de 400'e çevirsin diye validator kayıtlı.
}
