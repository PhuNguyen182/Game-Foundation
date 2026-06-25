using System.Collections.Generic;
using Firebase.Analytics;
using UnityEngine.Pool;

namespace DracoRuan.Tracking.Tracker
{
    public class FirebaseTracker : IEventTracker
    {
        private const string TrackerTag = "FirebaseTracker";
        
        public void TrackEvent(string eventName, string parameterName, string parameterValue)
        {
            Parameter parameter = new Parameter(parameterName, parameterValue);
            FirebaseAnalytics.LogEvent(eventName, parameter);
        }

        public void TrackEvent(string eventName, string parameterName, double parameterValue)
        {
            Parameter parameter = new Parameter(parameterName, parameterValue);
            FirebaseAnalytics.LogEvent(eventName, parameter);
        }

        public void TrackEvent(string eventName, string parameterName, long parameterValue)
        {
            Parameter parameter = new Parameter(parameterName, parameterValue);
            FirebaseAnalytics.LogEvent(eventName, parameter);
        }

        public void TrackEvent(string eventName, string parameterName, int parameterValue)
        {
            Parameter parameter = new Parameter(parameterName, parameterValue);
            FirebaseAnalytics.LogEvent(eventName, parameter);
        }

        public void TrackEvent(string eventName, Dictionary<string, object> parameters)
        {
            using (ListPool<Parameter>.Get(out List<Parameter> parameterList))
            {
                foreach (KeyValuePair<string, object> parameter in parameters)
                {
                    Parameter param = this.GetParameterFromKeyValuePair(parameter.Key, parameter.Value);
                    parameterList.Add(param);
                }

                Parameter[] logParams = parameterList.ToArray();
                FirebaseAnalytics.LogEvent(eventName, logParams);
            }
        }
        
        public void SetUserProperty(string propertyName, string propertyValue)
        {
            FirebaseAnalytics.SetUserProperty(propertyName, propertyValue);
            Debug.Log($"[{TrackerTag}] Set user property: {propertyName}, value: {propertyValue}");
        }

        private Parameter GetParameterFromKeyValuePair(string paramKey, object paramValue)
        {
            return paramValue switch
            {
                int intParamValue => new Parameter(paramKey, intParamValue),
                float floatParamValue => new Parameter(paramKey, floatParamValue),
                long longParamValue => new Parameter(paramKey, longParamValue),
                double doubleParamValue => new Parameter(paramKey, doubleParamValue),
                string stringParamValue => new Parameter(paramKey, stringParamValue),
                _ => null
            };
        }
    }
}