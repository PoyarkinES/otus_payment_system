using System.Text.Json;
using Otus.PaymentSystem.Identity.Application.Queries.GetProfile;
using Otus.PaymentSystem.Identity.Domain.Aggregates;
using Otus.PaymentSystem.Identity.Host.Json;

namespace Otus.PaymentSystem.Identity.Host.Tests;

/// <summary>
/// Enum'ы в контракте — строками (#108): клиенту и спеке нечего делать с "0".
/// </summary>
public sealed class IdentityJsonOptionsTests
{
    [Fact]
    public void Apply_SerializesUserStatusAsString()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        IdentityJsonOptions.Apply(options);

        var profile = new GetProfileResult(
            Guid.NewGuid(),
            "user@example.com",
            UserStatus.Blocked,
            DateTimeOffset.UnixEpoch);

        var json = JsonSerializer.Serialize(profile, options);

        Assert.Contains("\"status\":\"Blocked\"", json);
        Assert.DoesNotContain("\"status\":1", json);
    }

    [Fact]
    public void Apply_DeserializesUserStatusFromString()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        IdentityJsonOptions.Apply(options);

        var profile = JsonSerializer.Deserialize<GetProfileResult>(
            """{"userId":"3f2504e0-4f89-11d3-9a0c-0305e82c3301","email":"user@example.com","status":"Active","createdAt":"2026-11-04T00:00:00+00:00"}""",
            options);

        Assert.NotNull(profile);
        Assert.Equal(UserStatus.Active, profile.Status);
    }
}
