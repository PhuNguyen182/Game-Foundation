namespace MessagePipeSupport.Models;

public record RegisterMessageFilterModel(string FullRequestHandlerName, bool IsAsync)
{
    public string FullRequestHandlerName { get; } = FullRequestHandlerName;
    public bool IsAsync { get; } = IsAsync;
}