using System;

namespace MyHomeLab.Domain;

public readonly record struct MachineId(Guid Value)
{
    public static MachineId New() => new(Guid.NewGuid());

    public static MachineId From(Guid value) => new(value);

    public override string ToString() => Value.ToString("D");
}
