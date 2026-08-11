using Commerce.Api.Common.Caching;
using Commerce.Api.Common.Extensions;
using Commerce.Api.Common.Results;
using Commerce.Api.Features.Admin.Orders.Dtos;
using Commerce.Api.Features.Orders;
using Commerce.Api.Features.Orders.Dtos;
using Commerce.Api.Persistence;
using Commerce.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Commerce.Api.Features.Admin.Orders;

public sealed class AdminOrderService(AppDbContext db, OrderService orders, HybridCache cache)
{
    public async Task<PagedResult<AdminOrderListDto>> SearchAsync(
        AdminOrderFilterRequest filter, CancellationToken ct = default)
    {
        var from = filter.DateFrom?.StartOfDayUtc();
        var to = filter.DateTo?.EndOfDayExclusiveUtc();       // ÜST SINIR DIŞLAYICI (K5)
        var q = string.IsNullOrWhiteSpace(filter.Q) ? null : filter.Q.Trim();

        var query = db.Orders.AsNoTracking()
            .WhereIf(filter.Status.HasValue, o => o.Status == filter.Status!.Value)
            .WhereIf(from.HasValue, o => o.CreatedAt >= from!.Value)
            .WhereIf(to.HasValue, o => o.CreatedAt < to!.Value)
            .WhereIf(q is not null, o =>
                EF.Functions.ILike(o.OrderNumber, $"%{q}%") ||
                db.Users.Any(u => u.Id == o.UserId && u.Email != null && EF.Functions.ILike(u.Email, $"%{q}%")))
            .OrderByDescending(o => o.CreatedAt).ThenByDescending(o => o.Id);

        // EN SONDA projeksiyon (CLAUDE.md kuralı). Order'da User navigation YOK
        // (domain entity'leri sadece Guid UserId tutar) — ilişkili alt sorgu.
        return await query.Select(o => new AdminOrderListDto(
                o.Id, o.OrderNumber, o.Status, o.Total,
                db.Users.Where(u => u.Id == o.UserId).Select(u => u.Email).FirstOrDefault(),
                db.Users.Where(u => u.Id == o.UserId)
                    .Select(u => (u.FirstName ?? "") + " " + (u.LastName ?? "")).FirstOrDefault(),
                o.Items.Sum(i => i.Quantity), o.CreatedAt))
            .ToPagedResultAsync(filter.ToPageRequest(), ct);
    }

    /// Durum geçişi TAMAMEN OrderService.ChangeStatusAsync'e delege edilir —
    /// stok iadesi, durum makinesi ve kargo maili kuyruklaması orada zaten
    /// doğru yapılıyor; burada yeniden yazmak "eksik kopya" riski taşır (K4).
    public async Task<OrderDetailDto> UpdateStatusAsync(
        string orderNumber, OrderStatus newStatus, CancellationToken ct = default)
    {
        var result = await orders.ChangeStatusAsync(
            orderNumber, newStatus, expectedCurrentStatus: null, ct: ct);

        await cache.RemoveByTagAsync(CacheTags.Orders, ct);
        return result;
    }
}
