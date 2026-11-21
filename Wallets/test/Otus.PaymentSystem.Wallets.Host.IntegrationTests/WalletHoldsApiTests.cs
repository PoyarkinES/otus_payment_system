using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Otus.PaymentSystem.Wallets.Infrastructure.Persistence;

namespace Otus.PaymentSystem.Wallets.Host.IntegrationTests
{
    public class WalletHoldsApiTests : IClassFixture<WalletApiFactory>
    {
        private readonly WalletApiFactory _factory;

        public WalletHoldsApiTests(WalletApiFactory factory) => _factory = factory;

        [Fact]
        public async Task Post_Hold_Returns200_AndUpdatesBalances()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var response = await PostHoldAsync(client, walletId, Guid.NewGuid(), 30m, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var hold = await response.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);
            Assert.NotEqual(Guid.Empty, hold!.Id);

            var balance = await GetWalletBalanceAsync(client, walletId);
            Assert.Equal(70m, balance.Available);
            Assert.Equal(30m, balance.Held);
        }

        [Fact]
        public async Task Post_Hold_SameTransferId_ReturnsExistingHold_AndDoesNotDoubleHold()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var transferId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();

            var first = await PostHoldAsync(client, walletId, transferId, 30m, recipientId);
            var firstHold = await first.Content.ReadFromJsonAsync<HoldResponse>();
            var second = await PostHoldAsync(client, walletId, transferId, 30m, recipientId);
            var secondHold = await second.Content.ReadFromJsonAsync<HoldResponse>();

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.OK, second.StatusCode);
            Assert.NotNull(firstHold);
            Assert.NotNull(secondHold);
            Assert.Equal(firstHold!.Id, secondHold!.Id);

            var balance = await GetWalletBalanceAsync(client, walletId);
            Assert.Equal(70m, balance.Available);
            Assert.Equal(30m, balance.Held);
        }

        [Fact]
        public async Task Post_Hold_SameTransferIdDifferentAmount_Returns409()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var transferId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();

            var first = await PostHoldAsync(client, walletId, transferId, 30m, recipientId);
            var second = await PostHoldAsync(client, walletId, transferId, 40m, recipientId);

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Post_Hold_SameTransferIdDifferentRecipient_Returns409()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var transferId = Guid.NewGuid();

            var first = await PostHoldAsync(client, walletId, transferId, 30m, Guid.NewGuid());
            var second = await PostHoldAsync(client, walletId, transferId, 30m, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        }

        [Fact]
        public async Task Post_Hold_InsufficientFunds_Returns409()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);

            var response = await PostHoldAsync(client, walletId, Guid.NewGuid(), 101m, Guid.NewGuid());

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        }

        [Fact]
        public async Task Post_Hold_ForeignWallet_Returns403()
        {
            var ownerClient = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(ownerClient);
            var strangerClient = CreateClient(Guid.NewGuid());

            var response = await PostHoldAsync(
                strangerClient,
                walletId,
                Guid.NewGuid(),
                30m,
                Guid.NewGuid());

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        [Fact]
        public async Task Post_Hold_WithoutAuth_Returns401()
        {
            var client = _factory.CreateClient();

            var response = await PostHoldAsync(
                client,
                Guid.NewGuid(),
                Guid.NewGuid(),
                30m,
                Guid.NewGuid());

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        [Fact]
        public async Task Post_ConfirmHold_TransfersFunds_AndRepeatDoesNotDoubleCredit()
        {
            var senderId = Guid.NewGuid();
            var recipientId = Guid.NewGuid();
            var client = CreateClient(senderId);
            var walletId = await CreateFundedWalletAsync(client);
            var placeHold = await PostHoldAsync(client, walletId, Guid.NewGuid(), 30m, recipientId);
            var hold = await placeHold.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);

            var firstConfirm = await PostHoldActionAsync(client, hold!.Id, "confirm");
            Assert.Equal(HttpStatusCode.NoContent, firstConfirm.StatusCode);

            var senderBalance = await GetWalletBalanceAsync(client, walletId);
            Assert.Equal(70m, senderBalance.Available);
            Assert.Equal(0m, senderBalance.Held);

            var recipientBalance = await GetRecipientBalanceAsync(recipientId);
            Assert.Equal(30m, recipientBalance.Available);

            var secondConfirm = await PostHoldActionAsync(client, hold.Id, "confirm");
            Assert.Equal(HttpStatusCode.NoContent, secondConfirm.StatusCode);

            recipientBalance = await GetRecipientBalanceAsync(recipientId);
            Assert.Equal(30m, recipientBalance.Available);
        }

        [Fact]
        public async Task Post_ConfirmHold_AfterRelease_Returns409()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var placeHold = await PostHoldAsync(client, walletId, Guid.NewGuid(), 30m, Guid.NewGuid());
            var hold = await placeHold.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);

            var release = await PostHoldActionAsync(client, hold!.Id, "release");
            var confirm = await PostHoldActionAsync(client, hold.Id, "confirm");

            Assert.Equal(HttpStatusCode.NoContent, release.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, confirm.StatusCode);
        }

        [Fact]
        public async Task Post_ReleaseHold_RestoresBalance_AndRepeatDoesNotChangeIt()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var placeHold = await PostHoldAsync(client, walletId, Guid.NewGuid(), 30m, Guid.NewGuid());
            var hold = await placeHold.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);

            var firstRelease = await PostHoldActionAsync(client, hold!.Id, "release");
            Assert.Equal(HttpStatusCode.NoContent, firstRelease.StatusCode);
            var firstBalance = await GetWalletBalanceAsync(client, walletId);
            Assert.Equal(100m, firstBalance.Available);
            Assert.Equal(0m, firstBalance.Held);

            var secondRelease = await PostHoldActionAsync(client, hold.Id, "release");
            Assert.Equal(HttpStatusCode.NoContent, secondRelease.StatusCode);
            var secondBalance = await GetWalletBalanceAsync(client, walletId);
            Assert.Equal(firstBalance.Available, secondBalance.Available);
            Assert.Equal(firstBalance.Held, secondBalance.Held);
        }

        [Fact]
        public async Task Post_ReleaseHold_AfterConfirm_Returns409()
        {
            var client = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(client);
            var placeHold = await PostHoldAsync(client, walletId, Guid.NewGuid(), 30m, Guid.NewGuid());
            var hold = await placeHold.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);

            var confirm = await PostHoldActionAsync(client, hold!.Id, "confirm");
            var release = await PostHoldActionAsync(client, hold.Id, "release");

            Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);
            Assert.Equal(HttpStatusCode.Conflict, release.StatusCode);
        }

        [Fact]
        public async Task Post_ConfirmHold_ForeignHold_Returns403()
        {
            var ownerClient = CreateClient(Guid.NewGuid());
            var walletId = await CreateFundedWalletAsync(ownerClient);
            var placeHold = await PostHoldAsync(ownerClient, walletId, Guid.NewGuid(), 30m, Guid.NewGuid());
            var hold = await placeHold.Content.ReadFromJsonAsync<HoldResponse>();
            Assert.NotNull(hold);

            var strangerClient = CreateClient(Guid.NewGuid());
            var response = await PostHoldActionAsync(strangerClient, hold!.Id, "confirm");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        private HttpClient CreateClient(Guid userId)
        {
            var client = _factory.CreateClient();
            client.DefaultRequestHeaders.Add("X-Test-UserId", userId.ToString());
            return client;
        }

        private async Task<Guid> CreateFundedWalletAsync(HttpClient client)
        {
            var create = await client.PostAsJsonAsync("/wallets", new { currency = "RUB" });
            create.EnsureSuccessStatusCode();
            var wallet = await create.Content.ReadFromJsonAsync<WalletIdResponse>();
            Assert.NotNull(wallet);

            using var depositRequest = new HttpRequestMessage(
                HttpMethod.Post,
                $"/wallets/{wallet!.Id}/deposit")
            {
                Content = JsonContent.Create(new { amount = 100m })
            };
            depositRequest.Headers.Add("Idempotency-Key", Guid.NewGuid().ToString());
            using var deposit = await client.SendAsync(depositRequest);
            Assert.Equal(HttpStatusCode.OK, deposit.StatusCode);

            return wallet.Id;
        }

        private static Task<HttpResponseMessage> PostHoldAsync(
            HttpClient client,
            Guid walletId,
            Guid transferId,
            decimal amount,
            Guid recipientId) =>
            client.PostAsJsonAsync(
                $"/wallets/{walletId}/holds",
                new { transferId, amount, recipientUserId = recipientId });

        private static Task<HttpResponseMessage> PostHoldActionAsync(
            HttpClient client,
            Guid holdId,
            string action) =>
            client.PostAsync($"/wallets/holds/{holdId}/{action}", content: null);

        private static async Task<WalletBalanceResponse> GetWalletBalanceAsync(HttpClient client, Guid walletId)
        {
            var response = await client.GetAsync($"/wallets/{walletId}");
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<WalletBalanceResponse>())!;
        }

        private async Task<WalletBalanceResponse> GetRecipientBalanceAsync(Guid recipientId)
        {
            using var scope = _factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<WalletsDbContext>();
            return await db.Wallets
                .Where(wallet => wallet.UserId == recipientId)
                .Select(wallet => new WalletBalanceResponse(wallet.Available, wallet.Held))
                .SingleAsync();
        }

        private sealed record WalletIdResponse(Guid Id);
        private sealed record HoldResponse(Guid Id);
        private sealed record WalletBalanceResponse(decimal Available, decimal Held);
    }
}
