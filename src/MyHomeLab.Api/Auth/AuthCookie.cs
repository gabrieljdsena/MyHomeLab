namespace MyHomeLab.Api.Auth;

public static class AuthCookie
{
    public const string Name = "__Host-mhl.session";

    public static void Append(HttpResponse response, string token, DateTime expiresAtUtc) =>
        response.Cookies.Append(Name, token, Options(expiresAtUtc));

    public static void Delete(HttpResponse response) =>
        response.Cookies.Delete(Name, Options(null));

    private static CookieOptions Options(DateTime? expiresAtUtc) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        Path = "/",
        Expires = expiresAtUtc,
    };
}
