using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.Foundation.DataFlow.SaveSystem;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence
{
    /// <summary>
    /// Implementation của ITimerPersistence sử dụng PlayerPrefDataSaveService
    /// Lưu timer data vào PlayerPrefs dưới dạng JSON
    /// </summary>
    public class PlayerPrefsTimerPersistence : ITimerPersistence
    {
        private const string SaveKey = "CountdownTimers";
        
        private readonly TimerDataSerializer _serializer = new();
        private readonly IDataSaveService _dataSaveService = new PlayerPrefDataSaveService();

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
                var serializedTimerData = await loadTask;
                
                if (!string.IsNullOrEmpty(serializedTimerData))
                {
                    Debug.Log($"[PlayerPrefsTimerPersistence] Loaded {serializedTimerData} timers from PlayerPrefs");
                }
                
                List<CountdownTimerData> timerDataList = this._serializer.Deserialize(serializedTimerData);
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

