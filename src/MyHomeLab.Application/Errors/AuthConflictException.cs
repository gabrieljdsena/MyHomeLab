namespace MyHomeLab.Application.Errors;

public class AuthConflictException : Exception
{
    public AuthConflictException(string message) : base(message)
    {
    }
}
