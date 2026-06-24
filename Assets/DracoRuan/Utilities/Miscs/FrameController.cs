using UnityEngine;

namespace DracoRuan.Utilities.Miscs
{
    public class FrameController : MonoBehaviour
    {
        [SerializeField] public bool useDesiredValue = true;
        [SerializeField] public int desiredFramerate = 60;

        private void Awake() => SetupFramerate();
        
        private void SetupFramerate()
        {
#if UNITY_EDITOR
            return;
#elif UNITY_STANDALONE
            Application.targetFrameRate = -1;
#elif UNITY_ANDROID || UNITY_IOS
            Application.targetFrameRate = useDesiredValue ? desiredFramerate : (int)Screen.currentResolution.refreshRateRatio.value;
#endif
        }
    }
}
