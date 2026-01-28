using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeSchedulerComponent;
using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Manager
{
    public class TimeScheduleManager : IUpdateHandler
    {
        private readonly ICountdownTimerManager _countdownTimerManager;
        
        /// <summary>
        /// Constructor mặc định - sử dụng File persistence (Recommended)
        /// </summary>
        public TimeScheduleManager() : this(TimerPersistenceType.File)
        {
        }
        
        /// <summary>
        /// Constructor với persistence type - cho phép lựa chọn storage backend
        /// </summary>
        /// <param name="persistenceType">Loại persistence (File hoặc PlayerPrefs)</param>
        public TimeScheduleManager(TimerPersistenceType persistenceType)
        {
            this._countdownTimerManager = new CountdownTimerManager(persistenceType);
            this.LoadAllSchedulers();
            UpdateServiceManager.RegisterUpdateHandler(this);
            this.Initialize();
        }
        
        private void Initialize()
        {
            this._countdownTimerManager.OnTimerCompleted += this.HandleCountdownTimerCompleted;
        }

        /// <summary>
        /// Tạo và bắt đầu countdown timer mới
        /// </summary>
        public ICountdownTimer StartCountdownTimer(string key, float durationSeconds)
            => this._countdownTimerManager.GetOrCreateTimer(key, durationSeconds);

        /// <summary>
        /// Bắt đầu countdown timer đã được load (từ save data)
        /// </summary>
        public ICountdownTimer StartLoadedCountdownTimer(string key)
            => this._countdownTimerManager.StartTimer(key);

        public ICountdownTimer GetCountdownTimer(string key) => this._countdownTimerManager.GetTimer(key);

        public bool HasCountdownTimer(string key) => this._countdownTimerManager.HasTimer(key);

        public bool RemoveCountdownTimer(string key) => this._countdownTimerManager.RemoveTimer(key);

        public void Tick(float deltaTime) => this._countdownTimerManager.Tick(deltaTime);

        public void LoadAllSchedulers() => this._countdownTimerManager.LoadAllTimers();

        public void SaveAllSchedulers() => this._countdownTimerManager.SaveAllTimers();

        public void Clear() => this._countdownTimerManager.ClearAllTimers();

        private void HandleCountdownTimerCompleted(string key)
        {
            Debug.Log($"Countdown timer '{key}' completed!");
        }

        public void Dispose()
        {
            UpdateServiceManager.DeregisterUpdateHandler(this);
            
            if (this._countdownTimerManager != null)
            {
                this._countdownTimerManager.OnTimerCompleted -= this.HandleCountdownTimerCompleted;
                this._countdownTimerManager.Dispose();
            }
            
            this.Clear();
        }
    }
}
