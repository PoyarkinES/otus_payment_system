using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Otus.PaymentSystem.Gateway.Host.Security;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;

namespace Otus.PaymentSystem.Gateway.Host.Tests;

public sealed class JwksKeyStoreTests
{
    [Fact]
    public async Task Keys_WhenJwksRequestIsPending_ReturnsWithoutWaitingForNetwork()
    {
        var response = new TaskCompletionSource<HttpResponseMessage>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var http = new HttpClient(new DeferredHttpMessageHandler(response.Task))
        {
            Timeout = Timeout.InfiniteTimeSpan
        };
        using var keyStore = new JwksKeyStore(http, "https://identity.test/.well-known/jwks.json");

        Task<IList<SecurityKey>> keys = Task.Run(() => keyStore.Keys);

        try
        {
            Task completed = await Task.WhenAny(keys, Task.Delay(TimeSpan.FromSeconds(1)));

            Assert.Same(keys, completed);
            Assert.Empty(await keys);
        }
        finally
        {
            response.TrySetResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"keys\":[]}")
            });
            await keys;
        }
    }

    [Fact]
    public async Task ValidateToken_WhenJwksIsUnavailable_LogsFailureAndReturnsSignatureError()
    {
        const string jwksUri = "https://identity.test/.well-known/jwks.json";
        var logger = new RecordingLogger();
        var handler = new FailingHttpMessageHandler();
        using var keyStore = new JwksKeyStore(
            new HttpClient(handler),
            jwksUri,
            logger);

        bool refreshed = await keyStore.RefreshIfNeededAsync("rotated-key");

        Assert.False(refreshed);
        Assert.Empty(keyStore.Keys);
        Assert.False(await keyStore.RefreshIfNeededAsync("rotated-key"));
        Assert.Equal(1, handler.RequestCount);
        Assert.Equal(LogLevel.Warning, logger.LogLevel);
        Assert.Contains("Gateway key store", logger.Message);
        Assert.Contains(jwksUri, logger.Message);
        Assert.IsType<HttpRequestException>(logger.Exception);

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false,
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256],
            IssuerSigningKeyResolver = (_, _, _, _) => keyStore.Keys
        };

        var exception = Assert.Throws<SecurityTokenSignatureKeyNotFoundException>(() =>
            new JwtSecurityTokenHandler().ValidateToken(CreateSignedToken(), validationParameters, out _));

        Assert.Contains("signature", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static string CreateSignedToken()
    {
        using RSA rsa = RSA.Create(2048);
        var key = new RsaSecurityKey(rsa) { KeyId = "rotated-key" };
        var token = new JwtSecurityToken(
            issuer: "test",
            audience: "test",
            claims: [new Claim("sub", "test-user")],
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.RsaSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private sealed class DeferredHttpMessageHandler(Task<HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) => response;
    }

    private sealed class FailingHttpMessageHandler : HttpMessageHandler
    {
        public int RequestCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            return Task.FromException<HttpResponseMessage>(new HttpRequestException("JWKS unavailable."));
        }
    }

    private sealed class RecordingLogger : ILogger<JwksKeyStore>
    {
        public LogLevel LogLevel { get; private set; }
        public string Message { get; private set; } = string.Empty;
        public Exception? Exception { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            LogLevel = logLevel;
            Message = formatter(state, exception);
            Exception = exception;
        }
    }
}