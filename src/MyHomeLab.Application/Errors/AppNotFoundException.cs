namespace MyHomeLab.Application.Errors;

public class AppNotFoundException : Exception
{
    public AppNotFoundException(string message) : base(message)
    {
    }
}