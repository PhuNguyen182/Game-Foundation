using System.Collections.Generic;

namespace DracoRuan.Tracking.Tracker
{
    public interface IEventTracker
    {
        public void TrackEvent(string eventName, string parameterName, string parameterValue);
        public void TrackEvent(string eventName, string parameterName, double parameterValue);
        public void TrackEvent(string eventName, string parameterName, long parameterValue);
        public void TrackEvent(string eventName, string parameterName, int parameterValue);
        public void TrackEvent(string eventName, Dictionary<string, object> parameters);
    }
}
