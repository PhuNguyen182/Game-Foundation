using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using DracoRuan.PrebuildServices.UISystem.Tutorial;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    /// <summary>
    /// PlayMode tests for UITutorialBase's data-driven mode (REWRITE_PLAN.md mục 5 bước 10,
    /// user instruction: "2 chế độ: dựng sẵn kịch bản và truyền data vào"). Must be PlayMode
    /// (not EditMode + manual eval polling): UniTask.Delay real-time waits and the
    /// UniTaskCompletionSource driving ClickAnywhere/External only resolve across real ticked
    /// frames - confirmed the hard way in this session, where polling a live Play session
    /// through the Unity CLI's `eval` command showed Time.frameCount frozen at 1 for many real
    /// seconds (the Editor's player loop only advances on repaint, which nothing in a
    /// CLI-driven, unfocused-window session triggers) while [UnityTest] here ticks normally
    /// (proven by UIServiceTests' own 9 passing tests in the same suite).
    /// </summary>
    public class UITutorialBaseTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in this._created)
                if (obj != null)
                    Object.Destroy(obj);

            this._created.Clear();
        }

        private (GameObject go, UITutorialBase tutorial, UITutorialMaskController mask) NewTutorial()
        {
            var go = new GameObject("Tutorial", typeof(RectTransform), typeof(Image));
            this._created.Add(go);

            var mask = go.AddComponent<UITutorialMaskController>();
            var tutorial = go.AddComponent<UITutorialBase>();

            return (go, tutorial, mask);
        }

        private static RectTransform NewTarget(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        [UnityTest]
        public IEnumerator DataDriven_TimerStep_AdvancesOnItsOwnAfterDelay()
        {
            (GameObject go, UITutorialBase tutorial, _) = this.NewTutorial();
            NewTarget(go.transform, "Target");

            tutorial.Steps.Add(new UITutorialStep
            {
                message = "step",
                targetPath = "Target",
                advanceMode = UITutorialAdvanceMode.Timer,
                autoAdvanceSeconds = 0.05f,
            });

            bool completed = false;
            UniTask runTask = tutorial.RunAsync();
            runTask.GetAwaiter().OnCompleted(() => completed = true);

            yield return new WaitForSeconds(0.3f);

            Assert.IsTrue(completed, "A Timer-mode step should advance on its own without any external call.");
        }

        [UnityTest]
        public IEnumerator DataDriven_ExternalMode_WaitsUntilAdvanceCurrentStepIsCalled()
        {
            (GameObject go, UITutorialBase tutorial, _) = this.NewTutorial();
            NewTarget(go.transform, "Target");

            tutorial.Steps.Add(new UITutorialStep
            {
                message = "step",
                targetPath = "Target",
                advanceMode = UITutorialAdvanceMode.External,
            });

            bool completed = false;
            UniTask runTask = tutorial.RunAsync();
            runTask.GetAwaiter().OnCompleted(() => completed = true);

            yield return new WaitForSeconds(0.2f);
            Assert.IsFalse(completed, "External mode must not advance on its own.");

            tutorial.AdvanceCurrentStep();
            yield return null;

            Assert.IsTrue(completed, "AdvanceCurrentStep() should unblock a step waiting in External mode.");
        }

        [UnityTest]
        public IEnumerator DataDriven_TwoSteps_SecondStepMessageFiresOnlyAfterFirstAdvances()
        {
            (GameObject go, UITutorialBase tutorial, _) = this.NewTutorial();
            NewTarget(go.transform, "First");
            NewTarget(go.transform, "Second");

            var messages = new List<string>();
            var recorder = go.AddComponent<MessageRecordingTutorial>();
            recorder.Recorded = messages;
            recorder.Steps.Add(new UITutorialStep { message = "first", targetPath = "First", advanceMode = UITutorialAdvanceMode.External });
            recorder.Steps.Add(new UITutorialStep { message = "second", targetPath = "Second", advanceMode = UITutorialAdvanceMode.External });

            _ = recorder.RunAsync();
            yield return null;

            Assert.AreEqual(new[] { "first" }, messages.ToArray());

            recorder.AdvanceCurrentStep();
            yield return null;

            Assert.AreEqual(new[] { "first", "second" }, messages.ToArray());
        }

        /// <summary>Test-only subclass exposing OnStepMessage's calls, since the base has no
        /// public way to observe them (by design - a real tutorial sets a dialogue box's text
        /// instead of exposing state for a test to read).</summary>
        private sealed class MessageRecordingTutorial : UITutorialBase
        {
            public List<string> Recorded;

            protected override void OnStepMessage(string message) => this.Recorded.Add(message);
        }
    }
}
