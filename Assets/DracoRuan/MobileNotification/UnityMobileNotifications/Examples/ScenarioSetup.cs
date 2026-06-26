using Cysharp.Threading.Tasks;
using DracoRuan.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.MobileNotification.UnityMobileNotifications.Data;
using UnityEngine;

namespace MBDK.MobileNotifications.Examples
{
    /// <summary>
    /// Example về cách sử dụng Notification Scenarios
    /// </summary>
    /// <remarks>
    /// Script này minh họa cách setup và schedule notification scenarios
    /// - kịch bản notifications được định nghĩa từ trước.
    /// </remarks>
    public class ScenarioSetup : MonoBehaviour
    {
        [Header("Notification Manager")]
        [SerializeField]
        [Tooltip("Reference đến MobileNotificationManager trong scene")]
        private MobileNotificationManager notificationManager;

        [Header("Scenarios")]
        [SerializeField]
        [Tooltip("Engagement scenario - để engage players trở lại game")]
        private NotificationScenario engagementScenario;

        [SerializeField]
        [Tooltip("Daily reminder scenario - nhắc người chơi quay lại hàng ngày")]
        private NotificationScenario dailyReminderScenario;

        [SerializeField]
        [Tooltip("Event scenario - thông báo về events trong game")]
        private NotificationScenario eventScenario;

        /// <summary>
        /// Unity Start lifecycle
        /// </summary>
        private async void Start()
        {
            // Wait for manager initialization
            await UniTask.WaitUntil(() => this.notificationManager != null && this.notificationManager.IsInitialized);

            // Wait for permission
            await UniTask.WaitUntil(() => this.notificationManager.HasPermission);

            Debug.Log("📋 [ScenarioSetup] Notification manager ready!");
        }

        /// <summary>
        /// PUBLIC: Schedule engagement scenario
        /// Gọi khi người chơi thoát game hoặc không active
        /// </summary>
        public async void ScheduleEngagementScenario()
        {
            if (this.engagementScenario == null)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Engagement scenario is null!");
                return;
            }

            if (!this.notificationManager.HasPermission)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Không có permission!");
                return;
            }

            Debug.Log("📅 [ScenarioSetup] Scheduling engagement scenario...");

            var scheduledIds = await this.notificationManager.ScheduleScenarioAsync(this.engagementScenario);

            Debug.Log($"✅ [ScenarioSetup] Engagement scenario scheduled: {scheduledIds.Count} notifications");
        }

        /// <summary>
        /// PUBLIC: Schedule daily reminder scenario
        /// Gọi sau khi người chơi hoàn thành session
        /// </summary>
        public async void ScheduleDailyReminderScenario()
        {
            if (this.dailyReminderScenario == null)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Daily reminder scenario is null!");
                return;
            }

            if (!this.notificationManager.HasPermission)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Không có permission!");
                return;
            }

            Debug.Log("📅 [ScenarioSetup] Scheduling daily reminder scenario...");

            var scheduledIds = await this.notificationManager.ScheduleScenarioAsync(this.dailyReminderScenario);

            Debug.Log($"✅ [ScenarioSetup] Daily reminder scenario scheduled: {scheduledIds.Count} notifications");
        }

        /// <summary>
        /// PUBLIC: Schedule event scenario
        /// Gọi khi có event mới trong game
        /// </summary>
        public async void ScheduleEventScenario()
        {
            if (this.eventScenario == null)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Event scenario is null!");
                return;
            }

            if (!this.notificationManager.HasPermission)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Không có permission!");
                return;
            }

            Debug.Log("📅 [ScenarioSetup] Scheduling event scenario...");

            var scheduledIds = await this.notificationManager.ScheduleScenarioAsync(this.eventScenario);

            Debug.Log($"✅ [ScenarioSetup] Event scenario scheduled: {scheduledIds.Count} notifications");
        }

        /// <summary>
        /// PUBLIC: Create và schedule custom scenario runtime
        /// Example về cách tạo scenario dynamically
        /// </summary>
        public async void CreateCustomScenario()
        {
            if (!this.notificationManager.HasPermission)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Không có permission!");
                return;
            }

            Debug.Log("⚙️ [ScenarioSetup] Creating custom scenario...");

            // Tạo scenario mới
            var customScenario = ScriptableObject.CreateInstance<NotificationScenario>();
            customScenario.scenarioName = "Custom Promotion";
            customScenario.description = "Limited time promotion notifications";
            customScenario.cancelPreviousOnSchedule = true;
            customScenario.groupKey = "promotion";

            // Add notifications vào scenario
            
            // Notification 1: Immediate promotion announcement
            var promoStart = new NotificationData(
                title: "🎉 Special Promotion Started!",
                body: "Get 50% off all items for the next 24 hours!",
                fireTimeInSeconds: 5 // 5 giây
            );
            promoStart.groupKey = "promotion";
            customScenario.AddNotification(promoStart);

            // Notification 2: Mid-promotion reminder
            var promoMid = new NotificationData(
                title: "⏰ Promotion Ending Soon!",
                body: "Only 12 hours left for 50% off! Don't miss out!",
                fireTimeInSeconds: 60 * 60 * 12 // 12 giờ
            );
            promoMid.groupKey = "promotion";
            customScenario.AddNotification(promoMid);

            // Notification 3: Last chance
            var promoEnd = new NotificationData(
                title: "🔥 Last Chance!",
                body: "Promotion ends in 1 hour! Shop now!",
                fireTimeInSeconds: 60 * 60 * 23 // 23 giờ
            );
            promoEnd.groupKey = "promotion";
            customScenario.AddNotification(promoEnd);

            // Schedule scenario
            var scheduledIds = await this.notificationManager.ScheduleScenarioAsync(customScenario);

            Debug.Log($"✅ [ScenarioSetup] Custom scenario scheduled: {scheduledIds.Count} notifications");
        }

        /// <summary>
        /// PUBLIC: Cancel current scenarios
        /// </summary>
        public void CancelAllScenarios()
        {
            if (this.notificationManager != null)
            {
                this.notificationManager.CancelAllNotifications();
                Debug.Log("🗑️ [ScenarioSetup] All scenarios cancelled");
            }
        }

        /// <summary>
        /// PUBLIC: Log scheduled notifications
        /// Debug method để xem những notifications nào đang scheduled
        /// </summary>
        public void LogScheduledNotifications()
        {
            if (this.notificationManager == null)
            {
                Debug.LogWarning("⚠️ [ScenarioSetup] Notification manager is null!");
                return;
            }

            var scheduled = this.notificationManager.GetScheduledNotifications();

            Debug.Log($"📋 [ScenarioSetup] Currently scheduled: {scheduled.Count} notifications");

            for (int i = 0; i < scheduled.Count; i++)
            {
                var notification = scheduled[i];
                Debug.Log($"   #{notification.identifier}: {notification.title} (Fire in {notification.fireTimeInSeconds}s)");
            }
        }
    }
}

