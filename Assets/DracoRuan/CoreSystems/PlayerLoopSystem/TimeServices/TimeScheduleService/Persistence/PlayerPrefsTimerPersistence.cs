using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence
{
    /// <summary>
    /// Implementation của ITimerPersistence sử dụng PlayerPrefDataSaveService
    /// Lưu timer data vào PlayerPrefs dưới dạng JSON
    /// </summary>
    public class PlayerPrefsTimerPersistence : ITimerPersistence
    {
        private const string SaveKey = "CountdownTimers";
        
        private readonly IDataSaveService<List<CountdownTimerData>> _dataSaveService;

        public PlayerPrefsTimerPersistence()
        {
            var serializer = new TimerDataSerializer();
            this._dataSaveService = new PlayerPrefDataSaveService<List<CountdownTimerData>>(serializer);
        }

        public bool SaveTimers(List<CountdownTimerData> timerDataList)
        {
            if (timerDataList.Count == 0)
            {
                return this.ClearTimers();
            }
            
            try
            {
                this._dataSaveService.SaveData(SaveKey, timerDataList);
                Debug.Log($"[PlayerPrefsTimerPersistence] Saved {timerDataList.Count} timers to PlayerPrefs");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsTimerPersistence] Failed to save timer data: {ex.Message}");
                return false;
            }
        }

        public async UniTask<List<CountdownTimerData>> LoadTimers()
        {
            try
            {
                var loadTask = this._dataSaveService.LoadData(SaveKey);
                var timerDataList = await loadTask;
                
                if (timerDataList is { Count: > 0 })
                {
                    Debug.Log($"[PlayerPrefsTimerPersistence] Loaded {timerDataList.Count} timers from PlayerPrefs");
                }
                
                return timerDataList ?? new List<CountdownTimerData>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsTimerPersistence] Failed to load timer data: {ex.Message}");
                return new List<CountdownTimerData>();
            }
        }

        public bool ClearTimers()
        {
            try
            {
                this._dataSaveService.DeleteData(SaveKey);
                Debug.Log("[PlayerPrefsTimerPersistence] Cleared all timer data from PlayerPrefs");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PlayerPrefsTimerPersistence] Failed to clear timer data: {ex.Message}");
                return false;
            }
        }

        public async UniTask<bool> HasSavedTimers()
        {
            try
            {
                var timerDataList = await this.LoadTimers();
                return timerDataList is { Count: > 0 };
            }
            catch
            {
                return false;
            }
        }
    }
}

