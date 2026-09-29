using System;
using DracoRuan.PrebuildServices.UISystem.Motion;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Tutorial
{
    /// <summary>
    /// One step of a data-driven tutorial (REWRITE_PLAN.md mục 5 bước 10 + user instruction:
    /// "bổ sung thêm 1 kiểu data hợp lệ và tương thích với UIMotion để có thể diễn theo
    /// scripting"). A UITutorialBase in data-driven mode plays a List&lt;UITutorialStep&gt; in
    /// order; a scripted-scenario subclass doesn't use this type at all (see UITutorialBase).
    ///
    /// `targetPath` is a Transform.Find path relative to the tutorial root rather than a
    /// direct scene reference, because a tutorial step commonly needs to point at something
    /// on whatever screen is active when the step runs - a direct reference can't survive
    /// that.
    /// </summary>
    [Serializable]
    public sealed class UITutorialStep
    {
        [TextArea] public string message;

        /// <summary>Transform.Find path (relative to the resolving root) of the RectTransform
        /// to highlight. Empty string means "no highlight for this step" (e.g. a pure dialogue
        /// step) - the mask stays wherever the previous step left it, fully closed/hidden, or at
        /// a resting state, depending on how UITutorialRunner is configured; see its own doc
        /// comment for the exact default.</summary>
        public string targetPath = "";

        public UITutorialAdvanceMode advanceMode = UITutorialAdvanceMode.ClickHighlight;

        /// <summary>Only meaningful when advanceMode == Timer.</summary>
        public float autoAdvanceSeconds = 2f;

        /// <summary>Optional: a custom mask sprite for this step's hole shape (falls back to
        /// UITutorialMaskController's own default sprite when null) - REWRITE_PLAN.md/user
        /// instruction: "tùy chỉnh mask bằng cách sử dụng bất kỳ sprite nào".</summary>
        public Sprite maskSprite;

        /// <summary>Optional: a UIMotion timeline to play for this step's entrance (e.g. the
        /// mask's move-to-target Custom track, plus a dialogue box's own Show timeline riding
        /// alongside it in the same UIMotion - see UITutorialRunner). Null = snap the mask
        /// straight to `targetPath` with no animation.</summary>
        public UIMotion enterMotion;
    }
}