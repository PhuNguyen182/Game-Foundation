using System;
using System.Collections.Generic;
using DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Data;
using DracoRuan.Foundation.DataFlow.Serialization;
using UnityEngine;

namespace DracoRuan.CoreSystems.PlayerLoopSystem.TimeServices.TimeScheduleService.Persistence
{
    /// <summary>
    /// Serializer cho List CountdownTimerData sử dụng JSON
    /// </summary>
    public class TimerDataSerializer : IDataSerializer<List<CountdownTimerData>>
    {
        public string FileExtension => ".json";

        public object Serialize(List<CountdownTimerData> data)
        {
            if (data.Count == 0)
            {
                return string.Empty;
            }

            try
            {
                var wrapper = new TimerDataWrapper { timers = data.ToArray() };
                return JsonUtility.ToJson(wrapper, true);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to serialize timer data: {ex.Message}");
                return string.Empty;
            }
        }

        public List<CountdownTimerData> Deserialize(object serializedData)
        {
            string serializedDataAsString = serializedData as string;
            if (string.IsNullOrEmpty(serializedDataAsString))
            {
                return new List<CountdownTimerData>();
            }

            try
            {
                var wrapper = JsonUtility.FromJson<TimerDataWrapper>(serializedDataAsString);
                
                if (wrapper?.timers == null)
                {
                    return new List<CountdownTimerData>();
                }

                var resultList = new List<CountdownTimerData>(wrapper.timers.Length);
                
                for (int i = 0; i < wrapper.timers.Length; i++)
                {
                    resultList.Add(wrapper.timers[i]);
                }
                
                return resultList;
            }
            catch (Exception ex)
            {
                Debug.LogError($"Failed to deserialize timer data: {ex.Message}");
                return new List<CountdownTimerData>();
            }
        }

        [Serializable]
        private class TimerDataWrapper
        {
            public CountdownTimerData[] timers = null!;
        }
    }
}

