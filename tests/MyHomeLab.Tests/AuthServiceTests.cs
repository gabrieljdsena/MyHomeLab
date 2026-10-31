using MyHomeLab.Application.Abstractions;
using MyHomeLab.Application.Dtos;
using MyHomeLab.Application.Errors;
using MyHomeLab.Application.Services;
using MyHomeLab.Infrastructure.Auth;
using Xunit;

namespace MyHomeLab.Tests;

public class AuthServiceTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef";
    private const string Password = "correct horse battery staple";

    private static readonly DateTime Now = Truncate(DateTime.UtcNow);

    private static DateTime Truncate(DateTime value) =>
        new(value.Ticks - value.Ticks % TimeSpan.TicksPerSecond, DateTimeKind.Utc);

    private static readonly AuthPolicy Policy = new(TimeSpan.FromDays(30), TimeSpan.FromMinutes(5), TimeSpan.FromHours(24));
    private static readonly AuthClientInfo Client = new("test-agent", "127.0.0.1");

    private sealed record Harness(
        AuthService Service,
        FakeAuthRepository Repository,
        TestTimeProvider Clock,
        JwtTokenService Tokens);

    private static Harness CreateHarness()
    {
        var repository = new FakeAuthRepository();
        var clock = new TestTimeProvider(new DateTimeOffset(Now));
        var tokens = new JwtTokenService(new AuthOptions { JwtSecret = Secret, Issuer = "myhomelab", Audience = "myhomelab" });
        var service = new AuthService(repository, new FakePasswordHasher(), tokens, Policy, clock);
        return new Harness(service, repository, clock, tokens);
    }

    private static AuthUserRecord SeedUser(FakeAuthRepository repository, string username = "gabriel", string password = Password)
    {
        var user = new AuthUserRecord(Guid.NewGuid(), username, new FakePasswordHasher().Hash(password));
        repository.Users.Add(user);
        return user;
    }

    [Fact]
    public async Task GetStatus_Reports_NeedsSetup_Until_Account_Exists()
    {
        var harness = CreateHarness();

        var before = await harness.Service.GetStatusAsync(default);
        SeedUser(harness.Repository);
        var after = await harness.Service.GetStatusAsync(default);

        Assert.True(before.NeedsSetup);
        Assert.False(after.NeedsSetup);
    }

    [Fact]
    public async Task Setup_Creates_Account_And_Logs_In()
    {
        var harness = CreateHarness();

        var result = await harness.Service.SetupAsync(new SetupRequest("  Gabriel ", Password), Client, default);

        Assert.Equal("gabriel", result.User.Username);
        Assert.Equal(Now.AddDays(30), result.ExpiresAtUtc);

        var user = Assert.Single(harness.Repository.Users);
        Assert.Equal(new FakePasswordHasher().Hash(Password), user.PasswordHash);

        var session = Assert.Single(harness.Repository.Sessions);
        Assert.Equal(Now, session.CreatedAtUtc);
        Assert.Equal(Now.AddDays(30), session.ExpiresAtUtc);
        Assert.Equal("test-agent", session.UserAgent);
        Assert.Equal("127.0.0.1", session.CreatedIp);

        var claims = await harness.Tokens.ValidateAsync(result.Token);
        Assert.NotNull(claims);
        Assert.Equal(user.Id, claims.UserId);
        Assert.Equal(session.Id, claims.SessionId);
    }

    [Fact]
    public async Task Setup_When_Account_Exists_Throws_Conflict()
    {
        var harness = CreateHarness();
        SeedUser(harness.Repository);

        await Assert.ThrowsAsync<AuthConflictException>(
            () => harness.Service.SetupAsync(new SetupRequest("gabriel", Password), Client, default));
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("bad user")]
    [InlineData("bad@user")]
    public async Task Setup_Invalid_Username_Throws_Validation(string username)
    {
        var harness = CreateHarness();

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.SetupAsync(new SetupRequest(username, Password), Client, default));
    }

    [Theory]
    [InlineData("short")]
    [InlineData("gabriel")]
    public async Task Setup_Invalid_Password_Throws_Validation(string password)
    {
        var harness = CreateHarness();

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.SetupAsync(new SetupRequest("gabriel", password), Client, default));
    }

    [Fact]
    public async Task Setup_Overlong_Password_Throws_Validation()
    {
        var harness = CreateHarness();

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.SetupAsync(new SetupRequest("gabriel", new string('x', 300)), Client, default));
    }

    [Fact]
    public async Task Login_Unknown_User_Throws_Invalid_Credentials()
    {
        var harness = CreateHarness();

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => harness.Service.LoginAsync(new LoginRequest("nobody", Password), Client, default));
    }

    [Fact]
    public async Task Login_Wrong_Password_Throws_Invalid_Credentials()
    {
        var harness = CreateHarness();
        SeedUser(harness.Repository);

        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => harness.Service.LoginAsync(new LoginRequest("gabriel", "not the password"), Client, default));
    }

    [Fact]
    public async Task Login_Overlong_Password_Throws_Invalid_Credentials()
    {
        var harness = CreateHarness();
        SeedUser(harness.Repository);

        var oversized = new string('x', 10_000);
        await Assert.ThrowsAsync<InvalidCredentialsException>(
            () => harness.Service.LoginAsync(new LoginRequest("gabriel", oversized), Client, default));
    }

    [Fact]
    public async Task Login_Success_Creates_Session_And_Valid_Token()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);

        var result = await harness.Service.LoginAsync(new LoginRequest("  GABRIEL ", Password), Client, default);

        Assert.Equal(user.Id, result.User.Id);
        var session = Assert.Single(harness.Repository.Sessions);
        var claims = await harness.Tokens.ValidateAsync(result.Token);
        Assert.NotNull(claims);
        Assert.Equal(user.Id, claims.UserId);
        Assert.Equal(session.Id, claims.SessionId);
    }

    [Fact]
    public async Task Login_Truncates_Long_User_Agent()
    {
        var harness = CreateHarness();
        SeedUser(harness.Repository);

        await harness.Service.LoginAsync(
            new LoginRequest("gabriel", Password),
            new AuthClientInfo(new string('u', 600), new string('1', 80)),
            default);

        var session = Assert.Single(harness.Repository.Sessions);
        Assert.Equal(512, session.UserAgent!.Length);
        Assert.Equal(64, session.CreatedIp!.Length);
    }

    [Fact]
    public async Task Login_Cleans_Up_Expired_Sessions()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        harness.Repository.Sessions.Add(new AuthSessionRecord(
            Guid.NewGuid(), user.Id, Now.AddDays(-40), Now.AddDays(-40), Now.AddDays(-10), null, null, null));

        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);

        Assert.Equal(1, harness.Repository.DeleteExpiredCalls);
        Assert.Single(harness.Repository.Sessions);
    }

    [Fact]
    public async Task ValidateSession_Valid_Without_Touching_Before_Interval()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        var login = await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);

        var check = await harness.Service.ValidateSessionAsync(login.User.Id == user.Id ? harness.Repository.Sessions[0].Id : Guid.Empty, user.Id, Now, default);

        Assert.True(check.IsValid);
        Assert.Null(check.RenewedToken);
        Assert.Equal(0, harness.Repository.TouchCalls);
    }

    [Fact]
    public async Task ValidateSession_Touches_And_Extends_After_Interval()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        harness.Clock.Now = new DateTimeOffset(Now.AddMinutes(10));
        var check = await harness.Service.ValidateSessionAsync(sessionId, user.Id, Now, default);

        Assert.True(check.IsValid);
        Assert.Null(check.RenewedToken);
        Assert.Equal(1, harness.Repository.TouchCalls);
        Assert.Equal(Now.AddMinutes(10).AddDays(30), harness.Repository.Sessions[0].ExpiresAtUtc);
    }

    [Fact]
    public async Task ValidateSession_Renews_Token_After_Reissue_Interval()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        harness.Clock.Now = new DateTimeOffset(Now.AddHours(25));
        var check = await harness.Service.ValidateSessionAsync(sessionId, user.Id, Now, default);

        Assert.True(check.IsValid);
        Assert.NotNull(check.RenewedToken);
        Assert.Equal(Now.AddHours(25).AddDays(30), check.RenewedTokenExpiresAtUtc);
        Assert.Equal(3, check.RenewedToken!.Split('.').Length);
    }

    [Fact]
    public async Task ValidateSession_Revoked_Session_Is_Invalid()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;
        await harness.Service.LogoutAsync(sessionId, default);

        var check = await harness.Service.ValidateSessionAsync(sessionId, user.Id, Now, default);

        Assert.False(check.IsValid);
    }

    [Fact]
    public async Task ValidateSession_Expired_Session_Is_Invalid()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        harness.Clock.Now = new DateTimeOffset(Now.AddDays(31));
        var check = await harness.Service.ValidateSessionAsync(sessionId, user.Id, Now, default);

        Assert.False(check.IsValid);
    }

    [Fact]
    public async Task ValidateSession_User_Mismatch_Is_Invalid()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        var check = await harness.Service.ValidateSessionAsync(sessionId, Guid.NewGuid(), Now, default);

        Assert.False(check.IsValid);
        Assert.Equal(user.Id, harness.Repository.Sessions[0].UserId);
    }

    [Fact]
    public async Task LogoutAll_Revokes_Every_Session()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);

        var revoked = await harness.Service.LogoutAllAsync(user.Id, default);

        Assert.Equal(2, revoked);
        Assert.All(harness.Repository.Sessions, s => Assert.NotNull(s.RevokedAtUtc));
    }

    [Fact]
    public async Task ChangePassword_Wrong_Current_Throws_Validation()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.ChangePasswordAsync(
                user.Id,
                sessionId,
                new ChangePasswordRequest("not the current one", "a brand new password 123"),
                default));
    }

    [Fact]
    public async Task ChangePassword_Overlong_Current_Throws_Validation()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.ChangePasswordAsync(
                user.Id,
                sessionId,
                new ChangePasswordRequest(new string('x', 10_000), "a brand new password 123"),
                default));
    }

    [Fact]
    public async Task ChangePassword_Updates_Hash_And_Revokes_Other_Sessions()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var currentSessionId = harness.Repository.Sessions[0].Id;
        var otherSessionId = harness.Repository.Sessions[1].Id;

        await harness.Service.ChangePasswordAsync(
            user.Id,
            currentSessionId,
            new ChangePasswordRequest(Password, "a brand new password 123"),
            default);

        Assert.Equal(new FakePasswordHasher().Hash("a brand new password 123"), harness.Repository.Users[0].PasswordHash);
        Assert.Null(harness.Repository.Sessions.First(s => s.Id == currentSessionId).RevokedAtUtc);
        Assert.NotNull(harness.Repository.Sessions.First(s => s.Id == otherSessionId).RevokedAtUtc);
    }

    [Fact]
    public async Task ChangePassword_Weak_New_Password_Throws_Validation()
    {
        var harness = CreateHarness();
        var user = SeedUser(harness.Repository);
        await harness.Service.LoginAsync(new LoginRequest("gabriel", Password), Client, default);
        var sessionId = harness.Repository.Sessions[0].Id;

        await Assert.ThrowsAsync<ValidationException>(
            () => harness.Service.ChangePasswordAsync(
                user.Id,
                sessionId,
                new ChangePasswordRequest(Password, "short"),
                default));
    }
}
