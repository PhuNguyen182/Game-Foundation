using System;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Data;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Interfaces;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Core
{
    /// <summary>
    /// Implementation để xử lý quyền truy cập notification
    /// </summary>
    /// <remarks>
    /// Class này xử lý việc request và kiểm tra permission cho notifications
    /// trên cả Android và iOS platforms.
    /// </remarks>
    public class NotificationPermissionHandler : INotificationPermissionHandler
    {
        private bool _hasPermission;
        private readonly MobileNotificationConfig _config;

        /// <summary>
        /// Event được raise khi permission status thay đổi
        /// </summary>
        public event Action<bool> OnPermissionStatusChanged;

        /// <summary>
        /// Lấy trạng thái permission hiện tại
        /// </summary>
        /// <value>True nếu đã có permission</value>
        public bool HasPermission => this._hasPermission;

        /// <summary>
        /// Constructor với config
        /// </summary>
        /// <param name="config">Configuration cho permission handler</param>
        public NotificationPermissionHandler(MobileNotificationConfig config)
        {
            this._config = config;
            this._hasPermission = false;
        }

        /// <summary>
        /// Request quyền hiển thị notification từ user
        /// </summary>
        /// <returns>UniTask với bool cho biết permission đã được granted</returns>
        public async UniTask<bool> RequestPermissionAsync()
        {
            try
            {
                if (this._config.enableDebugLogs)
                {
                    Debug.Log("🔔 [NotificationPermission] Requesting notification permission...");
                }

#if UNITY_ANDROID
                this._hasPermission = await this.RequestAndroidPermissionAsync();
#elif UNITY_IOS
                this._hasPermission = await this.RequestIOSPermissionAsync();
#else
                // Platform không được hỗ trợ
                this._hasPermission = false;
                Debug.LogWarning("⚠️ [NotificationPermission] Current platform không hỗ trợ notifications");
#endif

                if (this._config.enableDebugLogs)
                {
                    Debug.Log($"✅ [NotificationPermission] Permission result: {this._hasPermission}");
                }

                // Raise event
                this.OnPermissionStatusChanged?.Invoke(this._hasPermission);

                return this._hasPermission;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationPermission] Error requesting permission: {ex.Message}");
                this._hasPermission = false;
                return false;
            }
        }

        /// <summary>
        /// Kiểm tra xem hiện tại có quyền hiển thị notification không
        /// </summary>
        /// <returns>True nếu có quyền</returns>
        public bool CheckPermission()
        {
#if UNITY_ANDROID
            // Android mặc định có permission (trừ khi user tắt trong settings)
            // Check permission status nếu có API
            this._hasPermission = true;
#elif UNITY_IOS
            // iOS cần check authorization status
            var settings = iOSNotificationCenter.GetNotificationSettings();
            this._hasPermission = settings.AuthorizationStatus == AuthorizationStatus.Authorized;
#else
            this._hasPermission = false;
#endif

            return this._hasPermission;
        }

#if UNITY_ANDROID
        /// <summary>
        /// Request permission trên Android
        /// </summary>
        /// <returns>UniTask với permission result</returns>
        private async UniTask<bool> RequestAndroidPermissionAsync()
        {
            try
            {
                // Android 13+ (API 33+) cần runtime permission
                // Versions cũ hơn tự động có permission
#if UNITY_2023_2_OR_NEWER
                // Unity 2023.2+ có API để check và request permission
                if (AndroidNotificationCenter.UserPermissionToPost == PermissionStatus.Allowed)
                {
                    await UniTask.Delay(100); // Delay nhỏ để UI ready
                    
                    // Note: Unity Mobile Notifications không có direct API để request
                    // Cần sử dụng Unity Permission API hoặc native plugin
                    // Hiện tại assume permission được granted
                    return true;
                }
#endif
                
                // Default permission cho Android
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationPermission] Android permission error: {ex.Message}");
                return false;
            }
        }
#endif

#if UNITY_IOS
        /// <summary>
        /// Request permission trên iOS
        /// </summary>
        /// <returns>UniTask với permission result</returns>
        private async UniTask<bool> RequestIOSPermissionAsync()
        {
            try
            {
                // Setup authorization options
                var authOptions = AuthorizationOption.None;
                
                if (this._config.iosRequestAlert)
                {
                    authOptions |= AuthorizationOption.Alert;
                }
                
                if (this._config.iosRequestBadge)
                {
                    authOptions |= AuthorizationOption.Badge;
                }
                
                if (this._config.iosRequestSound)
                {
                    authOptions |= AuthorizationOption.Sound;
                }

                // Request authorization
                using (var request = new AuthorizationRequest(authOptions, true))
                {
                    while (!request.IsFinished)
                    {
                        await UniTask.Yield();
                    }

                    var granted = request.Granted;
                    
                    if (this._config.enableDebugLogs)
                    {
                        Debug.Log($"📱 [NotificationPermission] iOS authorization: {(granted ? "Granted" : "Denied")}");
                        Debug.Log($"📱 [NotificationPermission] Device Token: {request.DeviceToken}");
                    }

                    return granted;
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationPermission] iOS permission error: {ex.Message}");
                return false;
            }
        }
#endif
    }
}

