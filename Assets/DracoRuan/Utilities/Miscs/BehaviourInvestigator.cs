using UnityEngine;

namespace DracoRuan.Utilities.Miscs
{
    /// <summary>
    /// This class is used to investigate the behavior of a game object.
    /// </summary>
    public class BehaviourInvestigator : MonoBehaviour
    {
        private void Awake()
        {
            this.LogBehaviourMessage("Awake");
        }

        private void OnEnable()
        {
            this.LogBehaviourMessage("OnEnable");
        }

        private void Start()
        {
            this.LogBehaviourMessage("Start");
        }

        private void OnDisable()
        {
            this.LogBehaviourMessage("OnDisable");
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            this.LogBehaviourMessage(hasFocus ? "OnApplicationFocus" : "OnApplicationBlur");
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            this.LogBehaviourMessage(pauseStatus ? "OnApplicationPause" : "OnApplicationResume");
        }

        private void OnApplicationQuit()
        {
            this.LogBehaviourMessage("OnApplicationQuit");
        }

        private void OnDestroy()
        {
            this.LogBehaviourMessage("OnDestroy");
        }
        
        private void LogBehaviourMessage(string behaviourName = null)
        {
#if UNITY_6000_0_OR_NEWER
            EntityId hashId = gameObject.GetEntityId();
#else
            int hashId = gameObject.GetInstanceID();
#endif
            Debug.Log($"This game object {gameObject.name} with instance id: {hashId} is now triggered by {behaviourName} behaviour.");
        }
    }
}
