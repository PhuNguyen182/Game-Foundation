using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.MobileNotification.UnityMobileNotifications.Core;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Data;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Interfaces;
using VContainer.Unity;

#if UNITY_ANDROID
using Unity.Notifications.Android;
#elif UNITY_IOS
using Unity.Notifications.iOS;
#endif

namespace DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Core
{
    /// <summary>
    /// Manager chính để quản lý toàn bộ mobile notification system
    /// </summary>
    /// <remarks>
    /// Class này là entry point chính để sử dụng notification system.
    /// Nó orchestrate tất cả các components: permission, scheduler, và service.
    /// </remarks>
    public class MobileNotificationManager : IMobileNotificationManager, IDisposable, ITickable
    {
        /// <summary>
        /// Configuration cho notification system
        /// </summary>
        private MobileNotificationConfig _mobileNotificationConfig;

        // Dependencies
        private INotificationPermissionHandler _permissionHandler;
        private INotificationScheduler _scheduler;
        private IMobileNotificationService _service;

        // State
        private bool _isInitialized;
        private bool _isCheckingNotifications;

        /// <summary>
        /// Kiểm tra xem manager đã được khởi tạo chưa
        /// </summary>
        /// <value>True nếu đã khởi tạo</value>
        public bool IsInitialized => this._isInitialized;

        /// <summary>
        /// Kiểm tra xem đã có quyền hiển thị notification hay chưa
        /// </summary>
        /// <value>True nếu có quyền</value>
        public bool HasPermission => this._permissionHandler?.HasPermission ?? false;

        /// <summary>
        /// Event được raise khi notification permission thay đổi
        /// </summary>
        public event Action<bool> OnPermissionChanged;

        /// <summary>
        /// Event được raise khi có notification được tap bởi user
        /// </summary>
        public event Action<NotificationData> OnNotificationReceived;

        /// <summary>
        /// Event được raise khi có lỗi xảy ra trong notification system
        /// </summary>
        public event Action<string> OnNotificationError;

        public MobileNotificationManager(MobileNotificationConfig config)
        {
            this._mobileNotificationConfig = config;
            this.InitializeDependencies();
            this.TryInitializeAsync().Forget();
        }

        private async UniTask TryInitializeAsync()
        {
            // Auto initialize nếu có config
            if (this._mobileNotificationConfig)
                await this.InitializeAsync(this._mobileNotificationConfig);
        }

        /// <summary>
        /// Unity Update lifecycle
        /// </summary>
        public void Tick()
        {
            if (this._isInitialized && !this._isCheckingNotifications)
            {
                this.CheckReceivedNotifications();
            }
        }
        
        public void Dispose()
        {
            if (this._permissionHandler != null)
            {
                this._permissionHandler.OnPermissionStatusChanged -= this.HandlePermissionChanged;
            }
        }

        /// <summary>
        /// Khởi tạo notification manager với configuration
        /// </summary>
        /// <param name="notificationConfig">Configuration cho notification system</param>
        public async UniTask InitializeAsync(MobileNotificationConfig notificationConfig)
        {
            if (this._isInitialized)
            {
                Debug.LogWarning("⚠️ [NotificationManager] Manager đã được initialized!");
                return;
            }

            try
            {
                this._mobileNotificationConfig = notificationConfig;

                if (!this._mobileNotificationConfig || !this._mobileNotificationConfig.IsValid())
                {
                    Debug.LogError("❌ [NotificationManager] Invalid configuration!");
                    return;
                }

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log("🔔 [NotificationManager] Initializing notification system...");
                }

                // Initialize service
                await this._service.InitializeAsync(this._mobileNotificationConfig);

                // Initialize scheduler
                await this._scheduler.InitializeAsync(this._mobileNotificationConfig);

                // Subscribe to permission events
                this._permissionHandler.OnPermissionStatusChanged += this.HandlePermissionChanged;

                // Auto request permission nếu config yêu cầu
                if (this._mobileNotificationConfig.autoRequestPermission)
                {
                    await this.RequestPermissionAsync();
                }

                this._isInitialized = true;

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log("✅ [NotificationManager] Notification system initialized successfully!");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Initialization failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Initialization failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Request quyền hiển thị notification từ người dùng
        /// </summary>
        /// <returns>UniTask với bool cho biết quyền đã được cấp hay chưa</returns>
        public async UniTask<bool> RequestPermissionAsync()
        {
            try
            {
                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log("🔐 [NotificationManager] Requesting notification permission...");
                }

                var granted = await this._permissionHandler.RequestPermissionAsync();

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"✅ [NotificationManager] Permission {(granted ? "granted" : "denied")}");
                }

                return granted;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Permission request failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Permission request failed: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Schedule một notification với data được cung cấp
        /// </summary>
        /// <param name="notificationData">Data của notification cần schedule</param>
        /// <returns>ID của notification đã được schedule</returns>
        public async UniTask<int> ScheduleNotificationAsync(NotificationData notificationData)
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return -1;
            }

            if (!this.HasPermission)
            {
                Debug.LogWarning("⚠️ [NotificationManager] Không có permission để hiển thị notifications!");
                return -1;
            }

            try
            {
                // Validate data
                if (!this._service.ValidateNotificationData(notificationData))
                {
                    Debug.LogError("❌ [NotificationManager] Invalid notification data!");
                    return -1;
                }

                // Schedule notification
                var notificationId = await this._scheduler.ScheduleAsync(notificationData);

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"✅ [NotificationManager] Notification #{notificationId} scheduled: {notificationData.title}");
                }

                return notificationId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Schedule failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Schedule failed: {ex.Message}");
                return -1;
            }
        }

        /// <summary>
        /// Schedule nhiều notifications cùng lúc
        /// </summary>
        /// <param name="notifications">Danh sách notifications cần schedule</param>
        /// <returns>Danh sách IDs của các notifications đã được schedule</returns>
        public async UniTask<List<int>> ScheduleMultipleNotificationsAsync(List<NotificationData> notifications)
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return new List<int>();
            }

            if (!this.HasPermission)
            {
                Debug.LogWarning("⚠️ [NotificationManager] Không có permission để hiển thị notifications!");
                return new List<int>();
            }

            try
            {
                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"📅 [NotificationManager] Scheduling {notifications.Count} notifications...");
                }

                var scheduledIds = await this._scheduler.ScheduleMultipleAsync(notifications);

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"✅ [NotificationManager] {scheduledIds.Count} notifications scheduled");
                }

                return scheduledIds;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Multiple schedule failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Multiple schedule failed: {ex.Message}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Schedule notification scenario đã được setup từ trước
        /// </summary>
        /// <param name="scenario">Scenario cần schedule</param>
        /// <returns>Danh sách IDs của các notifications trong scenario</returns>
        public async UniTask<List<int>> ScheduleScenarioAsync(NotificationScenario scenario)
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return new List<int>();
            }

            if (!this.HasPermission)
            {
                Debug.LogWarning("⚠️ [NotificationManager] Không có permission để hiển thị notifications!");
                return new List<int>();
            }

            try
            {
                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"📋 [NotificationManager] Scheduling scenario: {scenario.scenarioName}");
                }

                // Cancel previous scenario nếu config yêu cầu
                if (scenario.cancelPreviousOnSchedule)
                {
                    this.CancelAllNotifications();
                }

                // Process scenario
                var notifications = this._service.ProcessScenario(scenario);

                if (notifications.Count == 0)
                {
                    Debug.LogWarning($"⚠️ [NotificationManager] No valid notifications in scenario: {scenario.scenarioName}");
                    return new List<int>();
                }

                // Schedule all notifications
                var scheduledIds = await this._scheduler.ScheduleMultipleAsync(notifications);

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"✅ [NotificationManager] Scenario scheduled: {scheduledIds.Count} notifications");
                }

                return scheduledIds;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Scenario schedule failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Scenario schedule failed: {ex.Message}");
                return new List<int>();
            }
        }

        /// <summary>
        /// Hủy một notification đã được schedule
        /// </summary>
        /// <param name="notificationId">ID của notification cần hủy</param>
        public void CancelNotification(int notificationId)
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return;
            }

            try
            {
                this._scheduler.Cancel(notificationId);

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"🗑️ [NotificationManager] Notification #{notificationId} cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Cancel failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Cancel failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hủy tất cả notifications đã được schedule
        /// </summary>
        public void CancelAllNotifications()
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return;
            }

            try
            {
                this._scheduler.CancelAll();

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log("🗑️ [NotificationManager] All notifications cancelled");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Cancel all failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Cancel all failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Hủy tất cả notifications đã được delivered
        /// </summary>
        public void ClearDeliveredNotifications()
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return;
            }

            try
            {
                this._scheduler.ClearDelivered();

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log("🧹 [NotificationManager] Delivered notifications cleared");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Clear delivered failed: {ex.Message}");
                this.OnNotificationError?.Invoke($"Clear delivered failed: {ex.Message}");
            }
        }

        /// <summary>
        /// Lấy danh sách tất cả notifications đang được schedule
        /// </summary>
        /// <returns>Danh sách notification data đang scheduled</returns>
        public List<NotificationData> GetScheduledNotifications()
        {
            if (!this._isInitialized)
            {
                Debug.LogError("❌ [NotificationManager] Manager chưa được initialized!");
                return new List<NotificationData>();
            }

            return this._scheduler.GetScheduledNotifications();
        }

        /// <summary>
        /// Khởi tạo các dependencies
        /// </summary>
        private void InitializeDependencies()
        {
            // Create temporary config nếu chưa có
            MobileNotificationConfig config = this._mobileNotificationConfig
                ? this._mobileNotificationConfig
                : MobileNotificationConfig.CreateDevelopmentPreset();

            // Initialize dependencies
            this._permissionHandler = new NotificationPermissionHandler(config);
            this._scheduler = new NotificationScheduler();
            this._service = new MobileNotificationService();
        }

        /// <summary>
        /// Handle permission changed event
        /// </summary>
        private void HandlePermissionChanged(bool granted)
        {
            if (this._mobileNotificationConfig.enableDebugLogs)
            {
                Debug.Log($"🔐 [NotificationManager] Permission changed: {granted}");
            }

            this.OnPermissionChanged?.Invoke(granted);
        }

        /// <summary>
        /// Check notifications đã received và trigger events
        /// </summary>
        private void CheckReceivedNotifications()
        {
            this._isCheckingNotifications = true;

            try
            {
#if UNITY_ANDROID
                this.CheckAndroidNotifications();
#elif UNITY_IOS
                this.CheckIOSNotifications();
#endif
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationManager] Check notifications error: {ex.Message}");
            }
            finally
            {
                this._isCheckingNotifications = false;
            }
        }

#if UNITY_ANDROID
        /// <summary>
        /// Check Android notifications
        /// </summary>
        private void CheckAndroidNotifications()
        {
            var notificationIntentData = AndroidNotificationCenter.GetLastNotificationIntent();

            if (notificationIntentData != null)
            {
                var notificationId = notificationIntentData.Id;
                var customData = notificationIntentData.Notification.IntentData;

                if (this._mobileNotificationConfig.enableDebugLogs)
                {
                    Debug.Log($"📬 [NotificationManager] Received Android notification #{notificationId}");
                }

                // Create notification data from intent
                var notificationData = new NotificationData
                {
                    identifier = notificationId,
                    title = notificationIntentData.Notification.Title,
                    body = notificationIntentData.Notification.Text,
                    customData = customData
                };

                this.OnNotificationReceived?.Invoke(notificationData);
            }
        }
#endif

#if UNITY_IOS
        /// <summary>
        /// Check iOS notifications
        /// </summary>
        private void CheckIOSNotifications()
        {
            var notification = iOSNotificationCenter.GetLastRespondedNotification();

            if (notification != null)
            {
                if (this.config.enableDebugLogs)
                {
                    Debug.Log($"📬 [NotificationManager] Received iOS notification: {notification.Identifier}");
                }

                // Create notification data from iOS notification
                var notificationData = new NotificationData
                {
                    identifier = int.Parse(notification.Identifier),
                    title = notification.Title,
                    body = notification.Body,
                    subtitle = notification.Subtitle,
                    customData = notification.Data
                };

                this.OnNotificationReceived?.Invoke(notificationData);

                // Remove notification để không trigger lại
                iOSNotificationCenter.RemoveDeliveredNotification(notification.Identifier);
            }
        }
#endif
    }
}

