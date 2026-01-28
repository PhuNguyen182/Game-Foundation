using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Manager;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Examples
{
    /// <summary>
    /// Example sử dụng TimeScheduleService với load/start pattern và persistence options
    /// </summary>
    public class TimeScheduleUsageExample : MonoBehaviour
    {
        private TimeScheduleManager _timeScheduleManager;
        
        private void Start()
        {
            // ============================================
            // PERSISTENCE OPTIONS
            // ============================================
            
            // Option 1: Sử dụng File persistence (Recommended - Default)
            this._timeScheduleManager = new TimeScheduleManager();
            // Hoặc explicit:
            // this._timeScheduleManager = new TimeScheduleManager(TimerPersistenceType.File);
            
            // Option 2: Sử dụng PlayerPrefs persistence
            // this._timeScheduleManager = new TimeScheduleManager(TimerPersistenceType.PlayerPrefs);
            
            // ============================================
            
            // Tất cả timers đã được load từ save data nhưng ở trạng thái PAUSED
            // Bạn cần gọi StartLoadedCountdownTimer() để bắt đầu đếm ngược
            
            // Ví dụ sử dụng
            this.ExampleCreateNewTimer();
            this.ExampleStartLoadedTimer();
            this.ExampleTimerEvents();
            this.ExamplePersistenceComparison();
        }
        
        /// <summary>
        /// Tạo timer mới và bắt đầu đếm ngược ngay
        /// </summary>
        private void ExampleCreateNewTimer()
        {
            // Tạo timer mới với key và duration
            var timer = this._timeScheduleManager.StartCountdownTimer("skill_cooldown", 60f);
            
            // Subscribe events
            timer.OnUpdate += (remainingSeconds) =>
            {
                Debug.Log($"Skill cooldown: {remainingSeconds:F2}s remaining");
            };
            
            timer.OnComplete += () =>
            {
                Debug.Log("Skill cooldown completed!");
            };
        }
        
        /// <summary>
        /// Bắt đầu timer đã được load từ save data
        /// </summary>
        private void ExampleStartLoadedTimer()
        {
            // Kiểm tra xem timer có tồn tại không (đã được load từ save)
            if (this._timeScheduleManager.HasCountdownTimer("daily_reward"))
            {
                // Bắt đầu đếm ngược cho timer đã load
                var timer = this._timeScheduleManager.StartLoadedCountdownTimer("daily_reward");
                
                Debug.Log($"Started daily reward timer: {timer.RemainingSeconds:F2}s remaining");
                
                // Subscribe events nếu cần
                timer.OnComplete += () =>
                {
                    Debug.Log("Daily reward is ready!");
                };
            }
            else
            {
                Debug.Log("Daily reward timer not found in save data");
            }
        }
        
        /// <summary>
        /// Ví dụ về event handling
        /// </summary>
        private void ExampleTimerEvents()
        {
            // Tạo timer
            var eventTimer = this._timeScheduleManager.StartCountdownTimer("event_timer", 120f);
            
            // Subscribe multiple events
            eventTimer.OnUpdate += this.HandleTimerUpdate;
            eventTimer.OnComplete += this.HandleTimerComplete;
            
            // Pause/Resume timer
            if (eventTimer.IsActive)
            {
                eventTimer.Pause();
                Debug.Log("Timer paused");
            }
            
            if (eventTimer.IsPaused)
            {
                eventTimer.Resume();
                Debug.Log("Timer resumed");
            }
        }
        
        private void HandleTimerUpdate(float remainingSeconds)
        {
            // Update UI or game logic
            Debug.Log($"Event timer: {remainingSeconds:F2}s");
        }
        
        private void HandleTimerComplete()
        {
            Debug.Log("Event timer completed!");
        }
        
        /// <summary>
        /// Ví dụ kiểm tra trạng thái timer
        /// </summary>
        public void CheckTimerStatus(string timerKey)
        {
            var timer = this._timeScheduleManager.GetCountdownTimer(timerKey);
            
            if (timer == null)
            {
                Debug.Log($"Timer '{timerKey}' not found");
                return;
            }
            
            Debug.Log($"Timer '{timerKey}' status:");
            Debug.Log($"- IsActive: {timer.IsActive}");
            Debug.Log($"- IsPaused: {timer.IsPaused}");
            Debug.Log($"- IsExpired: {timer.IsExpired}");
            Debug.Log($"- RemainingSeconds: {timer.RemainingSeconds:F2}");
            Debug.Log($"- TotalDuration: {timer.TotalDuration:F2}");
        }
        
        /// <summary>
        /// Ví dụ workflow hoàn chỉnh
        /// </summary>
        public void CompleteWorkflowExample()
        {
            // 1. Tạo timer mới
            var timer = this._timeScheduleManager.StartCountdownTimer("quest_timer", 300f);
            
            // 2. Subscribe events
            timer.OnUpdate += (remaining) =>
            {
                // Cập nhật UI mỗi giây
                this.UpdateQuestTimerUI(remaining);
            };
            
            timer.OnComplete += () =>
            {
                // Quest failed vì hết thời gian
                this.HandleQuestTimeout();
            };
            
            // 3. Pause khi cần (ví dụ: game pause)
            if (this.IsGamePaused())
            {
                timer.Pause();
            }
            
            // 4. Resume khi continue
            if (this.IsGameResumed())
            {
                timer.Resume();
            }
            
            // 5. Remove timer khi không cần (ví dụ: complete quest sớm)
            if (this.IsQuestCompleted())
            {
                this._timeScheduleManager.RemoveCountdownTimer("quest_timer");
            }
        }
        
        private void UpdateQuestTimerUI(float remainingSeconds)
        {
            // Implement UI update
        }
        
        private void HandleQuestTimeout()
        {
            // Implement quest timeout logic
        }
        
        private bool IsGamePaused() => false;
        private bool IsGameResumed() => false;
        private bool IsQuestCompleted() => false;
        
        /// <summary>
        /// So sánh giữa File và PlayerPrefs persistence
        /// </summary>
        private void ExamplePersistenceComparison()
        {
            Debug.Log("=== Persistence Comparison ===");
            Debug.Log("File Persistence (Recommended):");
            Debug.Log("  + Lưu vào Application.persistentDataPath/PD/TimerData.json");
            Debug.Log("  + Dễ dàng backup và restore");
            Debug.Log("  + Có thể edit trực tiếp file JSON");
            Debug.Log("  + Không giới hạn dung lượng như PlayerPrefs");
            Debug.Log("");
            Debug.Log("PlayerPrefs Persistence:");
            Debug.Log("  + Lưu vào registry (Windows) hoặc plist (iOS)");
            Debug.Log("  + Đơn giản và nhanh");
            Debug.Log("  + Có giới hạn dung lượng");
            Debug.Log("  + Khó backup và restore");
        }
        
        private void OnDestroy()
        {
            // TimeScheduleManager sẽ tự động save tất cả timers khi Dispose
            this._timeScheduleManager?.Dispose();
        }
    }
}

