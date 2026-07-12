using Cysharp.Threading.Tasks;
using DracoRuan.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Data;
using UnityEngine;

namespace MBDK.MobileNotifications.Examples
{
    /// <summary>
    /// Example đơn giản về cách sử dụng Mobile Notification System
    /// </summary>
    /// <remarks>
    /// Script này minh họa các use cases cơ bản: request permission,
    /// schedule single notification, và handle notification events.
    /// </remarks>
    public class BasicUsage : MonoBehaviour
    {
        [Header("Notification Manager")]
        [SerializeField]
        [Tooltip("Reference đến MobileNotificationManager trong scene")]
        private MobileNotificationManager notificationManager;

        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Config cho notification system")]
        private MobileNotificationConfig config;

        /// <summary>
        /// Unity Start lifecycle
        /// </summary>
        private async UniTaskVoid Start()
        {
            // Initialize notification system
            await this.InitializeNotificationSystemAsync();

            // Request permission
            await this.RequestNotificationPermissionAsync();

            // Schedule example notifications
            await this.ScheduleExampleNotificationsAsync();
        }

        /// <summary>
        /// Unity OnEnable lifecycle
        /// </summary>
        private void OnEnable()
        {
            // Subscribe to notification events
            if (this.notificationManager != null)
            {
                this.notificationManager.OnPermissionChanged += this.HandlePermissionChanged;
                this.notificationManager.OnNotificationReceived += this.HandleNotificationReceived;
                this.notificationManager.OnNotificationError += this.HandleNotificationError;
            }
        }

        /// <summary>
        /// Unity OnDisable lifecycle
        /// </summary>
        private void OnDisable()
        {
            // Unsubscribe from notification events
            if (this.notificationManager != null)
            {
                this.notificationManager.OnPermissionChanged -= this.HandlePermissionChanged;
                this.notificationManager.OnNotificationReceived -= this.HandleNotificationReceived;
                this.notificationManager.OnNotificationError -= this.HandleNotificationError;
            }
        }

        /// <summary>
        /// Khởi tạo notification system
        /// </summary>
        private async UniTask InitializeNotificationSystemAsync()
        {
            if (this.notificationManager == null)
            {
                Debug.LogError("❌ [BasicUsage] NotificationManager is null!");
                return;
            }

            if (this.config == null)
            {
                Debug.LogError("❌ [BasicUsage] NotificationConfig is null!");
                return;
            }

            Debug.Log("🔔 [BasicUsage] Initializing notification system...");

            // Initialize manager
            await this.notificationManager.InitializeAsync(this.config);

            Debug.Log("✅ [BasicUsage] Notification system initialized!");
        }

        /// <summary>
        /// Request permission để hiển thị notifications
        /// </summary>
        private async UniTask RequestNotificationPermissionAsync()
        {
            Debug.Log("🔐 [BasicUsage] Requesting notification permission...");

            var granted = await this.notificationManager.RequestPermissionAsync();

            if (granted)
            {
                Debug.Log("✅ [BasicUsage] Notification permission granted!");
            }
            else
            {
                Debug.LogWarning("⚠️ [BasicUsage] Notification permission denied!");
            }
        }

        /// <summary>
        /// Schedule example notifications
        /// </summary>
        private async UniTask ScheduleExampleNotificationsAsync()
        {
            if (!this.notificationManager.HasPermission)
            {
                Debug.LogWarning("⚠️ [BasicUsage] Không có permission để schedule notifications");
                return;
            }

            Debug.Log("📅 [BasicUsage] Scheduling example notifications...");

            // Example 1: Welcome notification sau 10 giây
            var welcomeNotification = new NotificationData(
                title: "Welcome back! 🎮",
                body: "Your game misses you! Come back and continue your adventure.",
                fireTimeInSeconds: 10
            );

            var welcomeId = await this.notificationManager.ScheduleNotificationAsync(welcomeNotification);
            Debug.Log($"✅ [BasicUsage] Welcome notification scheduled with ID: {welcomeId}");

            // Example 2: Daily reward notification sau 60 giây
            var dailyRewardNotification = new NotificationData(
                title: "Daily Reward Available! 🎁",
                body: "Claim your daily reward now!",
                fireTimeInSeconds: 60
            );
            dailyRewardNotification.badge = 1;
            dailyRewardNotification.customData = "daily_reward";

            var rewardId = await this.notificationManager.ScheduleNotificationAsync(dailyRewardNotification);
            Debug.Log($"✅ [BasicUsage] Daily reward notification scheduled with ID: {rewardId}");

            // Example 3: Energy refill notification sau 120 giây (repeating)
            var energyNotification = new NotificationData(
                title: "Energy Refilled! ⚡",
                body: "Your energy is full! Time to play!",
                fireTimeInSeconds: 120
            );
            energyNotification.repeats = true;
            energyNotification.repeatInterval = 300; // Repeat mỗi 5 phút

            var energyId = await this.notificationManager.ScheduleNotificationAsync(energyNotification);
            Debug.Log($"✅ [BasicUsage] Energy notification scheduled with ID: {energyId}");
        }

        /// <summary>
        /// Handle permission changed event
        /// </summary>
        /// <param name="granted">Permission status</param>
        private void HandlePermissionChanged(bool granted)
        {
            Debug.Log($"🔐 [BasicUsage] Permission changed: {(granted ? "Granted" : "Denied")}");

            if (granted)
            {
                // Permission granted - có thể schedule notifications
                Debug.Log("✅ [BasicUsage] Can now schedule notifications!");
            }
            else
            {
                // Permission denied - không thể schedule
                Debug.LogWarning("⚠️ [BasicUsage] Cannot schedule notifications without permission");
            }
        }

        /// <summary>
        /// Handle notification received event
        /// </summary>
        /// <param name="notification">Notification data received</param>
        private void HandleNotificationReceived(NotificationData notification)
        {
            Debug.Log($"📬 [BasicUsage] Notification received: {notification.title}");
            Debug.Log($"   Body: {notification.body}");
            Debug.Log($"   Custom Data: {notification.customData}");

            // Xử lý dựa vào custom data
            if (!string.IsNullOrWhiteSpace(notification.customData))
            {
                switch (notification.customData)
                {
                    case "daily_reward":
                        this.HandleDailyRewardNotification();
                        break;

                    default:
                        Debug.Log($"   Unknown notification type: {notification.customData}");
                        break;
                }
            }
        }

        /// <summary>
        /// Handle notification error event
        /// </summary>
        /// <param name="errorMessage">Error message</param>
        private void HandleNotificationError(string errorMessage)
        {
            Debug.LogError($"❌ [BasicUsage] Notification error: {errorMessage}");
        }

        /// <summary>
        /// Handle daily reward notification
        /// </summary>
        private void HandleDailyRewardNotification()
        {
            Debug.Log("🎁 [BasicUsage] User tapped daily reward notification!");
            
            // Xử lý logic: show daily reward screen, grant reward, etc.
            // Example: Load daily reward scene
            // UnityEngine.SceneManagement.SceneManager.LoadScene("DailyRewardScene");
        }

        /// <summary>
        /// PUBLIC: Cancel tất cả notifications
        /// Gọi method này từ UI button hoặc khi cần
        /// </summary>
        public void CancelAllNotifications()
        {
            if (this.notificationManager != null)
            {
                this.notificationManager.CancelAllNotifications();
                Debug.Log("🗑️ [BasicUsage] All notifications cancelled");
            }
        }

        /// <summary>
        /// PUBLIC: Clear delivered notifications
        /// Gọi method này từ UI button
        /// </summary>
        public void ClearDeliveredNotifications()
        {
            if (this.notificationManager != null)
            {
                this.notificationManager.ClearDeliveredNotifications();
                Debug.Log("🧹 [BasicUsage] Delivered notifications cleared");
            }
        }
    }
}

