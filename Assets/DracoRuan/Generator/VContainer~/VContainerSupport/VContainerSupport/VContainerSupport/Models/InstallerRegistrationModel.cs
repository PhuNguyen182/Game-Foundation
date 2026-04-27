namespace VContainerSupport.Models;

public record InstallerRegistrationModel(
    string InstallerKey,
    string FullInstallerName,
    string LifetimeScopeName,
    string InstallerInstanceType)
{
    public string InstallerKey { get; } = InstallerKey;
    public string FullInstallerName { get; } = FullInstallerName;
    public string LifetimeScopeName { get; } = LifetimeScopeName;
    public string InstallerInstanceType { get; } = InstallerInstanceType;
}