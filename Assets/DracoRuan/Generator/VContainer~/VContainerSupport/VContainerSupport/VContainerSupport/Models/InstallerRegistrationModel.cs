namespace VContainerSupport.Models;

public record InstallerRegistrationModel(
    string InstallerKey,
    string FullInstallerName,
    string MinimalInstallerName,
    string LifetimeScopeName,
    string InstallerInstanceType)
{
    public string InstallerKey { get; } = InstallerKey;
    public string FullInstallerName { get; } = FullInstallerName;
    public string MinimalInstallerName { get; } = MinimalInstallerName;
    public string LifetimeScopeName { get; } = LifetimeScopeName;
    public string InstallerInstanceType { get; } = InstallerInstanceType;
}