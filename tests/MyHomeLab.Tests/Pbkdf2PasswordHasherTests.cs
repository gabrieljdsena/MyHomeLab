using MyHomeLab.Infrastructure.Auth;
using Xunit;

namespace MyHomeLab.Tests;

public class Pbkdf2PasswordHasherTests
{
    private readonly Pbkdf2PasswordHasher _hasher = new();

    [Fact]
    public void Hash_Then_Verify_Roundtrips()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.StartsWith("pbkdf2-sha256$600000$", hash);
        Assert.True(_hasher.Verify("correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_Wrong_Password_Returns_False()
    {
        var hash = _hasher.Hash("correct horse battery staple");

        Assert.False(_hasher.Verify("wrong horse battery staple", hash));
    }

    [Fact]
    public void Hash_Uses_Unique_Salt()
    {
        var first = _hasher.Hash("same password");
        var second = _hasher.Hash("same password");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("same password", first));
        Assert.True(_hasher.Verify("same password", second));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("pbkdf2-sha256$notanumber$c2FsdA==$a2V5")]
    [InlineData("pbkdf2-sha256$99999999$c2FsdA==$a2V5")]
    [InlineData("pbkdf2-sha256$600000$notbase64$a2V5")]
    [InlineData("md5$600000$c2FsdA==$a2V5")]
    public void Verify_Malformed_Hash_Returns_False(string hash)
    {
        Assert.False(_hasher.Verify("whatever", hash));
    }
}
