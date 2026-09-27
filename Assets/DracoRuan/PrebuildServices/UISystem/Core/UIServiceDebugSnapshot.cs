using System.Collections.Generic;

namespace DracoRuan.PrebuildServices.UISystem.Core
{
    /// <summary>Read-only introspection data for UISystem.Editor's UI Debugger window
    /// (REWRITE_PLAN.md mục 5 bước 8) - not part of IUINavigator, since ordinary game code has
    /// no business reading it; UIService.CaptureDebugSnapshot is the only way to get one.</summary>
    public readonly struct UIServiceDebugSnapshot
    {
        public readonly IReadOnlyList<string> ScreenStack;
        public readonly IReadOnlyDictionary<string, IReadOnlyList<string>> PopupsByLayer;
        public readonly IReadOnlyDictionary<string, int> QueueCountByLayer;
        public readonly IReadOnlyDictionary<string, bool> QueuePausedByLayer;
        public readonly int InputLockCount;
        public readonly IReadOnlyList<string> OpenViewModelTypeNames;

        public UIServiceDebugSnapshot(
            IReadOnlyList<string> screenStack,
            IReadOnlyDictionary<string, IReadOnlyList<string>> popupsByLayer,
            IReadOnlyDictionary<string, int> queueCountByLayer,
            IReadOnlyDictionary<string, bool> queuePausedByLayer,
            int inputLockCount,
            IReadOnlyList<string> openViewModelTypeNames)
        {
            this.ScreenStack = screenStack;
            this.PopupsByLayer = popupsByLayer;
            this.QueueCountByLayer = queueCountByLayer;
            this.QueuePausedByLayer = queuePausedByLayer;
            this.InputLockCount = inputLockCount;
            this.OpenViewModelTypeNames = openViewModelTypeNames;
        }
    }
}
