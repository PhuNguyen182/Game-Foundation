using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeSchedulerComponent;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Manager
{
    /// <summary>
    /// Interface cho quản lý các bộ đếm thời gian theo thời gian thực
    /// </summary>
    public interface ICountdownTimerManager : IDisposable
    {
        /// <summary>
        /// Tạo hoặc lấy bộ đếm thời gian theo key
        /// </summary>
        /// <param name="key">Khóa định danh</param>
        /// <param name="durationSeconds">Thời gian đếm ngược (seconds)</param>
        /// <returns>Bộ đếm thời gian</returns>
        public ICountdownTimer GetOrCreateTimer(string key, float durationSeconds);
        
        /// <summary>
        /// Bắt đầu đếm ngược cho timer đã load
        /// </summary>
        /// <param name="key">Khóa định danh</param>
        /// <returns>Bộ đếm thời gian</returns>
        public ICountdownTimer StartTimer(string key);
        
        /// <summary>
        /// Lấy bộ đếm thời gian theo key
        /// </summary>
        /// <param name="key">Khóa định danh</param>
        /// <returns>Bộ đếm thời gian hoặc null nếu không tồn tại</returns>
        public ICountdownTimer GetTimer(string key);
        
        /// <summary>
        /// Kiểm tra bộ đếm có tồn tại không
        /// </summary>
        /// <param name="key">Khóa định danh</param>
        /// <returns>True nếu tồn tại</returns>
        public bool HasTimer(string key);
        
        /// <summary>
        /// Xóa bộ đếm thời gian
        /// </summary>
        /// <param name="key">Khóa định danh</param>
        /// <returns>True nếu xóa thành công</returns>
        public bool RemoveTimer(string key);
        
        /// <summary>
        /// Lấy tất cả các bộ đếm đang hoạt động
        /// </summary>
        /// <returns>Dictionary các bộ đếm</returns>
        public IReadOnlyDictionary<string, ICountdownTimer> GetAllTimers();
        
        /// <summary>
        /// Lấy số lượng bộ đếm đang hoạt động
        /// </summary>
        /// <returns>Số lượng bộ đếm</returns>
        public int ActiveTimerCount { get; }
        
        /// <summary>
        /// Cập nhật tất cả các bộ đếm (được gọi trong Update loop)
        /// </summary>
        /// <param name="deltaTime">Thời gian delta</param>
        public void Tick(float deltaTime);
        
        /// <summary>
        /// Lưu tất cả các bộ đếm
        /// </summary>
        public void SaveAllTimers();
        
        /// <summary>
        /// Tải tất cả các bộ đếm từ dữ liệu đã lưu (ở trạng thái paused)
        /// </summary>
        public UniTask LoadAllTimers();
        
        /// <summary>
        /// Xóa tất cả các bộ đếm
        /// </summary>
        public void ClearAllTimers();
        
        /// <summary>
        /// Sự kiện khi bộ đếm hoàn thành
        /// </summary>
        public event Action<string> OnTimerCompleted;
        
        /// <summary>
        /// Sự kiện khi bộ đếm được tạo
        /// </summary>
        public event Action<string, float> OnTimerCreated;
        
        /// <summary>
        /// Sự kiện khi bộ đếm bị xóa
        /// </summary>
        public event Action<string> OnTimerRemoved;
    }
}
