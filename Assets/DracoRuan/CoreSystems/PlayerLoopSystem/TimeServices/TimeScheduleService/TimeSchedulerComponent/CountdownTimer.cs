using System;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Extensions;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeSchedulerComponent
{
    /// <summary>
    /// Bộ đếm thời gian với hệ thống tier - khi hết thời gian sẽ tự động trừ tier
    /// </summary>
    public class CountdownTimer : ICountdownTimer
    {
        private const float UpdateThresholdSeconds = 1f;
        
        private bool _disposed;
        private string _key;
        private long _endTimeUnix;
        private long _startTimeUnix;
        private float _totalDuration;
        private float _cachedRemainingSeconds;
        private bool _isExpired;
        private bool _isPaused;
        private long _pausedTimeUnix;
        private long _lastUpdateTimeUnix;
        private int _tierCount;
        private int _currentTier;
        
        public string Key => this._key;
        
        public float RemainingSeconds
        {
            get
            {
                if (this._isExpired)
                {
                    return 0f;
                }
                
                if (this._isPaused)
                {
                    return this._cachedRemainingSeconds;
                }
                
                this.UpdateRealTime();
                return this._cachedRemainingSeconds;
            }
        }
        
        public TimeSpan RemainingTime => TimeSpan.FromSeconds(this.RemainingSeconds);
        
        public float TotalDuration => this._totalDuration;
        
        public int TierCount => this._tierCount;
        
        public int CurrentTier => this._currentTier;
        
        public bool IsActive => !this._isExpired && !this._isPaused && this.RemainingSeconds > 0f;
        public bool IsExpired => this._isExpired;
        public bool IsPaused => this._isPaused;
        
        public event Action<float> OnUpdate;
        public event Action OnComplete;
        public event Action<int> OnTierChanged;
        
        /// <summary>
        /// Khởi tạo bộ đếm thời gian với tier count tùy chỉnh
        /// </summary>
        /// <param name="key">Khóa định danh duy nhất</param>
        /// <param name="durationSeconds">Thời gian mỗi tier (giây)</param>
        /// <param name="tierCount">Số lượng tier (mặc định = 1, nếu = 0 thì timer kết thúc ngay)</param>
        /// <param name="startPaused">Bắt đầu ở trạng thái tạm dừng</param>
        public CountdownTimer(string key, float durationSeconds, int tierCount = 1, bool startPaused = false)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }
            
            if (durationSeconds <= 0f)
            {
                throw new ArgumentException("Duration must be positive", nameof(durationSeconds));
            }
            
            if (tierCount < 0)
            {
                throw new ArgumentException("Tier count cannot be negative", nameof(tierCount));
            }

            this._key = key;
            this._totalDuration = durationSeconds;
            this._tierCount = tierCount;
            this._currentTier = tierCount;
            this._startTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            this._endTimeUnix = this._startTimeUnix + (long)durationSeconds;
            this._cachedRemainingSeconds = durationSeconds;
            this._isPaused = startPaused;
            this._pausedTimeUnix = startPaused ? this._startTimeUnix : 0;
            this._lastUpdateTimeUnix = this._startTimeUnix;
            
            // Nếu tier count = 0, kết thúc ngay
            this._isExpired = tierCount == 0;
        }
        
        public CountdownTimer(CountdownTimerData data)
        {
            if (!this.InitializeFromSaveData(data))
            {
                throw new ArgumentException("Invalid save data", nameof(data));
            }
        }
        
        ~CountdownTimer() => this.Dispose(false);
        
        /// <summary>
        /// Cập nhật trạng thái bộ đếm theo thời gian thực với xử lý tier
        /// </summary>
        public void UpdateRealTime()
        {
            if (this._isExpired || this._isPaused)
            {
                return;
            }

            long currentTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            
            // Performance optimization: only update if enough time has passed
            if (currentTimeUnix - this._lastUpdateTimeUnix < UpdateThresholdSeconds)
            {
                return;
            }
            
            this._lastUpdateTimeUnix = currentTimeUnix;
            long remainingTimeUnix = this._endTimeUnix - currentTimeUnix;
            
            // Xử lý khi thời gian vượt quá (có thể do offline lâu)
            if (remainingTimeUnix <= 0)
            {
                this.ProcessTimeExpired(currentTimeUnix);
                return;
            }
            
            this._cachedRemainingSeconds = remainingTimeUnix;
            this.OnUpdate?.Invoke(this._cachedRemainingSeconds);
        }
        
        /// <summary>
        /// Xử lý logic khi hết thời gian - tính toán tier bị mất và cập nhật trạng thái
        /// </summary>
        /// <param name="currentTimeUnix">Thời gian Unix hiện tại</param>
        private void ProcessTimeExpired(long currentTimeUnix)
        {
            // Tính số giây đã vượt quá
            long totalElapsedTimeUnix = currentTimeUnix - this._startTimeUnix;
            
            // Tính số tier đã hoàn thành (làm tròn xuống)
            int completedTiers = (int)(totalElapsedTimeUnix / (long)this._totalDuration);
            
            // Tính tier cần trừ đi
            int tiersToDeduct = Mathf.Min(completedTiers, this._currentTier);
            
            if (tiersToDeduct > 0)
            {
                int previousTier = this._currentTier;
                this._currentTier = Mathf.Max(0, this._currentTier - tiersToDeduct);
                
                // Kích hoạt event tier changed
                if (this._currentTier != previousTier)
                {
                    this.OnTierChanged?.Invoke(this._currentTier);
                }
            }
            
            // Kiểm tra xem còn tier nào không
            if (this._currentTier <= 0)
            {
                this._cachedRemainingSeconds = 0f;
                this.Complete();
                return;
            }
            
            // Còn tier thì reset lại thời gian cho tier tiếp theo
            long timeSpentInCurrentCycle = totalElapsedTimeUnix % (long)this._totalDuration;
            this._startTimeUnix = currentTimeUnix - timeSpentInCurrentCycle;
            this._endTimeUnix = this._startTimeUnix + (long)this._totalDuration;
            this._cachedRemainingSeconds = this._endTimeUnix - currentTimeUnix;
            
            // Thông báo update với thời gian còn lại mới
            this.OnUpdate?.Invoke(this._cachedRemainingSeconds);
        }
        
        /// <summary>
        /// Lấy dữ liệu để lưu trữ bao gồm thông tin tier
        /// </summary>
        public CountdownTimerData GetSaveData()
        {
            CountdownTimerData data = new CountdownTimerData
            {
                key = this._key,
                endTimeUnix = this._endTimeUnix,
                startTimeUnix = this._startTimeUnix,
                totalDuration = this._totalDuration,
                tierCount = this._tierCount,
                currentTier = this._currentTier
            };
            
            return data;
        }
        
        /// <summary>
        /// Khởi tạo bộ đếm từ dữ liệu đã lưu với xử lý tier
        /// </summary>
        public bool InitializeFromSaveData(CountdownTimerData data)
        {
            if (string.IsNullOrEmpty(data.key))
            {
                return false;
            }
            
            if (data.totalDuration <= 0f)
            {
                return false;
            }
            
            if (data.tierCount < 0 || data.currentTier < 0)
            {
                return false;
            }

            this._key = data.key;
            this._endTimeUnix = data.endTimeUnix;
            this._startTimeUnix = data.startTimeUnix;
            this._totalDuration = data.totalDuration;
            this._tierCount = data.tierCount;
            this._currentTier = data.currentTier;
            this._isPaused = true; // Start in paused state when loading
            this._pausedTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            this._lastUpdateTimeUnix = this._pausedTimeUnix;
            
            // Nếu tier count hoặc current tier = 0, kết thúc ngay
            this._isExpired = data.tierCount == 0 || data.currentTier == 0;
            
            if (!this._isExpired)
            {
                this.UpdateRealTime();
            }
            
            return true;
        }
        
        public void Resume()
        {
            if (!this._isPaused || this._isExpired)
            {
                return;
            }

            long currentTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            long pausedDuration = currentTimeUnix - this._pausedTimeUnix;
            
            // Adjust end time based on pause duration
            this._endTimeUnix += pausedDuration;
            this._isPaused = false;
            this._pausedTimeUnix = 0;
            this._lastUpdateTimeUnix = currentTimeUnix;
            
            this.UpdateRealTime();
        }
        
        public void Pause()
        {
            if (this._isPaused || this._isExpired)
            {
                return;
            }

            this._isPaused = true;
            this._pausedTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            this.UpdateRealTime();
        }
        
        /// <summary>
        /// Hoàn thành bộ đếm và kích hoạt sự kiện OnComplete
        /// </summary>
        public void Complete()
        {
            if (this._isExpired)
            {
                return;
            }

            this._isExpired = true;
            this._cachedRemainingSeconds = 0f;
            this._currentTier = 0;
            this.OnComplete?.Invoke();
        }
        
        /// <summary>
        /// Reset bộ đếm với thời gian mới và khôi phục tier về giá trị ban đầu
        /// </summary>
        /// <param name="newDuration">Thời gian mới cho mỗi tier</param>
        public void Reset(float newDuration)
        {
            if (newDuration <= 0f)
            {
                return;
            }

            this._totalDuration = newDuration;
            this._startTimeUnix = TimeExtensions.GetCurrentUtcTimestampInSeconds();
            this._endTimeUnix = this._startTimeUnix + (long)newDuration;
            this._cachedRemainingSeconds = newDuration;
            this._currentTier = this._tierCount; // Reset tier về giá trị ban đầu
            this._isExpired = this._tierCount == 0;
            this._isPaused = false;
            
            // Thông báo tier đã thay đổi
            this.OnTierChanged?.Invoke(this._currentTier);
        }
        
        /// <summary>
        /// Giải phóng tài nguyên của bộ đếm
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (this._disposed)
            {
                return;
            }

            if (disposing)
            {
                this.OnUpdate = null;
                this.OnComplete = null;
                this.OnTierChanged = null;
            }

            this._disposed = true;
        }

        /// <summary>
        /// Giải phóng tài nguyên và hủy bộ đếm
        /// </summary>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
