namespace MessagePipeSupport.Models;

public record RequestAllHandlerModel(
    string FullRequestHandlerName,
    string RequestType,
    string ResponseType)
{
    public string FullRequestHandlerName { get; } = FullRequestHandlerName;
    public string RequestType { get; } = RequestType;
    public string ResponseType { get; } = ResponseType;
}