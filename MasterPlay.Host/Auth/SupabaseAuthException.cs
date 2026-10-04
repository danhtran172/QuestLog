namespace MasterPlay.Host.Auth;

public sealed class SupabaseAuthException : Exception
{
    public int StatusCode { get; }
    public string Code { get; }

    public SupabaseAuthException(int statusCode, string code, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
    }
}

