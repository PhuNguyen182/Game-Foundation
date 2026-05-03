using UnityEngine;

namespace DracoRuan.Utilities.SceneUtils
{
    public class SceneActivation
    {
        private bool _allowSceneActive;
        
        public AsyncOperation SceneOperation;

        public bool AllowSceneActive
        {
            get => this._allowSceneActive;
            set
            {
                this._allowSceneActive = value;
                this.SceneOperation.allowSceneActivation = this._allowSceneActive;
            }
        }
    }
}