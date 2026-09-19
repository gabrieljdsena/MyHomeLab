namespace MyHomeLab.Application.Errors;

public class AppConflictException : Exception
{
    public AppConflictException(string message) : base(message)
    {
    }
}