using MyHomeLab.Application.Abstractions;

namespace MyHomeLab.Tests;

public sealed class FakePasswordHasher : IPasswordHasher
{
    public string Hash(string password) => $"hashed::{password}";

    public bool Verify(string password, string passwordHash) => passwordHash == Hash(password);
}
