using DracoRuan.PrebuildServices.UISystem.DracoRuan.PrebuildServices.UISystem.Tutorial;
using TMPro;
using UnityEngine;

namespace DracoRuan.PrebuildServices.UISystem.Samples.TutorialDemo
{
    /// <summary>
    /// Concrete data-driven UITutorialBase for this sample: the only thing a data-driven
    /// tutorial needs to add over the base class is where UITutorialStep.message goes on
    /// screen (OnStepMessage's whole reason for existing - the base class has no opinion on
    /// dialogue layout). Steps themselves are authored on TutorialDemoDefinition.asset, not
    /// hand-written here - see Samples/README.md.
    /// </summary>
    [RequireComponent(typeof(UITutorialMaskController))]
    public sealed class TutorialDemoRunner : UITutorialBase
    {
        [SerializeField] private TMP_Text dialogueText;

        protected override void OnStepMessage(string message) => this.dialogueText.SetText(message);
    }
}
