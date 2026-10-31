using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using MyHomeLab.Api.Auth;
using Xunit;

namespace MyHomeLab.Tests;

public class OriginPolicyTests
{
    private static IConfiguration Config(params string[] origins) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Cors:AllowedOrigins"] = string.Join(',', origins),
            })
            .Build();

    private static HttpContext Context(string scheme, string host, string? origin, string? referer = null)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = "POST";
        context.Request.Scheme = scheme;
        context.Request.Host = new HostString(host);
        if (origin is not null)
        {
            context.Request.Headers.Origin = origin;
        }

        if (referer is not null)
        {
            context.Request.Headers.Referer = referer;
        }

        return context;
    }

    [Fact]
    public void Same_Origin_Is_Allowed()
    {
        Assert.True(OriginPolicy.IsAllowed(
            Context("https", "hub.local", "https://hub.local"), Config()));
    }

    [Fact]
    public void Same_Host_Different_Scheme_Is_Rejected()
    {
        Assert.False(OriginPolicy.IsAllowed(
            Context("https", "hub.local", "http://hub.local"), Config()));
    }

    [Fact]
    public void Configured_Dev_Origin_Is_Allowed()
    {
        Assert.True(OriginPolicy.IsAllowed(
            Context("http", "localhost:8080", "http://localhost:5173"), Config("http://localhost:5173")));
    }

    [Fact]
    public void Unknown_Origin_Is_Rejected()
    {
        Assert.False(OriginPolicy.IsAllowed(
            Context("https", "hub.local", "https://evil.example"), Config()));
    }

    [Fact]
    public void Null_Origin_Is_Rejected()
    {
        Assert.False(OriginPolicy.IsAllowed(
            Context("https", "hub.local", "null"), Config()));
    }

    [Fact]
    public void Missing_Origin_And_Referer_Is_Allowed()
    {
        Assert.True(OriginPolicy.IsAllowed(
            Context("https", "hub.local", null), Config()));
    }

    [Fact]
    public void Cross_Site_Referer_Is_Rejected()
    {
        Assert.False(OriginPolicy.IsAllowed(
            Context("https", "hub.local", null, "https://evil.example/page"), Config()));
    }

    [Fact]
    public void Same_Origin_Referer_Is_Allowed()
    {
        Assert.True(OriginPolicy.IsAllowed(
            Context("https", "hub.local", null, "https://hub.local/dashboard"), Config()));
    }
}
