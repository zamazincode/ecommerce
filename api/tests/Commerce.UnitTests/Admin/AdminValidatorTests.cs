using Commerce.Api.Features.Admin.Coupons;
using Commerce.Api.Features.Admin.Products;
using Commerce.Api.Features.Admin.Products.Dtos;
using Commerce.Domain.Common;
using Shouldly;

namespace Commerce.UnitTests.Admin;

public class AdminValidatorTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // ═══════════════════════════════════════════════════════════
    // CreateProductRequestValidator
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateProductRequest_WithEmptyName_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("", null, null, 10m, null, 0, 1, null, null, null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProductRequest_WithZeroPrice_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Kitap", null, null, 0m, null, 0, 1, null, null, null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProductRequest_WithDiscountedPriceGreaterThanPrice_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Kitap", null, null, 50m, 60m, 0, 1, null, null, null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProductRequest_WithNegativeStock_IsInvalid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Kitap", null, null, 50m, null, -1, 1, null, null, null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateProductRequest_WithValidData_IsValid()
    {
        var validator = new CreateProductRequestValidator();
        var request = new CreateProductRequest("Kitap", "SKU-1", "Açıklama", 50m, 40m, 5, 1, null, null, true);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeTrue();
    }

    // ═══════════════════════════════════════════════════════════
    // BulkPriceUpdateRequestValidator
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task BulkPriceUpdateRequest_WithEmptyList_IsInvalid()
    {
        var validator = new BulkPriceUpdateRequestValidator();
        var request = new BulkPriceUpdateRequest([]);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task BulkPriceUpdateRequest_With501Items_IsInvalid()
    {
        var validator = new BulkPriceUpdateRequestValidator();
        var items = Enumerable.Range(1, 501)
            .Select(i => new BulkPriceUpdateItem(i, 10m, null))
            .ToList();
        var request = new BulkPriceUpdateRequest(items);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task BulkPriceUpdateRequest_WithDuplicateProductId_IsInvalid()
    {
        var validator = new BulkPriceUpdateRequestValidator();
        var request = new BulkPriceUpdateRequest([
            new BulkPriceUpdateItem(1, 10m, null),
            new BulkPriceUpdateItem(1, 20m, null)
        ]);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    // ═══════════════════════════════════════════════════════════
    // CreateCouponRequestValidator
    // ═══════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateCouponRequest_WithValidToBeforeValidFrom_IsInvalid()
    {
        var validator = new CreateCouponRequestValidator();
        var request = new CreateCouponRequest(
            "TEST10", CouponType.Percentage, 10m, 0m,
            new DateTime(2026, 9, 1), new DateTime(2026, 8, 1), null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateCouponRequest_PercentageOver100_IsInvalid()
    {
        var validator = new CreateCouponRequestValidator();
        var request = new CreateCouponRequest(
            "TEST10", CouponType.Percentage, 101m, 0m,
            new DateTime(2026, 8, 1), new DateTime(2026, 9, 1), null);

        var result = await validator.ValidateAsync(request, Ct);

        result.IsValid.ShouldBeFalse();
    }
}
