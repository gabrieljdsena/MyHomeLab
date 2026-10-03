namespace MyHomeLab.Application.Errors;

public class SharedFileConflictException : Exception
{
    public SharedFileConflictException(string message) : base(message)
    {
    }
}
