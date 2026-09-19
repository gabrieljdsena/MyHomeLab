namespace MyHomeLab.Application.Errors;

public class ValidationException : Exception
{
    public ValidationException(string message) : base(message)
    {
    }
}