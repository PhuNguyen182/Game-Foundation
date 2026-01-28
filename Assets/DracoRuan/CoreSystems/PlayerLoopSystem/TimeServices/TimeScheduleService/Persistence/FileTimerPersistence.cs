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
        
        private readonly IDataSaveService<List<CountdownTimerData>> _dataSaveService;

        public FileTimerPersistence()
        {
            var serializer = new TimerDataSerializer();
            this._dataSaveService = new FileDataSaveService<List<CountdownTimerData>>(serializer);
        }

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
                var timerDataList = await loadTask;
                
                if (timerDataList is { Count: > 0 })
                {
                    Debug.Log($"[FileTimerPersistence] Loaded {timerDataList.Count} timers from file");
                }
                
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

