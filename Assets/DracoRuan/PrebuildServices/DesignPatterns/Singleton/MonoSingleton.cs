using UnityEngine;

namespace DracoRuan.CoreSystems.DesignPatterns.Singleton
{
    public abstract class MonoSingleton<TComponent> : MonoBehaviour where TComponent : Component
    {
        private static TComponent _instance;
        
        public static TComponent Instance
        {
            get
            {
                if (_instance) 
                    return _instance;
                
                _instance = FindFirstObjectByType<TComponent>();

                if (_instance) 
                    return _instance;
                    
                GameObject newInstanceObject = new GameObject
                {
                    hideFlags = HideFlags.None
                };
                        
                _instance = newInstanceObject.AddComponent<TComponent>();
                return _instance;
            }
        }
        
        protected virtual void Awake()
        {
            if (!_instance)
            {
                _instance = this as TComponent;
                DontDestroyOnLoad(this.gameObject);
                this.OnAwake();
            }

            else
            {
                if (this != _instance)
                    Destroy(this.gameObject);
            }
        }

        protected virtual void OnAwake()
        {

        }
    }
}
