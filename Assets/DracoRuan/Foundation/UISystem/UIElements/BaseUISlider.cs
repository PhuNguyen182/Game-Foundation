using System;
using DracoRuan.Foundation.UISystem.Views;
using UnityEngine;
using UnityEngine.UI;

namespace DracoRuan.Foundation.UISystem.UIElements
{
    [RequireComponent(typeof(Slider))]
    public abstract class BaseUISlider : BaseUIView
    {
        [SerializeField] private Slider uiSlider;
        
        private event Action<float> OnSliderValueChangedInternal;
        
        public event Action<float> OnSliderValueUpdated
        {
            add => this.OnSliderValueChangedInternal += value;
            remove => this.OnSliderValueChangedInternal -= value;
        }

        protected virtual void Awake()
        {
            this.RegisterSliderEvents();
        }

        private void RegisterSliderEvents()
        {
            if (this.uiSlider)
                this.uiSlider.onValueChanged.AddListener(this.OnSliderValueChanged);
        }

        protected virtual void OnSliderValueChanged(float value)
        {
            this.OnSliderValueChangedInternal?.Invoke(value);
        }

        protected virtual void OnDestroy()
        {
            if (this.uiSlider)
                this.uiSlider.onValueChanged.RemoveListener(this.OnSliderValueChanged);
            this.OnSliderValueChangedInternal = null;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!this.uiSlider)
                this.uiSlider = this.GetComponent<Slider>();
        }
#endif
    }
}
