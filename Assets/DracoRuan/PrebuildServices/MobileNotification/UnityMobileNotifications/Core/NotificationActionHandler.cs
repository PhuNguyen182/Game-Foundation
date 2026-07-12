using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Data;

namespace DracoRuan.PrebuildServices.MobileNotification.UnityMobileNotifications.Core
{

    /// <summary>
    /// Handler để xử lý các actions từ notifications khi người chơi tap
    /// </summary>
    /// <remarks>
    /// Class này routing actions đến các handlers tương ứng và
    /// quản lý execution flow của notification actions.
    /// </remarks>
    public class NotificationActionHandler
    {
        // Delegates cho các action handlers
        public delegate UniTask<bool> ActionHandlerDelegate(NotificationAction action, NotificationData notification);

        // Dictionary chứa các registered handlers
        private readonly Dictionary<NotificationActionType, ActionHandlerDelegate> _actionHandlers;

        // Configuration
        private MobileNotificationConfig _config;

        // Callbacks
        private Action<NotificationAction> _onActionStarted;
        private Action<NotificationAction, bool> _onActionCompleted;
        private Action<NotificationAction, string> _onActionError;

        /// <summary>
        /// Constructor
        /// </summary>
        public NotificationActionHandler()
        {
            this._actionHandlers = new Dictionary<NotificationActionType, ActionHandlerDelegate>();
        }

        /// <summary>
        /// Khởi tạo handler với configuration
        /// </summary>
        /// <param name="config">Configuration</param>
        public void Initialize(MobileNotificationConfig config)
        {
            this._config = config;

            if (this._config.enableDebugLogs)
            {
                Debug.Log("⚙️ [NotificationActionHandler] Initialized");
            }
        }

        /// <summary>
        /// Register handler cho một action type
        /// </summary>
        /// <param name="actionType">Loại action</param>
        /// <param name="handler">Handler function</param>
        public void RegisterHandler(NotificationActionType actionType, ActionHandlerDelegate handler)
        {
            if (handler == null)
            {
                Debug.LogWarning($"⚠️ [NotificationActionHandler] Handler is null for {actionType}");
                return;
            }

            this._actionHandlers[actionType] = handler;

            if (this._config && this._config.enableDebugLogs)
            {
                Debug.Log($"✅ [NotificationActionHandler] Handler registered for {actionType}");
            }
        }

        /// <summary>
        /// Unregister handler cho một action type
        /// </summary>
        /// <param name="actionType">Loại action</param>
        public void UnregisterHandler(NotificationActionType actionType)
        {
            if (this._actionHandlers.Remove(actionType))
            {
                if (this._config && this._config.enableDebugLogs)
                {
                    Debug.Log($"🗑️ [NotificationActionHandler] Handler unregistered for {actionType}");
                }
            }
        }

        /// <summary>
        /// Set callback khi action bắt đầu
        /// </summary>
        /// <param name="callback">Callback function</param>
        public void SetOnActionStarted(Action<NotificationAction> callback)
        {
            this._onActionStarted = callback;
        }

        /// <summary>
        /// Set callback khi action complete
        /// </summary>
        /// <param name="callback">Callback function (action, success)</param>
        public void SetOnActionCompleted(Action<NotificationAction, bool> callback)
        {
            this._onActionCompleted = callback;
        }

        /// <summary>
        /// Set callback khi action có error
        /// </summary>
        /// <param name="callback">Callback function (action, errorMessage)</param>
        public void SetOnActionError(Action<NotificationAction, string> callback)
        {
            this._onActionError = callback;
        }

        /// <summary>
        /// Process notification và execute action nếu có
        /// </summary>
        /// <param name="notification">Notification data</param>
        /// <returns>True nếu action được executed successfully</returns>
        public async UniTask<bool> ProcessNotificationAsync(NotificationData notification)
        {
            if (notification == null)
            {
                Debug.LogWarning("⚠️ [NotificationActionHandler] Notification is null");
                return false;
            }

            if (string.IsNullOrWhiteSpace(notification.customData))
            {
                if (this._config && this._config.enableDebugLogs)
                {
                    Debug.Log($"📭 [NotificationActionHandler] No custom data in notification: {notification.title}");
                }
                return false;
            }

            try
            {
                // Parse action từ customData
                var action = NotificationAction.FromJson(notification.customData);

                if (action == null || !action.IsValid())
                {
                    if (this._config && this._config.enableDebugLogs)
                    {
                        Debug.LogWarning($"⚠️ [NotificationActionHandler] Invalid action in notification: {notification.title}");
                    }
                    return false;
                }

                // Execute action
                return await this.ExecuteActionAsync(action, notification);
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationActionHandler] Error processing notification: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Execute một action
        /// </summary>
        /// <param name="action">Action cần execute</param>
        /// <param name="notification">Notification gốc</param>
        /// <returns>True nếu success</returns>
        public async UniTask<bool> ExecuteActionAsync(NotificationAction action, NotificationData notification)
        {
            if (action == null || !action.IsValid())
            {
                Debug.LogWarning("⚠️ [NotificationActionHandler] Invalid action");
                return false;
            }

            try
            {
                if (this._config && this._config.enableDebugLogs)
                {
                    Debug.Log($"🎯 [NotificationActionHandler] Executing action: {action.actionType} for {notification.title}");
                }

                // Trigger onActionStarted callback
                this._onActionStarted?.Invoke(action);

                // Check nếu có registered handler
                if (!this._actionHandlers.TryGetValue(action.actionType, out var handler))
                {
                    Debug.LogWarning($"⚠️ [NotificationActionHandler] No handler registered for {action.actionType}");
                    
                    // Trigger error callback
                    this._onActionError?.Invoke(action, $"No handler for {action.actionType}");
                    return false;
                }

                // Execute handler
                var success = await handler(action, notification);

                if (this._config && this._config.enableDebugLogs)
                {
                    Debug.Log($"{(success ? "✅" : "❌")} [NotificationActionHandler] Action {action.actionType} {(success ? "completed" : "failed")}");
                }

                // Trigger onActionCompleted callback
                this._onActionCompleted?.Invoke(action, success);

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationActionHandler] Error executing action: {ex.Message}");
                
                // Trigger error callback
                this._onActionError?.Invoke(action, ex.Message);
                
                return false;
            }
        }

        /// <summary>
        /// Check nếu có handler cho action type
        /// </summary>
        /// <param name="actionType">Action type</param>
        /// <returns>True nếu có handler</returns>
        public bool HasHandler(NotificationActionType actionType)
        {
            return this._actionHandlers.ContainsKey(actionType);
        }

        /// <summary>
        /// Lấy số lượng handlers đã registered
        /// </summary>
        /// <value>Số lượng handlers</value>
        public int RegisteredHandlerCount => this._actionHandlers.Count;

        /// <summary>
        /// Clear tất cả handlers
        /// </summary>
        public void ClearHandlers()
        {
            this._actionHandlers.Clear();

            if (this._config && this._config.enableDebugLogs)
            {
                Debug.Log("🗑️ [NotificationActionHandler] All handlers cleared");
            }
        }
    }
}

