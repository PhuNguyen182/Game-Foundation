using System;
using System.IO;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Models;
using MemoryPack;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.CompleteTimer.Core
{
    public class TimerDataController
    {
        private const string TimerSaveDataFileName = "TimerSaveData";
        private const string LocalDataPrefix = "GameData";
        private const string FileExtension = ".tim";
        
        private readonly TimeValidator _timeValidator;
        private readonly string _filePath = Application.persistentDataPath;
        
        private TimerSaveData _timerSaveData = new();

        public TimerSaveData TimerSaveData => this._timerSaveData;

        public TimerDataController(TimeValidator timeValidator)
        {
            this._timeValidator = timeValidator;
            this.LoadTimerData();
        }

        #region Data Access

        public TimerUpdateState AddOrUpdateTimerCounterUnitData(TimerCounterUnit timer, bool saveDataManually)
        {
            TimerUpdateState state = !this._timerSaveData.TimerSaveDataUnits.ContainsKey(timer.TimerId)
                ? this.AddTimerCounterUnitData(timer)
                : this.UpdateTimerCounterUnitData(timer);

            if (saveDataManually)
                this.SaveTimerData();
            
            return state;
        }

        private TimerUpdateState AddTimerCounterUnitData(TimerCounterUnit timer)
        {
            TimerSaveUnitModel timerUnitModel = new TimerSaveUnitModel
            {
                TimerId = timer.TimerId,
                StartUnixTime = timer.TimerModel.StartUnixTime,
                TierCount = timer.TimerModel.TierCount,
                TicksByTier = timer.TimerModel.TicksByTier,
            };
            
            this._timerSaveData.TimerSaveDataUnits.Add(timer.TimerId, timerUnitModel);
            return TimerUpdateState.Add;
        }

        private TimerUpdateState UpdateTimerCounterUnitData(TimerCounterUnit timer)
        {
            TimerSaveUnitModel timerUnitModel = this._timerSaveData.TimerSaveDataUnits[timer.TimerId];
            timerUnitModel.StartUnixTime = timer.TimerModel.StartUnixTime;
            timerUnitModel.TierCount = timer.TimerModel.TierCount;
            timerUnitModel.TicksByTier = timer.TimerModel.TicksByTier;
            this._timerSaveData.TimerSaveDataUnits[timer.TimerId] = timerUnitModel;
            return TimerUpdateState.Update;
        }

        public TimerUpdateState RemoveTimerCounterUnitData(string timerId)
        {
            this._timerSaveData.TimerSaveDataUnits.Remove(timerId);
            this.SaveTimerData();
            return TimerUpdateState.Remove;
        }

        public TimerCounterUnit GetTimerCounterUnitFromData(string timerId)
        {
            if (!this._timerSaveData.TimerSaveDataUnits.TryGetValue(timerId, out TimerSaveUnitModel timerUnitModel))
            {
                Debug.LogError($"No timer save data found! TimerId: {timerId}");
                return null;
            }

            TimerModel timerModel = new TimerModel(timerId, timerUnitModel.StartUnixTime, timerUnitModel.TicksByTier);
            TimerCounterUnit timerCounterUnit = new TimerCounterUnit(timerModel, this._timeValidator);
            return timerCounterUnit;
        }

        #endregion

        #region Timer Persistant Data

        public void LoadTimerData()
        {
            try
            {
                string dataPath = this.GetDataPath(TimerSaveDataFileName);
                if (!File.Exists(dataPath))
                {
                    Debug.LogError($"Data file {dataPath} not found! Use created default one.");
                    return;
                }

                using FileStream fileStream = new(dataPath, FileMode.Open, FileAccess.Read, FileShare.None,
                    bufferSize: 4096, useAsync: false);
                byte[] serializedData = new byte[fileStream.Length];
                int readBytes = fileStream.Read(serializedData, 0, serializedData.Length);
                this._timerSaveData = MemoryPackSerializer.Deserialize<TimerSaveData>(serializedData);
                Debug.Log($"Load timer data successfully! Read bytes: {readBytes}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        public void SaveTimerData()
        {
            try
            {
                string dataPath = this.GetDataPath(TimerSaveDataFileName);
                string directoryPath = this.GetDirectoryPath();

                if (!Directory.Exists(directoryPath))
                    Directory.CreateDirectory(directoryPath);

                using FileStream fileStream = new(dataPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: false);
                byte[] serializedData = MemoryPackSerializer.Serialize(this._timerSaveData);
                fileStream.Write(serializedData);
                Debug.Log($"Save timer data successfully! Write bytes: {serializedData.Length}");
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        private string GetDataPath(string name)
        {
            string dataPath = Path.Combine(this._filePath, LocalDataPrefix, $"{name}{FileExtension}");
            return dataPath;
        }
        
        private string GetDirectoryPath() => Path.Combine(this._filePath, LocalDataPrefix);

        #endregion
    }
}
