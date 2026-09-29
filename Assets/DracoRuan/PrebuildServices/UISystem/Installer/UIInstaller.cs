using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.PrebuildServices.UISystem.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.UISystem.Installer
{
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "UIInstaller", menuName = "DracoRuan/UISystem/UI Installer")]
    public sealed class UIInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private UIRootConfig rootConfig;
        [SerializeField] private UIViewCollection[] viewCollections = System.Array.Empty<UIViewCollection>();

        public void Install(IContainerBuilder builder) =>
            builder.AddUIService(this.rootConfig, this.viewCollections);
    }
}