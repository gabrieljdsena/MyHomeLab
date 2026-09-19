using System;

namespace MyHomeLab.Domain;

public readonly record struct AppId(Guid Value)
{
    public static AppId New() => new(Guid.NewGuid());

    public static AppId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}