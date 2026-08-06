using System.Net;
using Commerce.IntegrationTests.Infrastructure;
using Shouldly;

namespace Commerce.IntegrationTests;

public class HealthCheckTests(DatabaseFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task GetHealth_WhenDatabaseIsUp_ReturnsHealthy()
    {
        // Act
        // TestContext.Current.CancellationToken: test iptal edilirse (timeout,
        // Ctrl+C) bekleyen HTTP çağrısı da hemen iptal olsun. xUnit v3 bunu
        // analyzer ile zorunlu tutuyor (xUnit1051).
        var response = await Client.GetAsync("/health", TestContext.Current.CancellationToken);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        body.ShouldContain("Healthy");
    }

    [Fact]
    public async Task GetUnknownRoute_Returns404()
    {
        var response = await Client.GetAsync(
            "/boyle-bir-yol-yok", TestContext.Current.CancellationToken);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
