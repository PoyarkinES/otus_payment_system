using System;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Wallets.Host.IntegrationTests;

public class WalletsApiTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public WalletsApiTests(WalletApiFactory factory) => _factory = factory;

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    [Fact]
    public async Task Post_CreateWallet_Returns201_AndGetReturns200()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);

        var created = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);
        Assert.Equal("RUB", wallet!.Currency);
        Assert.Equal(0m, wallet.Available);
        Assert.Equal(0m, wallet.Held);

        var get = await client.GetAsync($"/wallets/{wallet.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
    }

    [Fact]
    public async Task Post_DuplicateCurrency_Returns409()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);

        var first = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Get_ForeignWallet_Returns403()
    {
        var ownerId = Guid.NewGuid();
        var ownerClient = CreateClient(ownerId);

        var created = await ownerClient.PostAsJsonAsync("/wallets", new { currency = "RUB" });
        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();

        var strangerClient = CreateClient(Guid.NewGuid());
        var get = await strangerClient.GetAsync($"/wallets/{wallet!.Id}");

        Assert.Equal(HttpStatusCode.Forbidden, get.StatusCode);
    }

    [Fact]
    public async Task Get_MissingWallet_Returns404()
    {
        var client = CreateClient(Guid.NewGuid());

        var get = await client.GetAsync($"/wallets/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, get.StatusCode);
    }

    [Fact]
    public async Task Post_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();
        // без X-Test-UserId → TestAuthHandler → Fail → 401

        var response = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });

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
