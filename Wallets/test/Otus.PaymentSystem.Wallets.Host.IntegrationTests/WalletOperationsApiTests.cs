using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Wallets.Host.IntegrationTests;

public class WalletOperationsApiTests : IClassFixture<WalletApiFactory>
{
    private readonly WalletApiFactory _factory;

    public WalletOperationsApiTests(WalletApiFactory factory) => _factory = factory;

    [Fact]
    public async Task GetWalletOperations_AfterDepositAndWithdraw_ReturnsOrderedDetails()
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);
        var depositReferenceId = Guid.NewGuid();
        var withdrawalReferenceId = Guid.NewGuid();

        using var deposit = await PostOperationAsync(client, wallet.Id, "deposit", 100m, depositReferenceId);
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        using var withdrawal = await PostOperationAsync(client, wallet.Id, "withdraw", 40m, withdrawalReferenceId);
        Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);

        var response = await client.GetAsync($"/wallets/{wallet.Id}/operations");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<OperationsPageResponse>();

        Assert.NotNull(page);
        Assert.Equal(2, page!.TotalCount);
        Assert.Equal(2, page.Items.Count);
        var withdrawalOperation = Assert.Single(
            page.Items,
            operation => operation.ReferenceId == withdrawalReferenceId);
        Assert.Equal("Withdrawal", withdrawalOperation.Type);
        Assert.Equal(40m, withdrawalOperation.Amount);
        Assert.Equal(100m, withdrawalOperation.BalanceBefore);
        Assert.Equal(60m, withdrawalOperation.BalanceAfter);
        var depositOperation = Assert.Single(
            page.Items,
            operation => operation.ReferenceId == depositReferenceId);
        Assert.Equal("TopUp", depositOperation.Type);
        Assert.Equal(100m, depositOperation.Amount);
        Assert.Equal(0m, depositOperation.BalanceBefore);
        Assert.Equal(100m, depositOperation.BalanceAfter);
        Assert.True(page.Items[0].CreatedAt >= page.Items[1].CreatedAt);
    }

    [Fact]
    public async Task GetWalletOperations_SecondPage_ReturnsRemainingOperation()
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);
        var referenceIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };

        foreach (var referenceId in referenceIds)
        {
            using var deposit = await PostOperationAsync(client, wallet.Id, "deposit", 10m, referenceId);
            Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        }

        var response = await client.GetAsync($"/wallets/{wallet.Id}/operations?page=2&pageSize=2");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<OperationsPageResponse>();

        Assert.NotNull(page);
        Assert.Equal(2, page!.Page);
        Assert.Equal(2, page.PageSize);
        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Contains(page.Items[0].ReferenceId!.Value, referenceIds);
    }

    [Fact]
    public async Task GetWalletOperations_FiltersBeforePaginating()
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);
        using var deposit = await PostOperationAsync(client, wallet.Id, "deposit", 100m, Guid.NewGuid());
        using var withdrawal = await PostOperationAsync(client, wallet.Id, "withdraw", 40m, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, withdrawal.StatusCode);

        var from = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-1).ToString("yyyy-MM-dd");
        var to = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        var response = await client.GetAsync(
            $"/wallets/{wallet.Id}/operations?type=TopUp&from={from}&to={to}&pageSize=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<OperationsPageResponse>();

        Assert.NotNull(page);
        Assert.Equal(1, page!.TotalCount);
        Assert.Equal(1, page.TotalPages);
        Assert.Equal("TopUp", Assert.Single(page.Items).Type);

        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1).ToString("yyyy-MM-dd");
        var futureResponse = await client.GetAsync(
            $"/wallets/{wallet.Id}/operations?from={futureDate}&to={futureDate}");
        Assert.Equal(HttpStatusCode.OK, futureResponse.StatusCode);
        var futurePage = await futureResponse.Content.ReadFromJsonAsync<OperationsPageResponse>();
        Assert.NotNull(futurePage);
        Assert.Empty(futurePage!.Items);
        Assert.Equal(0, futurePage.TotalCount);
    }

    [Theory]
    [InlineData("?type=NotAnOperation")]
    [InlineData("?from=2026-02-02&to=2026-02-01")]
    public async Task GetWalletOperations_InvalidFilters_Returns400(string query)
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);

        var response = await client.GetAsync($"/wallets/{wallet.Id}/operations{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletOperations_PageBeyondEnd_ReturnsEmptyItemsAndRealTotalCount()
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);
        using var firstDeposit = await PostOperationAsync(client, wallet.Id, "deposit", 10m, Guid.NewGuid());
        using var secondDeposit = await PostOperationAsync(client, wallet.Id, "deposit", 20m, Guid.NewGuid());
        Assert.Equal(HttpStatusCode.OK, firstDeposit.StatusCode);
        Assert.Equal(HttpStatusCode.OK, secondDeposit.StatusCode);

        var response = await client.GetAsync($"/wallets/{wallet.Id}/operations?page=3&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var page = await response.Content.ReadFromJsonAsync<OperationsPageResponse>();

        Assert.NotNull(page);
        Assert.Empty(page!.Items);
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
    }

    [Theory]
    [InlineData("?page=0")]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    public async Task GetWalletOperations_InvalidPagination_Returns400(string query)
    {
        var client = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(client);

        var response = await client.GetAsync($"/wallets/{wallet.Id}/operations{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletOperations_ForeignWallet_Returns403()
    {
        var ownerClient = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(ownerClient);
        var strangerClient = CreateClient(Guid.NewGuid());

        var response = await strangerClient.GetAsync($"/wallets/{wallet.Id}/operations");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletOperations_MissingWallet_Returns404()
    {
        var client = CreateClient(Guid.NewGuid());

        var response = await client.GetAsync($"/wallets/{Guid.NewGuid()}/operations");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task GetWalletOperations_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync($"/wallets/{Guid.NewGuid()}/operations");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetWallets_ReturnsOnlyCurrentUsersWallets()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var firstWallet = await CreateWalletAsync(client, "RUB");
        var otherClient = CreateClient(Guid.NewGuid());
        await CreateWalletAsync(otherClient, "RUB");

        var response = await client.GetAsync("/wallets");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<List<WalletResponse>>();

        Assert.NotNull(wallets);
        Assert.Single(wallets);
        Assert.All(wallets, wallet => Assert.Equal(userId, wallet.UserId));
        Assert.Contains(wallets, wallet => wallet.Id == firstWallet.Id);
    }

    [Fact]
    public async Task GetWallets_UserWithoutWallets_ReturnsEmptyArray()
    {
        var client = CreateClient(Guid.NewGuid());

        var response = await client.GetAsync("/wallets");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var wallets = await response.Content.ReadFromJsonAsync<List<WalletResponse>>();
        Assert.NotNull(wallets);
        Assert.Empty(wallets!);
    }

    [Fact]
    public async Task GetWallets_WithoutAuth_Returns401()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/wallets");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private HttpClient CreateClient(Guid userId)
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
        return client;
    }

    private static async Task<WalletResponse> CreateWalletAsync(HttpClient client, string currency = "RUB")
    {
        var response = await client.PostAsJsonAsync("/wallets", new { currency });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<WalletResponse>())!;
    }

    private static async Task<HttpResponseMessage> PostOperationAsync(
        HttpClient client,
        Guid walletId,
        string operation,
        decimal amount,
        Guid idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/wallets/{walletId}/{operation}");
        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        request.Content = JsonContent.Create(new { amount });
        return await client.SendAsync(request);
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

    private sealed record OperationsPageResponse(
        List<OperationResponse> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record OperationResponse(
        Guid Id,
        Guid? ReferenceId,
        string Type,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        decimal BalanceBefore,
        string? Description,
        DateTimeOffset CreatedAt);
}