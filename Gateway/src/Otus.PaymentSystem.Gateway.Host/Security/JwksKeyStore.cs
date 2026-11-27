using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Otus.PaymentSystem.Gateway.Host.Security;

/// <summary>
/// Кэш публичных ключей Identity (JWKS). Gateway проверяет подпись локально,
/// ключи обновляются по TTL или при неизвестном kid.
/// </summary>
public sealed class JwksKeyStore : IDisposable
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(30);

    private readonly HttpClient _http;
    private readonly string _jwksUri;
    private readonly ILogger<JwksKeyStore> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);
    private IList<SecurityKey> _keys = Array.Empty<SecurityKey>();
    private long _loadedAtUtcTicks;
    private long _lastAttemptUtcTicks;

    public JwksKeyStore(HttpClient http, string jwksUri, ILogger<JwksKeyStore>? logger = null)
    {
        _http = http;
        _jwksUri = jwksUri;
        _logger = logger ?? NullLogger<JwksKeyStore>.Instance;
    }

    public IList<SecurityKey> Keys => Volatile.Read(ref _keys);

    public Task<bool> RefreshAsync() => RefreshAsync(CancellationToken.None);

    public async Task<bool> RefreshAsync(CancellationToken cancellationToken)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            Interlocked.Exchange(ref _lastAttemptUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            return await LoadKeysAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    public async Task<bool> RefreshIfNeededAsync(string? kid, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            if (!NeedsRefresh(kid))
                return true;

            long now = DateTimeOffset.UtcNow.UtcTicks;
            long lastAttempt = Interlocked.Read(ref _lastAttemptUtcTicks);
            if (lastAttempt != 0 && now - lastAttempt < RetryDelay.Ticks)
                return false;

            Interlocked.Exchange(ref _lastAttemptUtcTicks, now);
            return await LoadKeysAsync(cancellationToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private bool NeedsRefresh(string? kid)
    {
        long loadedAt = Interlocked.Read(ref _loadedAtUtcTicks);
        if (loadedAt == 0 || DateTimeOffset.UtcNow.UtcTicks - loadedAt >= Ttl.Ticks)
            return true;

        return kid is not null && Volatile.Read(ref _keys).All(key => key.KeyId != kid);
    }

    private async Task<bool> LoadKeysAsync(CancellationToken cancellationToken)
    {
        try
        {
            string json = await _http.GetStringAsync(_jwksUri, cancellationToken);
            List<SecurityKey> keys = new JsonWebKeySet(json).GetSigningKeys().ToList();
            if (keys.Count == 0)
            {
                _logger.LogWarning(
                    "JWKS refresh in Gateway key store returned no signing keys from {JwksUri}.",
                    _jwksUri);
                return false;
            }

            Volatile.Write(ref _keys, keys);
            Interlocked.Exchange(ref _loadedAtUtcTicks, DateTimeOffset.UtcNow.UtcTicks);
            return true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "JWKS refresh in Gateway key store failed for {JwksUri}; cached keys are retained.",
                _jwksUri);
            return false;
        }
    }

    public void Dispose()
    {
        _refreshLock.Dispose();
        _http.Dispose();
    }
}