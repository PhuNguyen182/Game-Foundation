using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.Foundation.DataFlow.SaveSystem;
using DracoRuan.Foundation.DataFlow.SaveSystem.CustomDataSaverService;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence
{
    /// <summary>
    /// Implementation của ITimerPersistence sử dụng FileDataSaveService
    /// Lưu timer data vào file JSON trong Application.persistentDataPath
    /// </summary>
    public class FileTimerPersistence : ITimerPersistence
    {
        private const string SaveFileName = "TimerData";
        
        private readonly TimerDataSerializer _serializer = new();
        private readonly IDataSaveService _dataSaveService = new FileDataSaveService();

        public bool SaveTimers(List<CountdownTimerData> timerDataList)
        {
            if (timerDataList.Count == 0)
            {
                return this.ClearTimers();
            }
            
            try
            {
                this._dataSaveService.SaveData(SaveFileName, timerDataList);
                Debug.Log($"[FileTimerPersistence] Saved {timerDataList.Count} timers to file");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileTimerPersistence] Failed to save timer data: {ex.Message}");
                return false;
            }
        }

        public async UniTask<List<CountdownTimerData>> LoadTimers()
        {
            try
            {
                var loadTask = this._dataSaveService.LoadData(SaveFileName);
                var serializedTimerData = await loadTask;
                
                if (!string.IsNullOrEmpty(serializedTimerData))
                {
                    Debug.Log($"[FileTimerPersistence] Loaded {serializedTimerData} timers from file");
                }
                
                List<CountdownTimerData> timerDataList = this._serializer.Deserialize(serializedTimerData);
                return timerDataList ?? new List<CountdownTimerData>();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileTimerPersistence] Failed to load timer data: {ex.Message}");
                return new List<CountdownTimerData>();
            }
        }

        public bool ClearTimers()
        {
            try
            {
                this._dataSaveService.DeleteData(SaveFileName);
                Debug.Log("[FileTimerPersistence] Cleared all timer data from file");
                return true;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileTimerPersistence] Failed to clear timer data: {ex.Message}");
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

