namespace MessagePipeSupport.Models;

public record RegisterRequestHandlerFilterModel(string FullRequestHandlerName, bool IsAsync)
{
    public string FullRequestHandlerName { get; } = FullRequestHandlerName;
    public bool IsAsync { get; } = IsAsync;
}