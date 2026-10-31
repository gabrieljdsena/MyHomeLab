namespace MyHomeLab.Application.Errors;

public class ApiKeyNotFoundException : Exception
{
    public ApiKeyNotFoundException(string message) : base(message)
    {
    }
}
