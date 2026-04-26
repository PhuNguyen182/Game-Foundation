namespace VContainerSupport.Models;

public record InstallerRegistrationModel(string InstallerName, string LifetimeScopeName)
{
    public string InstallerName { get; } = InstallerName;
    public string LifetimeScopeName { get; } = LifetimeScopeName;
}