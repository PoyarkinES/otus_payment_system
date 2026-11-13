using System;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Gateway.Host.IntegrationTests;

public class WalletsGatewayE2ETests : IClassFixture<GatewayApiFactory>
{
    private readonly GatewayApiFactory _factory;

    public WalletsGatewayE2ETests(GatewayApiFactory factory) => _factory = factory;

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.Gateway.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    [Fact]
    public async Task Post_CreateWallet_ThroughGateway_Returns201_AndGetReturns200()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);

        // POST /api/wallets → Gateway → Wallets (in-process) → Postgres → 201
        var created = await client.PostAsJsonAsync("/api/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal("RUB", wallet!.Currency);

        // GET /api/wallets/{id} → Gateway → Wallets → 200
        var get = await client.GetAsync($"/api/wallets/{wallet.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Post_DuplicateCurrency_ThroughGateway_Returns409()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);

        var first = await client.PostAsJsonAsync("/api/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_ForeignWallet_ThroughGateway_Returns403()
    {
        var ownerClient = CreateClient(Guid.NewGuid());
        var created = await ownerClient.PostAsJsonAsync("/api/wallets", new { currency = "RUB" });
        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();

        var strangerClient = CreateClient(Guid.NewGuid());
        var get = await strangerClient.GetAsync($"/api/wallets/{wallet!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Fact]
    public async Task Get_MissingWallet_ThroughGateway_Returns404()
    {
        var client = CreateClient(Guid.NewGuid());
        var get = await client.GetAsync($"/api/wallets/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutAuth_ThroughGateway_Returns401()
    {
        var client = _factory.Gateway.CreateClient();
        var response = await client.PostAsJsonAsync("/api/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private sealed record WalletResponse(
        Guid Id,
        Guid UserId,
        string Currency,
        decimal Available,
        decimal Held,
        decimal Total,
        string Status,
        int Version,
        DateTimeOffset CreatedAt,
        DateTimeOffset UpdatedAt);
}
