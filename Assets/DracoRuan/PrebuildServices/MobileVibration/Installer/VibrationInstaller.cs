using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.PrebuildServices.MobileVibration.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileVibration.Installer
{
    /// <summary>
    /// Drops the vibration service into a lifetime scope, carrying its collection.
    /// </summary>
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "VibrationInstaller", menuName = "DracoRuan/MobileVibration/VibrationInstaller")]
    public class VibrationInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private VibrationCollection collection;

        public void Install(IContainerBuilder builder) => builder.AddVibrationService(this.collection);
    }
}
