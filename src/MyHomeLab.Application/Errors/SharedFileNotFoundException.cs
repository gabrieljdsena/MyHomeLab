namespace MyHomeLab.Application.Errors;

public class SharedFileNotFoundException : Exception
{
    public SharedFileNotFoundException(string message) : base(message)
    {
    }
}
