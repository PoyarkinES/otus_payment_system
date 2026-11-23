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

    [Fact]
    public async Task Get_BffWallets_ThroughGateway_ReturnsWalletAndRecentOperations()
    {
        using HttpClient client = CreateClient(Guid.NewGuid());
        using HttpResponseMessage created = await client.PostAsJsonAsync(
            "/api/wallets",
            new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        WalletResponse wallet = (await created.Content.ReadFromJsonAsync<WalletResponse>())!;
        using HttpResponseMessage deposit = await PostOperationAsync(
            client,
            wallet.Id,
            "deposit",
            100m,
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        using HttpResponseMessage withdrawal = await PostOperationAsync(
            client,
            wallet.Id,
            "withdraw",
            25m,
            Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);

        using HttpResponseMessage response = await client.GetAsync("/bff/wallets?recentOperations=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BffWalletsResponse? result = await response.Content.ReadFromJsonAsync<BffWalletsResponse>();

        Assert.NotNull(result);
        BffWalletResponse resultWallet = Assert.Single(result!.Wallets);
        Assert.Equal(wallet.Id, resultWallet.Id);
        Assert.Equal("RUB", resultWallet.Currency);
        Assert.Equal(75m, resultWallet.Available);
        Assert.Equal(75m, resultWallet.Total);
        Assert.Single(resultWallet.RecentOperations);
        BffCurrencyTotalResponse total = Assert.Single(result.Totals);
        Assert.Equal("RUB", total.Currency);
        Assert.Equal(75m, total.Available);
        Assert.Equal(75m, total.Total);
    }

    [Fact]
    public async Task Get_BffWallets_WithoutAuth_Returns401()
    {
        using HttpClient client = _factory.Gateway.CreateClient();

        using HttpResponseMessage response = await client.GetAsync("/bff/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_BffWallets_ForNewUser_ReturnsEmptyCollections()
    {
        using HttpClient client = CreateClient(Guid.NewGuid());

        using HttpResponseMessage response = await client.GetAsync("/bff/wallets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        BffWalletsResponse? result = await response.Content.ReadFromJsonAsync<BffWalletsResponse>();
        Assert.NotNull(result);
        Assert.Empty(result!.Wallets);
        Assert.Empty(result.Totals);
    }

    [Fact]
    public async Task Post_Deposit_ThroughGateway_Returns200_AndUpdatesBalance()
    {
        var client = CreateClient(Guid.NewGuid());
        var idempotencyKey = Guid.NewGuid();

        var created = await client.PostAsJsonAsync(
            "/api/wallets",
            new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);

        var deposit = await PostOperationAsync(client, wallet!.Id, "deposit", 100, idempotencyKey);
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);

        var depositResponse = await deposit.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100, depositResponse.Available);

        var get = await client.GetAsync($"/api/wallets/{wallet.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(current);
        Assert.Equal(100m, current!.Available);
    }

    [Fact]
    public async Task Post_Deposit_ThroughGateway_RepeatedKey_DoesNotDoubleBalance()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var idempotencyKey = Guid.NewGuid();

        var created = await client.PostAsJsonAsync(
            "/api/wallets",
            new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);

        var deposit1 = await PostOperationAsync(client, wallet!.Id, "deposit", 100, idempotencyKey);
        Assert.Equal(HttpStatusCode.OK, deposit1.StatusCode);
        var depositResponse = await deposit1.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100, depositResponse.Available);

        var deposit2 = await PostOperationAsync(client, wallet!.Id, "deposit", 100, idempotencyKey);
        Assert.Equal(HttpStatusCode.OK, deposit2.StatusCode);
        var deposit2Response = await deposit2.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100, deposit2Response.Available);

        var get = await client.GetAsync($"/api/wallets/{wallet.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(current);
        Assert.Equal(100m, current!.Available);
    }

    [Fact]
    public async Task Post_Withdraw_ThroughGateway_Returns200_AndUpdatesBalance()
    {
        var client = CreateClient(Guid.NewGuid());
        var created = await client.PostAsJsonAsync(
            "/api/wallets",
            new { currency = "RUB" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var wallet = await created.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(wallet);

        var depositKey = Guid.NewGuid();
        var deposit = await PostOperationAsync(client, wallet!.Id, "deposit", 100, depositKey);
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);

        var depositResponse = await deposit.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100, depositResponse.Available);

        var withdrawKey = Guid.NewGuid();
        var withdraw = await PostOperationAsync(client, wallet.Id, "withdraw", 40, withdrawKey);
        Assert.Equal(HttpStatusCode.OK, withdraw.StatusCode);

        var withdrawResponse = await withdraw.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(60, withdrawResponse.Available);

        var get = await client.GetAsync($"/api/wallets/{wallet.Id}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.NotNull(current);
        Assert.Equal(60, current!.Available);
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

    private sealed record BffWalletsResponse(
        IReadOnlyCollection<BffWalletResponse> Wallets,
        IReadOnlyCollection<BffCurrencyTotalResponse> Totals);

    private sealed record BffWalletResponse(
        Guid Id,
        string Currency,
        decimal Available,
        decimal Held,
        decimal Total,
        string Status,
        IReadOnlyCollection<BffOperationResponse> RecentOperations);

    private sealed record BffCurrencyTotalResponse(
        string Currency,
        decimal Available,
        decimal Held,
        decimal Total);

    private sealed record BffOperationResponse(
        Guid Id,
        Guid? ReferenceId,
        string Type,
        decimal Amount,
        string Currency,
        decimal BalanceBefore,
        decimal BalanceAfter,
        string? Description,
        DateTimeOffset CreatedAt);

    private static async Task<HttpResponseMessage> PostOperationAsync(HttpClient client, Guid walletId, string operation, decimal amount, Guid idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/wallets/{walletId}/{operation}");
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        request.Content = JsonContent.Create(new { amount });

        return await client.SendAsync(request);
    }
}
