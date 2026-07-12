using System;
using Cysharp.Threading.Tasks;
using DracoRuan.MobileNotification.UnityMobileNotifications.Data;
using UnityEngine;

namespace DracoRuan.MobileNotification.UnityMobileNotifications.Core
{

    /// <summary>
    /// Router để điều hướng người chơi đến đúng screen/feature khi tap notification
    /// </summary>
    /// <remarks>
    /// Class này cung cấp các built-in handlers cho common notification actions
    /// và cho phép register custom handlers cho game-specific logic.
    /// </remarks>
    public class NotificationRouter : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Enable debug logs")]
        private bool enableDebugLogs = true;

        [SerializeField]
        [Tooltip("Delay trước khi execute action (milliseconds)")]
        private int actionDelayMs = 500;

        [Header("UI References")]
        [SerializeField]
        [Tooltip("Loading screen để hiển thị khi processing action")]
        private GameObject loadingScreen;

        // Action handler
        private NotificationActionHandler _actionHandler;

        // Callbacks cho routing
        public event Action<string> OnOpenScreen;
        public event Action<string> OnOpenLevel;
        public event Action<string> OnOpenShop;
        public event Action<string> OnClaimReward;
        public event Action OnOpenDailyReward;
        public event Action OnOpenAchievements;
        public event Action<string> OnOpenEvent;
        public event Action OnOpenProfile;
        public event Action<string> OnStartLevel;
        public event Action<string, string> OnCustomAction;

        /// <summary>
        /// Unity Awake lifecycle
        /// </summary>
        private void Awake()
        {
            this.InitializeRouter();
        }

        /// <summary>
        /// Khởi tạo router và register default handlers
        /// </summary>
        private void InitializeRouter()
        {
            // Create action handler
            this._actionHandler = new NotificationActionHandler();

            // Tạo temporary config cho handler
            var tempConfig = ScriptableObject.CreateInstance<MobileNotificationConfig>();
            tempConfig.enableDebugLogs = this.enableDebugLogs;
            this._actionHandler.Initialize(tempConfig);

            // Register default handlers
            this.RegisterDefaultHandlers();

            // Set callbacks
            this._actionHandler.SetOnActionStarted(this.HandleActionStarted);
            this._actionHandler.SetOnActionCompleted(this.HandleActionCompleted);
            this._actionHandler.SetOnActionError(this.HandleActionError);

            if (this.enableDebugLogs)
            {
                Debug.Log("🗺️ [NotificationRouter] Router initialized");
            }
        }

        /// <summary>
        /// Register các default handlers
        /// </summary>
        private void RegisterDefaultHandlers()
        {
            // OpenScreen handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenScreen,
                this.HandleOpenScreenAction
            );

            // OpenLevel handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenLevel,
                this.HandleOpenLevelAction
            );

            // OpenShop handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenShop,
                this.HandleOpenShopAction
            );

            // ClaimReward handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.ClaimReward,
                this.HandleClaimRewardAction
            );

            // OpenDailyReward handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenDailyReward,
                this.HandleOpenDailyRewardAction
            );

            // OpenAchievements handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenAchievements,
                this.HandleOpenAchievementsAction
            );

            // OpenEvent handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenEvent,
                this.HandleOpenEventAction
            );

            // OpenProfile handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.OpenProfile,
                this.HandleOpenProfileAction
            );

            // StartLevel handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.StartLevel,
                this.HandleStartLevelAction
            );

            // CustomAction handler
            this._actionHandler.RegisterHandler(
                NotificationActionType.CustomAction,
                this.HandleCustomAction
            );
        }

        /// <summary>
        /// Route notification đến handler tương ứng
        /// </summary>
        /// <param name="notification">Notification data</param>
        /// <returns>True nếu routing successful</returns>
        public async UniTask<bool> RouteNotificationAsync(NotificationData notification)
        {
            if (notification == null)
            {
                Debug.LogWarning("⚠️ [NotificationRouter] Notification is null");
                return false;
            }

            if (this.enableDebugLogs)
            {
                Debug.Log($"🗺️ [NotificationRouter] Routing notification: {notification.title}");
            }

            try
            {
                // Delay nhỏ để đảm bảo app đã ready
                await UniTask.Delay(this.actionDelayMs);

                // Process notification
                var success = await this._actionHandler.ProcessNotificationAsync(notification);

                return success;
            }
            catch (Exception ex)
            {
                Debug.LogError($"❌ [NotificationRouter] Routing error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Register custom handler cho notification action type
        /// </summary>
        /// <param name="actionType">Action type</param>
        /// <param name="handler">Handler function</param>
        public void RegisterCustomHandler(
            NotificationActionType actionType,
            NotificationActionHandler.ActionHandlerDelegate handler)
        {
            if (this._actionHandler == null)
            {
                Debug.LogError("❌ [NotificationRouter] Action handler not initialized!");
                return;
            }

            this._actionHandler.RegisterHandler(actionType, handler);

            if (this.enableDebugLogs)
            {
                Debug.Log($"✅ [NotificationRouter] Custom handler registered for {actionType}");
            }
        }

        // ============================================
        // Default Action Handlers
        // ============================================

        private async UniTask<bool> HandleOpenScreenAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"📱 [NotificationRouter] Opening screen: {action.targetId}");
            }

            // Trigger event cho game code
            this.OnOpenScreen?.Invoke(action.targetId);

            // Simulate async operation
            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenLevelAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"🎮 [NotificationRouter] Opening level: {action.targetId}");
            }

            // Trigger event
            this.OnOpenLevel?.Invoke(action.targetId);

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenShopAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"🛒 [NotificationRouter] Opening shop with item: {action.targetId}");
            }

            // Trigger event
            this.OnOpenShop?.Invoke(action.targetId);

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleClaimRewardAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"🎁 [NotificationRouter] Claiming reward: {action.targetId}");
            }

            // Trigger event
            this.OnClaimReward?.Invoke(action.targetId);

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenDailyRewardAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log("🎁 [NotificationRouter] Opening daily reward");
            }

            // Trigger event
            this.OnOpenDailyReward?.Invoke();

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenAchievementsAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log("🏆 [NotificationRouter] Opening achievements");
            }

            // Trigger event
            this.OnOpenAchievements?.Invoke();

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenEventAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"🎪 [NotificationRouter] Opening event: {action.targetId}");
            }

            // Trigger event
            this.OnOpenEvent?.Invoke(action.targetId);

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleOpenProfileAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log("👤 [NotificationRouter] Opening profile");
            }

            // Trigger event
            this.OnOpenProfile?.Invoke();

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleStartLevelAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"▶️ [NotificationRouter] Starting level: {action.targetId}");
            }

            // Trigger event
            this.OnStartLevel?.Invoke(action.targetId);

            await UniTask.Yield();

            return true;
        }

        private async UniTask<bool> HandleCustomAction(NotificationAction action, NotificationData notification)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"⚙️ [NotificationRouter] Custom action: {action.targetId}");
            }

            // Trigger event với action ID và parameters
            this.OnCustomAction?.Invoke(action.targetId, action.parameters);

            await UniTask.Yield();

            return true;
        }

        // ============================================
        // Callback Handlers
        // ============================================

        private void HandleActionStarted(NotificationAction action)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"⏳ [NotificationRouter] Action started: {action.actionType}");
            }

            // Show loading screen nếu action yêu cầu
            if (action.showLoading && this.loadingScreen != null)
            {
                this.loadingScreen.SetActive(true);
            }
        }

        private void HandleActionCompleted(NotificationAction action, bool success)
        {
            if (this.enableDebugLogs)
            {
                Debug.Log($"{(success ? "✅" : "❌")} [NotificationRouter] Action completed: {action.actionType}");
            }

            // Hide loading screen
            if (this.loadingScreen != null && this.loadingScreen.activeSelf)
            {
                this.loadingScreen.SetActive(false);
            }
        }

        private void HandleActionError(NotificationAction action, string errorMessage)
        {
            Debug.LogError($"❌ [NotificationRouter] Action error: {action.actionType} - {errorMessage}");

            // Hide loading screen
            if (this.loadingScreen != null && this.loadingScreen.activeSelf)
            {
                this.loadingScreen.SetActive(false);
            }
        }
    }
}

