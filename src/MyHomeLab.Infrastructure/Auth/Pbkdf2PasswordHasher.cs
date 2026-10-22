using System.Security.Cryptography;
using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Infrastructure.Auth;

internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const string Scheme = "pbkdf2-sha256";
    private const int Iterations = 600_000;
    private const int SaltBytes = 16;
    private const int KeyBytes = 32;
    private const int MaxIterations = 2_000_000;

    public string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, KeyBytes);
        return $"{Scheme}${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }

    public bool Verify(string password, string passwordHash)
    {
        var parsed = Parse(passwordHash);
        if (parsed is null)
        {
            return false;
        }

        var (iterations, salt, expectedKey) = parsed.Value;
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedKey.Length);
        return CryptographicOperations.FixedTimeEquals(key, expectedKey);
    }

    private static (int Iterations, byte[] Salt, byte[] Key)? Parse(string passwordHash)
    {
        var parts = passwordHash.Split('$');
        if (parts.Length != 4 || parts[0] != Scheme)
        {
            return null;
        }

        if (!int.TryParse(parts[1], out var iterations) || iterations < 1 || iterations > MaxIterations)
        {
            return null;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var key = Convert.FromBase64String(parts[3]);
            return salt.Length < 8 || key.Length < 16 ? null : (iterations, salt, key);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
