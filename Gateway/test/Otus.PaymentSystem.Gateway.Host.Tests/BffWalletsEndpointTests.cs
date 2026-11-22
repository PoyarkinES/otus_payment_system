using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Otus.PaymentSystem.Gateway.Host.Endpoints;
using Otus.PaymentSystem.Gateway.Host.Middleware;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

public sealed class BffWalletsEndpointTests
{
    private static readonly Guid UserId = Guid.Parse("78caacac-28fb-43e9-99c5-67cbaf3560bb");
    private static readonly Guid RubWalletId = Guid.Parse("4e2ad42b-4c45-4617-a7cc-03b3cfa4ee53");
    private static readonly Guid SecondRubWalletId = Guid.Parse("be844e6e-f45d-42b5-b777-53ea4ad4b56d");
    private static readonly Guid UsdWalletId = Guid.Parse("37cda7ad-5821-4376-884a-f1191618a5ad");
    private const string Authorization = "Bearer access-token";
    private const string CorrelationId = "bff-correlation-1";

    [Fact]
    public async Task GetWalletsAsync_AggregatesWalletsAndOperations_AndForwardsHeaders()
    {
        var handler = new WalletsHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/wallets")
                return Json(HttpStatusCode.OK, Wallets());

            if (request.RequestUri.AbsolutePath.EndsWith($"/{RubWalletId:D}/operations", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, OperationsPage("TopUp", 1));

            if (request.RequestUri.AbsolutePath.EndsWith($"/{SecondRubWalletId:D}/operations", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, OperationsPage("Withdrawal", 2));

            if (request.RequestUri.AbsolutePath.EndsWith($"/{UsdWalletId:D}/operations", StringComparison.Ordinal))
                return Json(HttpStatusCode.OK, OperationsPage("TopUp", 3, "USD"));

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var context = CreateContext();

        (int status, string body) = await CallEndpointAsync(context, handler, recentOperations: null);

        Assert.Equal(StatusCodes.Status200OK, status);
        using JsonDocument document = JsonDocument.Parse(body);
        JsonElement root = document.RootElement;
        JsonElement wallets = root.GetProperty("wallets");
        Assert.Equal(3, wallets.GetArrayLength());
        Assert.Equal(1, wallets[0].GetProperty("recentOperations").GetArrayLength());
        Assert.Equal("TopUp", wallets[0].GetProperty("recentOperations")[0].GetProperty("type").GetString());
        Assert.Equal(10m, wallets[0].GetProperty("recentOperations")[0].GetProperty("amount").GetDecimal());
        Assert.Equal("RUB", wallets[0].GetProperty("recentOperations")[0].GetProperty("currency").GetString());
        Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"),
            wallets[0].GetProperty("recentOperations")[0].GetProperty("referenceId").GetGuid());
        Assert.Equal(11, wallets[0].GetProperty("recentOperations")[0].GetProperty("balanceBefore").GetDecimal());
        Assert.Equal(21, wallets[0].GetProperty("recentOperations")[0].GetProperty("balanceAfter").GetDecimal());

        JsonElement totals = root.GetProperty("totals");
        Assert.Equal(2, totals.GetArrayLength());
        JsonElement rubTotal = Assert.Single(totals.EnumerateArray(), total => total.GetProperty("currency").GetString() == "RUB");
        Assert.Equal(130m, rubTotal.GetProperty("available").GetDecimal());
        Assert.Equal(30m, rubTotal.GetProperty("held").GetDecimal());
        Assert.Equal(160m, rubTotal.GetProperty("total").GetDecimal());

        Assert.Equal(4, handler.Requests.Count);
        Assert.All(handler.Requests, request =>
        {
            Assert.Equal(Authorization, request.Authorization);
            Assert.Equal(UserId.ToString("D"), request.UserId);
            Assert.Equal(CorrelationId, request.CorrelationId);
        });
        Assert.All(handler.Requests.Where(request => request.Path.Contains("/operations", StringComparison.Ordinal)), request =>
        {
            Assert.Contains("page=1", request.Path);
            Assert.Contains("pageSize=5", request.Path);
        });
    }

    [Fact]
    public async Task GetWalletsAsync_ZeroRecentOperations_DoesNotRequestHistory()
    {
        var handler = new WalletsHandler(_ => Json(HttpStatusCode.OK, Wallets()));

        (int status, string body) = await CallEndpointAsync(CreateContext(), handler, recentOperations: 0);

        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(handler.Requests);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.All(document.RootElement.GetProperty("wallets").EnumerateArray(), wallet =>
            Assert.Empty(wallet.GetProperty("recentOperations").EnumerateArray()));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(21)]
    public async Task GetWalletsAsync_InvalidRecentOperations_Returns400WithoutCallingWallets(int value)
    {
        var handler = new WalletsHandler(_ => throw new InvalidOperationException("Wallets must not be called."));

        (int status, _) = await CallEndpointAsync(CreateContext(), handler, value);

        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task GetWalletsAsync_UpstreamError_Returns502()
    {
        var handler = new WalletsHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        (int status, _) = await CallEndpointAsync(CreateContext(), handler, recentOperations: null);

        Assert.Equal(StatusCodes.Status502BadGateway, status);
    }

    [Fact]
    public async Task GetWalletsAsync_HistoryRequestFails_Returns502WithoutPartialSummary()
    {
        var handler = new WalletsHandler(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/wallets")
                return Json(HttpStatusCode.OK, Wallets());

            return request.RequestUri.AbsolutePath.EndsWith($"/{UsdWalletId:D}/operations", StringComparison.Ordinal)
                ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                : Json(HttpStatusCode.OK, OperationsPage("TopUp", 1));
        });

        (int status, string body) = await CallEndpointAsync(CreateContext(), handler, recentOperations: 1);

        Assert.Equal(StatusCodes.Status502BadGateway, status);
        Assert.DoesNotContain("recentOperations", body);
        Assert.Equal(4, handler.Requests.Count);
    }

    [Fact]
    public async Task GetWalletsAsync_UpstreamUnavailable_Returns503()
    {
        var handler = new WalletsHandler(_ => throw new HttpRequestException("connection refused"));

        (int status, _) = await CallEndpointAsync(CreateContext(), handler, recentOperations: null);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task GetWalletsAsync_UpstreamTimeout_Returns503()
    {
        var handler = new WalletsHandler(_ => throw new TaskCanceledException("request timed out"));

        (int status, _) = await CallEndpointAsync(CreateContext(), handler, recentOperations: null);

        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task GetWalletsAsync_EmptyList_ReturnsEmptyWalletsAndTotals()
    {
        var handler = new WalletsHandler(_ => Json(HttpStatusCode.OK, Array.Empty<WalletDto>()));

        (int status, string body) = await CallEndpointAsync(CreateContext(), handler, recentOperations: null);

        Assert.Equal(StatusCodes.Status200OK, status);
        using JsonDocument document = JsonDocument.Parse(body);
        Assert.Empty(document.RootElement.GetProperty("wallets").EnumerateArray());
        Assert.Empty(document.RootElement.GetProperty("totals").EnumerateArray());
        Assert.Single(handler.Requests);
    }

    private static async Task<(int Status, string Body)> CallEndpointAsync(
        HttpContext context,
        WalletsHandler handler,
        int? recentOperations)
    {
        IResult result = await BffEndpoints.GetWalletsAsync(
            recentOperations,
            context,
            new StubHttpClientFactory(handler),
            NullLoggerFactory.Instance,
            CancellationToken.None);

        var resultContext = new DefaultHttpContext
        {
            RequestServices = new ServiceCollection().AddOptions().AddLogging().BuildServiceProvider()
        };
        resultContext.Response.Body = new MemoryStream();
        await result.ExecuteAsync(resultContext);
        resultContext.Response.Body.Position = 0;
        using var reader = new StreamReader(resultContext.Response.Body);
        return (resultContext.Response.StatusCode, await reader.ReadToEndAsync());
    }

    private static DefaultHttpContext CreateContext()
    {
        var context = new DefaultHttpContext();
        context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", UserId.ToString("D"))], "Test"));
        context.Request.Headers.Authorization = Authorization;
        context.Request.Headers[CorrelationIdMiddleware.HeaderName] = CorrelationId;
        return context;
    }

    private static WalletDto[] Wallets() =>
    [
        new(RubWalletId, "RUB", 70m, 30m, 100m, "Active"),
        new(SecondRubWalletId, "RUB", 60m, 0m, 60m, "Active"),
        new(UsdWalletId, "USD", 5m, 0m, 5m, "Active")
    ];

    private static OperationsPageDto OperationsPage(string type, int seed, string currency = "RUB") =>
        new(
            [new OperationDto(
                Guid.Parse($"00000000-0000-0000-0000-{seed:D12}"),
                Guid.Parse($"10000000-0000-0000-0000-{seed:D12}"),
                type,
                10m,
                currency,
                21m,
                11m,
                null,
                DateTimeOffset.UnixEpoch)],
            1,
            5,
            1,
            1);

    private static HttpResponseMessage Json(HttpStatusCode status, object value) =>
        new(status) { Content = JsonContent.Create(value) };

    private sealed record WalletDto(Guid Id, string Currency, decimal Available, decimal Held, decimal Total, string Status);

    private sealed record OperationDto(
        Guid Id,
        Guid? ReferenceId,
        string Type,
        decimal Amount,
        string Currency,
        decimal BalanceAfter,
        decimal BalanceBefore,
        string? Description,
        DateTimeOffset CreatedAt);

    private sealed record OperationsPageDto(
        IReadOnlyCollection<OperationDto> Items,
        int Page,
        int PageSize,
        int TotalCount,
        int TotalPages);

    private sealed record RequestSnapshot(string Path, string? Authorization, string? UserId, string? CorrelationId);

    private sealed class WalletsHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public ConcurrentBag<RequestSnapshot> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            request.Headers.TryGetValues("X-User-Id", out IEnumerable<string>? userId);
            request.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out IEnumerable<string>? correlationId);
            Requests.Add(new RequestSnapshot(
                request.RequestUri!.PathAndQuery,
                request.Headers.Authorization?.ToString(),
                userId?.SingleOrDefault(),
                correlationId?.SingleOrDefault()));
            return Task.FromResult(respond(request));
        }
    }

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
        {
            Assert.Equal(BffEndpoints.WalletsClientName, name);
            return new HttpClient(handler, disposeHandler: false)
            {
                BaseAddress = new Uri("http://wallets.test/")
            };
        }
    }
}