using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileNotification.Installer
{
    /// <summary>
    /// Drops the notification service into a lifetime scope, carrying its config.
    /// </summary>
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "MobileNotificationInstaller", menuName = "DracoRuan/MobileNotifications/MobileNotificationInstaller")]
    public class MobileNotificationInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private MobileNotificationConfig config;

        public void Install(IContainerBuilder builder) => builder.AddMobileNotificationService(this.config);
    }
}
