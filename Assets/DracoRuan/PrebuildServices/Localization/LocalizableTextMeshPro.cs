using TMPro;
using UnityEngine;
using UnityEngine.Localization;

namespace DracoRuan.PrebuildServices.Localization
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(TMP_Text))]
    public class LocalizableTextMeshPro : MonoBehaviour
    {
        [SerializeField] private TMP_Text localizedText;
        [SerializeField] private LocalizedString localizedString;

        private void OnEnable()
        {
            this.localizedString.StringChanged += this.UpdateText;
            this.localizedText.text = this.localizedString.GetLocalizedString();
        }

        private void OnDisable()
        {
            this.localizedString.StringChanged -= this.UpdateText;
        }

        private void UpdateText(string text)
        {
            this.localizedText.text = text;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.localizedText)
                this.localizedText = GetComponent<TMP_Text>();
        }
#endif
    }
}