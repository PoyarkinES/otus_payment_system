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

    [Fact]
    public async Task Post_Deposit_ThenRepeatWithSameKey_DoesNotDoubleBalance()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);
        var key = Guid.NewGuid();

        var first = await PostOperationAsync(client, wallet.Id, "deposit", 100m, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstWallet = await first.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100m, firstWallet!.Available);

        var second = await PostOperationAsync(client, wallet.Id, "deposit", 100m, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondWallet = await second.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100m, secondWallet!.Available);

        var get = await client.GetAsync($"/wallets/{wallet.Id}");
        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(100m, current!.Available);
    }

    [Fact]
    public async Task Post_Withdraw_ThenRepeatWithSameKey_DoesNotDoubleWithdraw()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);

        await PostOperationAsync(client, wallet.Id, "deposit", 100m, Guid.NewGuid());

        var key = Guid.NewGuid();
        var first = await PostOperationAsync(client, wallet.Id, "withdraw", 40m, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var firstWallet = await first.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(60m, firstWallet!.Available);

        var second = await PostOperationAsync(client, wallet.Id, "withdraw", 40m, key);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var secondWallet = await second.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(60m, secondWallet!.Available);
    }

    [Fact]
    public async Task Post_Deposit_SameKeyDifferentAmount_Returns409()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);
        var key = Guid.NewGuid();

        var first = await PostOperationAsync(client, wallet.Id, "deposit", 100m, key);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await PostOperationAsync(client, wallet.Id, "deposit", 200m, key);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Post_Withdraw_OnEmptyWallet_Returns409()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);

        var response = await PostOperationAsync(
            client, wallet.Id, "withdraw", 10m, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Post_Deposit_ForeignWallet_Returns403()
    {
        var ownerClient = CreateClient(Guid.NewGuid());
        var wallet = await CreateWalletAsync(ownerClient);

        var strangerClient = CreateClient(Guid.NewGuid());

        var response = await PostOperationAsync(
            strangerClient, wallet.Id, "deposit", 10m, Guid.NewGuid());

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Post_ParallelDeposits_SameKey_AppliesOnlyOnce()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);
        var key = Guid.NewGuid();

        var requests = Enumerable.Range(0, 5)
            .Select(_ => PostOperationAsync(client, wallet.Id, "deposit", 100m, key))
            .ToArray();

        var responses = await Task.WhenAll(requests);

        Assert.All(responses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));

        foreach (var response in responses)
            response.Dispose();

        var get = await client.GetAsync($"/wallets/{wallet.Id}");
        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();

        Assert.Equal(100m, current!.Available);
    }

    [Fact]
    public async Task Post_ParallelDeposits_DifferentKeys_WithRetry_AppliesAll()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);

        var keys = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();

        var requests = keys
            .Select(key => PostOperationAsync(
                client, wallet.Id, "deposit", 100m, key))
            .ToArray();

        var responses = await Task.WhenAll(requests);

        for (var i = 0; i < responses.Length; i++)
        {
            if (responses[i].StatusCode == HttpStatusCode.Conflict)
            {
                responses[i].Dispose();
                responses[i] = await PostOperationAsync(
                    client, wallet.Id, "deposit", 100m, keys[i]);
            }

            Assert.Equal(HttpStatusCode.OK, responses[i].StatusCode);
        }

        foreach (var response in responses)
            response.Dispose();

        var get = await client.GetAsync($"/wallets/{wallet.Id}");
        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();

        Assert.Equal(500m, current!.Available);
    }

    [Fact]
    public async Task Post_ParallelWithdrawals_DifferentKeys_WithRetry_AppliesAll()
    {
        var userId = Guid.NewGuid();
        var client = CreateClient(userId);
        var wallet = await CreateWalletAsync(client);
        await PostOperationAsync(client, wallet.Id, "deposit", 500m, Guid.NewGuid());
        var keys = Enumerable.Range(0, 5)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        var requests = keys
            .Select(key => PostOperationAsync(
                client, wallet.Id, "withdraw", 100m, key))
            .ToArray();
        var responses = await Task.WhenAll(requests);
        for (var i = 0; i < responses.Length; i++)
        {
            if (responses[i].StatusCode == HttpStatusCode.Conflict)
            {
                responses[i].Dispose();
                responses[i] = await PostOperationAsync(
                    client, wallet.Id, "withdraw", 100m, keys[i]);
            }
            Assert.Equal(HttpStatusCode.OK, responses[i].StatusCode);
        }
        foreach (var response in responses)
            response.Dispose();
        var get = await client.GetAsync($"/wallets/{wallet.Id}");
        var current = await get.Content.ReadFromJsonAsync<WalletResponse>();
        Assert.Equal(0m, current!.Available);
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

    private static async Task<WalletResponse> CreateWalletAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });
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
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/wallets/{walletId}/{operation}");

        request.Headers.Add("Idempotency-Key", idempotencyKey.ToString());
        request.Content = JsonContent.Create(new { amount });

        return await client.SendAsync(request);
    }


}
