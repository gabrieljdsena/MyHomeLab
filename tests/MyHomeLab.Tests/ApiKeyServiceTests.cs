using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using Xunit;

namespace MyHomeLab.Tests;

public class ApiKeyServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    private sealed record Harness(
        ApiKeyService Service,
        FakeApiKeyRepository Repository,
        TestTimeProvider Clock);

    private static Harness CreateHarness()
    {
        var repository = new FakeApiKeyRepository();
        var clock = new TestTimeProvider(new DateTimeOffset(Now));
        return new Harness(new ApiKeyService(repository, clock), repository, clock);
    }

    [Fact]
    public async Task Create_Returns_Token_Once_And_Stores_Only_Its_Hash()
    {
        var harness = CreateHarness();

        var result = await harness.Service.CreateAsync("Vohithe AI assistant", false, default);

        Assert.StartsWith("mhl_", result.Token);
        Assert.Equal(68, result.Token.Length);
        Assert.Equal(result.Token[..12], result.ApiKey.Prefix);
        Assert.Equal("Vohithe AI assistant", result.ApiKey.Name);
        Assert.Null(result.ApiKey.LastUsedAtUtc);
        Assert.Null(result.ApiKey.RevokedAtUtc);

        var stored = Assert.Single(harness.Repository.Keys);
        Assert.Equal(ApiKeyService.HashToken(result.Token), stored.TokenHash);
        Assert.DoesNotContain(result.Token, stored.TokenHash);
        Assert.DoesNotContain("Vohithe AI assistant", stored.TokenHash);
    }

    [Fact]
    public async Task Validate_Accepts_A_Created_Key_And_Touches_Last_Used()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync("Vohithe", false, default);

        var key = await harness.Service.ValidateAsync(created.Token, default);

        Assert.NotNull(key);
        Assert.Equal(created.ApiKey.Id, key!.Id);
        Assert.Equal(1, harness.Repository.TouchCalls);

        harness.Clock.Now = harness.Clock.Now.AddMinutes(1);
        Assert.NotNull(await harness.Service.ValidateAsync(created.Token, default));
        Assert.Equal(1, harness.Repository.TouchCalls);

        harness.Clock.Now = harness.Clock.Now.AddMinutes(6);
        Assert.NotNull(await harness.Service.ValidateAsync(created.Token, default));
        Assert.Equal(2, harness.Repository.TouchCalls);
    }

    [Fact]
    public async Task Validate_Does_Not_Expire_Keys()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync("Vohithe", false, default);

        harness.Clock.Now = harness.Clock.Now.AddYears(10);

        Assert.NotNull(await harness.Service.ValidateAsync(created.Token, default));
    }

    [Fact]
    public async Task Validate_Rejects_Unknown_Garbage_And_Revoked_Keys()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync("Vohithe", false, default);

        Assert.Null(await harness.Service.ValidateAsync("", default));
        Assert.Null(await harness.Service.ValidateAsync("not-a-key", default));
        Assert.Null(await harness.Service.ValidateAsync("mhl_" + new string('0', 64), default));

        await harness.Service.RevokeAsync(created.ApiKey.Id, default);

        Assert.Null(await harness.Service.ValidateAsync(created.Token, default));
    }

    [Fact]
    public async Task Revoke_Is_Idempotent_And_Unknown_Ids_Throw()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync("Vohithe", false, default);

        var first = await harness.Service.RevokeAsync(created.ApiKey.Id, default);
        var second = await harness.Service.RevokeAsync(created.ApiKey.Id, default);

        Assert.NotNull(first.RevokedAtUtc);
        Assert.Equal(first.RevokedAtUtc, second.RevokedAtUtc);

        await Assert.ThrowsAsync<ApiKeyNotFoundException>(
            () => harness.Service.RevokeAsync(Guid.NewGuid(), default));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("x")]
    public async Task Create_Rejects_Invalid_Names(string name)
    {
        var harness = CreateHarness();

        if (name == "x")
        {
            var longName = new string('x', ApiKeyService.MaxNameLength + 1);
            await Assert.ThrowsAsync<ValidationException>(
                () => harness.Service.CreateAsync(longName, false, default));
            return;
        }

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.CreateAsync(name, false, default));
    }

    [Fact]
    public async Task Create_Tracks_CanWrite_Permission()
    {
        var harness = CreateHarness();

        var readOnly = await harness.Service.CreateAsync("reader", false, default);
        var actions = await harness.Service.CreateAsync("actor", true, default);

        Assert.False(readOnly.ApiKey.CanWrite);
        Assert.True(actions.ApiKey.CanWrite);
        Assert.False(Assert.Single(harness.Repository.Keys, key => key.Name == "reader").CanWrite);
        Assert.True(Assert.Single(harness.Repository.Keys, key => key.Name == "actor").CanWrite);
    }

    [Fact]
    public async Task SetCanWrite_Updates_Active_Keys()
    {
        var harness = CreateHarness();
        var created = await harness.Service.CreateAsync("Vohithe", false, default);

        var updated = await harness.Service.SetCanWriteAsync(created.ApiKey.Id, true, default);
        Assert.True(updated.CanWrite);
        Assert.True(harness.Repository.Keys.Single().CanWrite);

        var key = await harness.Service.ValidateAsync(created.Token, default);
        Assert.True(key!.CanWrite);

        await harness.Service.RevokeAsync(created.ApiKey.Id, default);
        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.SetCanWriteAsync(created.ApiKey.Id, false, default));
    }
}
