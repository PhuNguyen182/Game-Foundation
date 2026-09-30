using System;

namespace DracoRuan.PrebuildServices.UISystem.Motion.DracoRuan.PrebuildServices.UISystem.Motion
{
    /// <summary>
    /// Implemented by a parent (e.g. UIView) that wants child UIMotion components to
    /// play on OnParentShow. UIView hides via Canvas.enabled, which does not re-run
    /// OnEnable, so children can't rely on their own OnEnable to detect a re-show.
    /// Lives in the Motion asmdef so Motion never has to reference UISystem runtime.
    /// </summary>
    public interface IUIMotionTriggerSource
    {
        event Action ParentShown;
    }
}
