namespace MessagePipeSupport.Models;

public record RequestHandlerModel(
    string FullRequestHandlerName,
    string MinimalRequestHandlerName,
    string RequestType,
    string ResponseType)
{
    public string FullRequestHandlerName { get; } = FullRequestHandlerName;
    public string MinimalRequestHandlerName { get; } = MinimalRequestHandlerName;
    public string RequestType { get; } = RequestType;
    public string ResponseType { get; } = ResponseType;
}