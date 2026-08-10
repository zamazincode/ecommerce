using System.Net.Http.Json;
using Commerce.Api.Features.BackgroundJobs;
using Commerce.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Commerce.IntegrationTests.BackgroundJobs;

public class CartReminderJobsTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private async Task<int> SeedProductAsync(string name = "Test Kitabı", int stock = 20)
    {
        var id = 0;
        await ExecuteDbAsync(async db =>
        {
            var category = CatalogTestData.DefaultCategory();
            db.Categories.Add(category);
            var product = new ProductBuilder().WithName(name).WithStock(stock).InCategory(category).Build();
            db.Products.Add(product);
            await db.SaveChangesAsync();
            id = product.Id;
        });
        return id;
    }

    private async Task AddToCartAsync(int productId, int quantity = 1)
    {
        var response = await Client.PostAsJsonAsync("/api/cart/items", new { productId, quantity }, Ct);
        response.EnsureSuccessStatusCode();
    }

    private Task RunReminderAsync(Guid userId)
        => ExecuteScopedAsync(sp =>
            sp.GetRequiredService<CartReminderJobs>().SendReminderAsync(userId));

    [Fact]
    public async Task SendReminder_WithEmptyCart_SendsNothing()
    {
        var userId = await AuthenticateAsync();

        await RunReminderAsync(userId);

        Factory.EmailService.SentEmails.ShouldBeEmpty();
        var reminderSentAt = await ExecuteDbAsync(db =>
            db.Carts.Where(c => c.UserId == userId).Select(c => c.ReminderSentAt).FirstOrDefaultAsync(Ct));
        reminderSentAt.ShouldBeNull();
    }

    [Fact]
    public async Task SendReminder_WithItems_SendsOneMailListingProducts()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync("Simyacı");
        await AddToCartAsync(productId);

        await RunReminderAsync(userId);

        Factory.EmailService.SentEmails.Count.ShouldBe(1);
        Factory.EmailService.SentEmails.ShouldContain(e => e.Body.Contains("Simyacı"));

        var reminderSentAt = await ExecuteDbAsync(db =>
            db.Carts.Where(c => c.UserId == userId).Select(c => c.ReminderSentAt).FirstAsync(Ct));
        reminderSentAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task SendReminder_RunTwice_SendsOnlyOnce()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync();
        await AddToCartAsync(productId);

        await RunReminderAsync(userId);
        await RunReminderAsync(userId);

        // K6 tekilleştirmesi: sepet mailden sonra değişmediyse ikinci hatırlatma
        // hak edilmemiştir.
        Factory.EmailService.SentEmails.Count.ShouldBe(1);
    }

    [Fact]
    public async Task SendReminder_AfterCartChangedAgain_SendsSecondMail()
    {
        var userId = await AuthenticateAsync();
        var productId = await SeedProductAsync();
        await AddToCartAsync(productId);

        await RunReminderAsync(userId);
        Factory.EmailService.SentEmails.Count.ShouldBe(1);

        // Sepet ReminderSentAt'ten SONRA değişti (UpdatedAt ilerledi) — yeni
        // hatırlatma yeniden hak edilir.
        await AddToCartAsync(productId, quantity: 2);
        await RunReminderAsync(userId);

        Factory.EmailService.SentEmails.Count.ShouldBe(2);
    }
}
