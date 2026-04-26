namespace VContainerSupport.Models;

public record ServiceRegistrationModel(
    string ServiceName,
    string LifetimeScope,
    string LifetimeScopeName,
    string InstallerName,
    bool AsImplementInterfaces,
    bool IsEntryPoint,
    bool AsSelf,
    string WithKey)
{
    public string ServiceName { get; } = ServiceName;
    public string LifetimeScope { get; } = LifetimeScope;
    public string LifetimeScopeName { get; } = LifetimeScopeName;
    public string InstallerName { get; } = InstallerName;
    public bool AsImplementInterfaces { get; } = AsImplementInterfaces;
    public bool IsEntryPoint { get; } = IsEntryPoint;
    public bool AsSelf { get; } = AsSelf;
    public string WithKey { get; } = WithKey;
}