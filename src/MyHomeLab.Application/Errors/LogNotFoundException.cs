namespace MyHomeLab.Application.Errors;

public class LogNotFoundException : Exception
{
    public LogNotFoundException(string message) : base(message)
    {
    }
}
