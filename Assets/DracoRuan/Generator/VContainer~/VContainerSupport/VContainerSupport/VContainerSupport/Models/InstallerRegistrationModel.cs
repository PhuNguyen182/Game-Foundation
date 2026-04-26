namespace VContainerSupport.Models;

public record InstallerRegistrationModel(
    string InstallerKey,
    string InstallerDataType,
    string LifetimeScopeName,
    string InstallerInstanceType)
{
    public string InstallerKey { get; } = InstallerKey;
    public string LifetimeScopeName { get; } = LifetimeScopeName;
    public string InstallerDataType { get; } = InstallerDataType;
    public string InstallerInstanceType { get; } = InstallerInstanceType;
}