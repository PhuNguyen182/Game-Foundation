namespace VContainerSupport.Models;

public record AutoInstallRegistrationModel(
    string InstallerKey,
    string FullInstallerName,
    string MinimalInstallerName,
    string InstallerInstanceType)
{
    public string InstallerKey { get; } = InstallerKey;
    public string FullInstallerName { get; } = FullInstallerName;
    public string MinimalInstallerName { get; } = MinimalInstallerName;
    public string InstallerInstanceType { get; } = InstallerInstanceType;
}