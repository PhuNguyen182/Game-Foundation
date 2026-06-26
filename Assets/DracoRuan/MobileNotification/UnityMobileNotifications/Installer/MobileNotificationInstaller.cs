using DracoRuan.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.MobileNotification.UnityMobileNotifications.Data;
using UnityEngine;
using VContainer.Unity;
using VContainer;

namespace DracoRuan.MobileNotification.UnityMobileNotifications.Installer
{
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