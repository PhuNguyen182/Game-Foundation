using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Installer
{
    [AutoInstall(InstallerInstanceType = nameof(InstallerType.ScriptableObject))]
    [CreateAssetMenu(fileName = "MobileNotificationInstaller",menuName = "DracoRuan/MobileNotifications/MobileNotificationInstaller")]
    public class MobileNotificationInstaller : ScriptableObject, IInstaller
    {
        [SerializeField] private MobileNotificationConfig config;
        
        public void Install(IContainerBuilder builder)
        {
            builder.RegisterEntryPoint<MobileNotificationManager>(Lifetime.Scoped);
            builder.RegisterInstance(this.config);
        }
    }
}