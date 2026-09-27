namespace MyHomeLab.Application.Errors;

public class MachineNotFoundException : Exception
{
    public MachineNotFoundException(string message) : base(message)
    {
    }
}
