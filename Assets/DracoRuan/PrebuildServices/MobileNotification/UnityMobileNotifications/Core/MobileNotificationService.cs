using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.MobileNotification.UnityMobileNotifications.Data;
using DracoRuan.MobileNotification.UnityMobileNotifications.Interfaces;
using UnityEngine;

namespace DracoRuan.MobileNotification.UnityMobileNotifications.Core
{
    /// <summary>
    /// Service xử lý business logic của notification system
    /// </summary>
    /// <remarks>
    /// Service này xử lý các tác vụ như validate, transform data,
    /// quản lý checkpoints và tính toán timing cho notifications.
    /// </remarks>
    public class MobileNotificationService : IMobileNotificationService, IDisposable
    {
        /// <summary>
        /// Serializable data structure cho checkpoints
        /// </summary>
        [Serializable]
        private class CheckpointData
        {
            public List<CheckpointEntry> checkpoints = new();
        }

        /// <summary>
        /// Serializable checkpoint entry
        /// </summary>
        [Serializable]
        private class CheckpointEntry
        {
            public string name;
            public long timestamp;
        }
        
        private const string CheckpointDataKey = "MobileNotifications_Checkpoints";
        
        private MobileNotificationConfig _config;
        private readonly Dictionary<string, long> _checkpoints;

        /// <summary>
        /// Lấy configuration hiện tại của service
        /// </summary>
        /// <value>Configuration đang được sử dụng</value>
        public MobileNotificationConfig CurrentConfig => this._config;

        /// <summary>
        /// Constructor mặc định
        /// </summary>
        public MobileNotificationService()
        {
            this._checkpoints = new Dictionary<string, long>();
        }

        /// <summary>
        /// Khởi tạo notification service với configuration
        /// </summary>
        /// <param name="config">Configuration cho service</param>
        public async UniTask InitializeAsync(MobileNotificationConfig config)
        {
            try
            {
                this._config = config;

                if (this._config.enableDebugLogs)
                {
                    Debug.Log("⚙️ [NotificationService] Initializing service...");
                }

                // Load saved checkpoints nếu có
                this.LoadCheckpoints();

                if (this._config.enableDebugLogs)
                {
                    Debug.Log("✅ [NotificationService] Service initialized successfully");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationService] Initialization failed: {ex.Message}");
            }

            await UniTask.CompletedTask;
        }

        /// <summary>
        /// Validate notification data trước khi schedule
        /// </summary>
        /// <param name="data">Notification data cần validate</param>
        /// <returns>True nếu data hợp lệ</returns>
        public bool ValidateNotificationData(NotificationData data)
        {
            if (data == null)
            {
                if (this._config.enableDebugLogs)
                {
                    Debug.LogWarning("⚠️ [NotificationService] Notification data is null");
                }
                return false;
            }

            // Validate basic data
            if (!data.IsValid())
            {
                if (this._config.enableDebugLogs)
                {
                    Debug.LogWarning($"⚠️ [NotificationService] Invalid notification data: {data.title}");
                }
                return false;
            }

            // Validate fire time không quá xa trong tương lai
            var maxFireTime = 60L * 60L * 24L * 365L; // 1 năm
            if (data.fireTimeInSeconds > maxFireTime)
            {
                if (this._config.enableDebugLogs)
                {
                    Debug.LogWarning($"⚠️ [NotificationService] Fire time quá xa: {data.fireTimeInSeconds}s");
                }
                return false;
            }

            // Validate repeat interval nếu có
            if (data.repeats)
            {
                var minRepeatInterval = 60L; // 1 phút minimum
                if (data.repeatInterval < minRepeatInterval)
                {
                    if (this._config.enableDebugLogs)
                    {
                        Debug.LogWarning($"⚠️ [NotificationService] Repeat interval quá ngắn: {data.repeatInterval}s");
                    }
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Xử lý notification scenario và chuyển thành danh sách notifications
        /// </summary>
        /// <param name="scenario">Scenario cần xử lý</param>
        /// <returns>Danh sách notification data từ scenario</returns>
        public List<NotificationData> ProcessScenario(NotificationScenario scenario)
        {
            var processedNotifications = new List<NotificationData>();

            if (!scenario || !scenario.IsValid())
            {
                if (this._config.enableDebugLogs)
                {
                    Debug.LogWarning("⚠️ [NotificationService] Invalid scenario");
                }
                return processedNotifications;
            }

            if (this._config.enableDebugLogs)
            {
                Debug.Log($"⚙️ [NotificationService] Processing scenario: {scenario.scenarioName}");
            }

            // Lấy checkpoint timestamp nếu scenario sử dụng checkpoint
            long checkpointTime = 0;
            if (scenario.useCheckpoint)
            {
                if (this._checkpoints.TryGetValue(scenario.checkpointName, out var timestamp))
                {
                    checkpointTime = timestamp;
                    
                    if (this._config.enableDebugLogs)
                    {
                        Debug.Log($"📍 [NotificationService] Using checkpoint '{scenario.checkpointName}' with timestamp: {checkpointTime}");
                    }
                }
                else
                {
                    // Checkpoint không tồn tại, sử dụng thời gian hiện tại
                    checkpointTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    
                    if (this._config.enableDebugLogs)
                    {
                        Debug.LogWarning($"⚠️ [NotificationService] Checkpoint '{scenario.checkpointName}' không tồn tại, dùng thời gian hiện tại");
                    }
                }
            }

            // Process từng notification trong scenario
            for (int i = 0; i < scenario.notifications.Count; i++)
            {
                var notification = scenario.notifications[i].Clone();

                // Apply group key từ scenario nếu có
                if (!string.IsNullOrWhiteSpace(scenario.groupKey))
                {
                    notification.groupKey = scenario.groupKey;
                }

                // Tính toán fire time dựa vào checkpoint nếu có
                if (scenario.useCheckpoint && checkpointTime > 0)
                {
                    var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                    var timeSinceCheckpoint = currentTime - checkpointTime;
                    var originalFireTime = notification.fireTimeInSeconds;
                    
                    // Adjust fire time: nếu time đã qua checkpoint thì trigger ngay
                    notification.fireTimeInSeconds = (long)Mathf.Max(0, originalFireTime - timeSinceCheckpoint);
                    
                    if (this._config.enableDebugLogs)
                    {
                        Debug.Log($"⏱️ [NotificationService] Adjusted fire time: {originalFireTime}s → {notification.fireTimeInSeconds}s");
                    }
                }

                // Validate processed notification
                if (this.ValidateNotificationData(notification))
                {
                    processedNotifications.Add(notification);
                }
            }

            if (this._config.enableDebugLogs)
            {
                Debug.Log($"✅ [NotificationService] Processed {processedNotifications.Count}/{scenario.notifications.Count} notifications");
            }

            return processedNotifications;
        }

        /// <summary>
        /// Tính toán thời gian trigger cho notification dựa vào checkpoint
        /// </summary>
        /// <param name="checkpointName">Tên checkpoint</param>
        /// <param name="delayInSeconds">Thời gian delay từ checkpoint (giây)</param>
        /// <returns>Thời gian trigger tính theo seconds từ bây giờ</returns>
        public long CalculateTriggerTime(string checkpointName, long delayInSeconds)
        {
            if (string.IsNullOrWhiteSpace(checkpointName))
            {
                // Không có checkpoint, return delay trực tiếp
                return delayInSeconds;
            }

            if (!this._checkpoints.TryGetValue(checkpointName, out var checkpointTime))
            {
                // Checkpoint không tồn tại, return delay trực tiếp
                if (this._config.enableDebugLogs)
                {
                    Debug.LogWarning($"⚠️ [NotificationService] Checkpoint '{checkpointName}' không tồn tại");
                }
                return delayInSeconds;
            }

            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var timeSinceCheckpoint = currentTime - checkpointTime;
            var targetTime = delayInSeconds - timeSinceCheckpoint;

            // Ensure không âm
            return (long)Mathf.Max(0, targetTime);
        }

        /// <summary>
        /// Cập nhật checkpoint cho việc tính toán notification timing
        /// </summary>
        /// <param name="checkpointName">Tên checkpoint</param>
        /// <param name="timestamp">Timestamp của checkpoint</param>
        public void UpdateCheckpoint(string checkpointName, long timestamp)
        {
            if (string.IsNullOrWhiteSpace(checkpointName))
            {
                Debug.LogWarning("⚠️ [NotificationService] Checkpoint name cannot be empty");
                return;
            }

            this._checkpoints[checkpointName] = timestamp;

            if (this._config.enableDebugLogs)
            {
                Debug.Log($"📍 [NotificationService] Checkpoint updated: {checkpointName} = {timestamp}");
            }

            // Save checkpoints to PlayerPrefs
            this.SaveCheckpoints();
        }

        /// <summary>
        /// Load checkpoints từ PlayerPrefs
        /// </summary>
        private void LoadCheckpoints()
        {
            try
            {
                var checkpointsJson = PlayerPrefs.GetString(CheckpointDataKey, "{}");
                var checkpointDict = JsonUtility.FromJson<CheckpointData>(checkpointsJson);

                if (checkpointDict is { checkpoints: not null })
                {
                    this._checkpoints.Clear();
                    for (int i = 0; i < checkpointDict.checkpoints.Count; i++)
                    {
                        var checkpoint = checkpointDict.checkpoints[i];
                        this._checkpoints[checkpoint.name] = checkpoint.timestamp;
                    }

                    if (this._config.enableDebugLogs)
                    {
                        Debug.Log($"📍 [NotificationService] Loaded {this._checkpoints.Count} checkpoints");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationService] Error loading checkpoints: {ex.Message}");
            }
        }

        /// <summary>
        /// Save checkpoints to PlayerPrefs
        /// </summary>
        private void SaveCheckpoints()
        {
            try
            {
                var checkpointData = new CheckpointData
                {
                    checkpoints = new List<CheckpointEntry>()
                };

                foreach (var kvp in this._checkpoints)
                {
                    checkpointData.checkpoints.Add(new CheckpointEntry
                    {
                        name = kvp.Key,
                        timestamp = kvp.Value
                    });
                }

                string json = JsonUtility.ToJson(checkpointData);
                PlayerPrefs.SetString(CheckpointDataKey, json);
                PlayerPrefs.Save();

                if (this._config.enableDebugLogs)
                {
                    Debug.Log($"💾 [NotificationService] Saved {this._checkpoints.Count} checkpoints");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationService] Error saving checkpoints: {ex.Message}");
            }
        }

        public void Dispose()
        {
            this._checkpoints?.Clear();
        }
    }
}

