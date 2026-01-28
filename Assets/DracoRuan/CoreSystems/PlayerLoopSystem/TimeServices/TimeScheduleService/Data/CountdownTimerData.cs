using System;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data
{
    /// <summary>
    /// Dữ liệu lưu trữ cho bộ đếm thời gian theo thời gian thực với hỗ trợ tier
    /// </summary>
    [Serializable]
    public struct CountdownTimerData
    {
        /// <summary>
        /// Khóa định danh duy nhất của bộ đếm
        /// </summary>
        public string key;
        
        /// <summary>
        /// Thời gian kết thúc dưới dạng Unix timestamp (seconds)
        /// </summary>
        public long endTimeUnix;
        
        /// <summary>
        /// Thời gian khởi tạo dưới dạng Unix timestamp (seconds)
        /// </summary>
        public long startTimeUnix;
        
        /// <summary>
        /// Tổng thời gian đếm ngược ban đầu (seconds)
        /// </summary>
        public float totalDuration;
        
        /// <summary>
        /// Tổng số tier của bộ đếm
        /// </summary>
        public int tierCount;
        
        /// <summary>
        /// Số tier hiện tại còn lại
        /// </summary>
        public int currentTier;

        public CountdownTimerData(
            string key, 
            long endTimeUnix, 
            long startTimeUnix, 
            float totalDuration, 
            int tierCount, 
            int currentTier)
        {
            this.key = key;
            this.endTimeUnix = endTimeUnix;
            this.startTimeUnix = startTimeUnix;
            this.totalDuration = totalDuration;
            this.tierCount = tierCount;
            this.currentTier = currentTier;
        }
    }
}
