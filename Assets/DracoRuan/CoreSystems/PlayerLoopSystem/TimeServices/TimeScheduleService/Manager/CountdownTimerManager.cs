using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.PlayerLoopSystem.Core.Handlers;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeFactory;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.TimeSchedulerComponent;
using DracoRuan.CoreSystems.PlayerLoopSystem.UpdateServices;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Manager
{
    public class CountdownTimerManager : ICountdownTimerManager, IUpdateHandler
    {
        private const int InitialTimerCapacity = 512;
        
        private readonly CountdownTimerFactory _timerFactory;
        private readonly ITimerPersistence _persistence;
        private readonly Dictionary<string, ICountdownTimer> _timers;
        private readonly List<string> _expiredTimerKeys;
        private bool _disposed;

        public int ActiveTimerCount => this._timers.Count;
        public event Action<string> OnTimerCompleted;
        public event Action<string, float> OnTimerCreated;
        public event Action<string> OnTimerRemoved;

        /// <summary>
        /// Constructor mặc định - sử dụng File persistence (Recommended)
        /// </summary>
        public CountdownTimerManager() : this(TimerPersistenceFactory.CreateFilePersistence())
        {
        }
        
        /// <summary>
        /// Constructor với persistence type - cho phép lựa chọn storage backend
        /// </summary>
        /// <param name="persistenceType">Loại persistence (File hoặc PlayerPrefs)</param>
        public CountdownTimerManager(TimerPersistenceType persistenceType) 
            : this(TimerPersistenceFactory.Create(persistenceType))
        {
        }
        
        /// <summary>
        /// Constructor với custom persistence - cho testing và custom implementations
        /// </summary>
        /// <param name="persistence">Custom persistence implementation</param>
        public CountdownTimerManager(ITimerPersistence persistence)
        {
            this._timerFactory = new CountdownTimerFactory();
            this._persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
            this._timers = new Dictionary<string, ICountdownTimer>(InitialTimerCapacity);
            this._expiredTimerKeys = new List<string>(InitialTimerCapacity);
            UpdateServiceManager.RegisterUpdateHandler(this);
        }

        public ICountdownTimer GetOrCreateTimer(string key, float durationSeconds)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }
            
            if (this._timers.TryGetValue(key, out var existingTimer))
            {
                // Resume if timer exists and is paused
                if (existingTimer.IsPaused)
                {
                    existingTimer.Resume();
                }
                
                return existingTimer;
            }
            
            var newTimer = this._timerFactory.Produce(new TimeSchedulerConfig
            {
                Key = key,
                Duration = durationSeconds
            });
            
            this._timers[key] = newTimer;
            newTimer.OnComplete += () => this.HandleTimerCompleted(key);
            this.OnTimerCreated?.Invoke(key, durationSeconds);
            return newTimer;
        }
        
        public ICountdownTimer StartTimer(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                throw new ArgumentException("Key cannot be null or empty", nameof(key));
            }
            
            if (!this._timers.TryGetValue(key, out var timer))
            {
                throw new InvalidOperationException($"Timer with key '{key}' does not exist");
            }
            
            if (timer.IsPaused)
            {
                timer.Resume();
            }
            
            return timer;
        }

        public ICountdownTimer GetTimer(string key) => this._timers.GetValueOrDefault(key);

        public bool HasTimer(string key) => this._timers.ContainsKey(key);

        public bool RemoveTimer(string key)
        {
            if (!this._timers.TryGetValue(key, out var timer))
                return false;

            timer.Dispose();
            this._timers.Remove(key);
            this.OnTimerRemoved?.Invoke(key);
            return true;
        }

        public IReadOnlyDictionary<string, ICountdownTimer> GetAllTimers() => this._timers;
        
        public void Tick(float deltaTime) => UpdateTimers();

        private void UpdateTimers()
        {
            if (this._disposed)
                return;
            
            foreach (var timer in this._timers.Values)
            {
                timer.UpdateRealTime();
            }
            
            this.ProcessExpiredTimers();
        }

        public void SaveAllTimers()
        {
            var timerDataList = new List<CountdownTimerData>(this._timers.Count);
            
            foreach (var timer in this._timers.Values)
            {
                if (timer.IsActive || timer.IsPaused)
                {
                    timerDataList.Add(timer.GetSaveData());
                }
            }

            this._persistence.SaveTimers(timerDataList);
        }

        public async UniTask LoadAllTimers()
        {
            var timerDataList = await this._persistence.LoadTimers();
            
            if (timerDataList.Count == 0)
            {
                return;
            }
            
            foreach (var data in timerDataList)
            {
                try
                {
                    var timer = this._timerFactory.ProduceFromSaveData(data);
                    
                    // Timer will be loaded in paused state
                    this._timers[data.key] = timer;
                    timer.OnComplete += () => this.HandleTimerCompleted(data.key);
                    Debug.Log($"Loaded timer '{data.key}' in paused state. Call StartTimer('{data.key}') to resume.");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to load timer '{data.key}': {ex.Message}");
                }
            }
        }

        public void ClearAllTimers()
        {
            foreach (var timer in this._timers.Values)
            {
                timer.Dispose();
            }
            
            this._timers.Clear();
        }

        private void ProcessExpiredTimers()
        {
            this._expiredTimerKeys.Clear();
            
            foreach (var kvp in this._timers)
            {
                if (!kvp.Value.IsActive && kvp.Value.IsExpired)
                {
                    this._expiredTimerKeys.Add(kvp.Key);
                }
            }
            
            foreach (var key in this._expiredTimerKeys)
            {
                this.RemoveTimer(key);
            }
        }

        private void HandleTimerCompleted(string key)
        {
            this.OnTimerCompleted?.Invoke(key);
            this.RemoveTimer(key);
        }

        public void Dispose()
        {
            if (this._disposed)
            {
                return;
            }

            this.SaveAllTimers();
            this.ClearAllTimers();
            UpdateServiceManager.DeregisterUpdateHandler(this);
            
            this.OnTimerCompleted = null;
            this.OnTimerCreated = null;
            this.OnTimerRemoved = null;
            
            this._disposed = true;
        }
    }
}
