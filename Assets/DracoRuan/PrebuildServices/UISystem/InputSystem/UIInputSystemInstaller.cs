using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.UISystem.Input.DracoRuan.PrebuildServices.UISystem.InputSystem
{
    /// <summary>Auto-installed ScriptableObject variant of AddUIInputSystem (no PlayerInput
    /// runtime asset; use the extension method directly for that).</summary>
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "UIInputSystemInstaller", menuName = "DracoRuan/UISystem/UI Input System Installer")]
    public sealed class UIInputSystemInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private UIInputActions inputActions;

        public void Install(IContainerBuilder builder) => builder.AddUIInputSystem(this.inputActions);
    }
}
