namespace MyHomeLab.Application.Errors;

public sealed class SqlExecutionException : Exception
{
    public SqlExecutionException(string message) : base(message)
    {
    }
}
