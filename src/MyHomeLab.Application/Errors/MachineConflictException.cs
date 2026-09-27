namespace MyHomeLab.Application.Errors;

public class MachineConflictException : Exception
{
    public MachineConflictException(string message) : base(message)
    {
    }
}
