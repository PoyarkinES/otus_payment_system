using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Gateway.Host.IntegrationTests;

/// <summary>
/// Фейковый Wallets.Host: принимает /wallets (POST) и /wallets/{id} (GET),
/// возвращает 201/200/403/404. Gateway проксирует сюда.
/// </summary>
public sealed class FakeWalletsHandler : HttpMessageHandler
{
    private readonly Dictionary<Guid, FakeWallet> _wallets = new();

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        string path = request.RequestUri?.AbsolutePath ?? string.Empty;

        if (request.Method == HttpMethod.Post && path == "/wallets")
        {
            string body = await new StreamReader(request.Content!.ReadAsStream(), Encoding.UTF8)
                .ReadToEndAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            string currency = doc.RootElement.GetProperty("currency").GetString() ?? "RUB";

            // userId приходит от Gateway: X-User-Id (если настроен) или sub из токена.
            // В тестах — из заголовка, который Gateway пробрасывает.
            string userIdHeader = request.Headers.TryGetValues("X-User-Id", out var vals)
                ? vals.FirstOrDefault() ?? string.Empty
                : string.Empty;

            var wallet = new FakeWallet(Guid.NewGuid(), userIdHeader, currency);
            _wallets[wallet.Id] = wallet;

            return Json(HttpStatusCode.Created, wallet);
        }

        if (request.Method == HttpMethod.Get && path.StartsWith("/wallets/"))
        {
            string idPart = path["/wallets/".Length..];
            if (!Guid.TryParse(idPart, out var id) || !_wallets.TryGetValue(id, out var wallet))
                return Json(HttpStatusCode.NotFound, new { detail = "Кошелёк не найден." });

            string requester = request.Headers.TryGetValues("X-User-Id", out var v)
                ? v.FirstOrDefault() ?? string.Empty
                : string.Empty;

            if (wallet.UserId != requester)
                return Json(HttpStatusCode.Forbidden, new { detail = "Доступ к чужому кошельку запрещён." });

            return Json(HttpStatusCode.OK, wallet);
        }

        return new HttpResponseMessage(HttpStatusCode.NotFound);
    }

    private static HttpResponseMessage Json(HttpStatusCode status, object value)
        => new(status)
        {
            Content = new StringContent(
                JsonSerializer.Serialize(value),
                Encoding.UTF8,
                "application/json")
        };

    private sealed record FakeWallet(Guid Id, string UserId, string Currency)
    {
        public decimal Available => 0m;
        public decimal Held => 0m;
        public decimal Total => 0m;
        public string Status => "Active";
        public int Version => 0;
        public DateTimeOffset CreatedAt => DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedAt => DateTimeOffset.UtcNow;
    }
}
