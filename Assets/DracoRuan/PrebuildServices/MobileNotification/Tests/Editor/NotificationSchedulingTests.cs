using DracoRuan.PrebuildServices.MobileNotification.Core;
using DracoRuan.PrebuildServices.MobileNotification.Data;
using NUnit.Framework;
using UnityEngine;

namespace DracoRuan.PrebuildServices.MobileNotification.Tests
{
    public class NotificationSchedulingTests
    {
        private const long Day = 24 * 60 * 60;

        [Test]
        public void TryResolveDelay_WithoutElapsedTime_KeepsTheAuthoredDelay()
        {
            Assert.IsTrue(MobileNotificationService.TryResolveDelay(3600, false, 0, 0, out long delay));
            Assert.AreEqual(3600, delay);
        }

        [Test]
        public void TryResolveDelay_SubtractsTimeSinceTheAnchor()
        {
            Assert.IsTrue(MobileNotificationService.TryResolveDelay(3600, false, 0, 600, out long delay));
            Assert.AreEqual(3000, delay);
        }

        [Test]
        public void TryResolveDelay_FutureAnchor_AddsTheRemainingTime()
        {
            Assert.IsTrue(MobileNotificationService.TryResolveDelay(3600, false, 0, -600, out long delay));
            Assert.AreEqual(4200, delay);
        }

        [Test]
        public void TryResolveDelay_PassedOneTimeNotification_IsSkipped()
        {
            Assert.IsFalse(MobileNotificationService.TryResolveDelay(3600, false, 0, 4000, out _));
        }

        [Test]
        public void TryResolveDelay_PassedRepeatingNotification_MovesToItsNextOccurrence()
        {
            // First fire was 400s ago, so the next one is a full day minus those 400s away.
            Assert.IsTrue(MobileNotificationService.TryResolveDelay(3600, true, Day, 4000, out long delay));
            Assert.AreEqual(Day - 400, delay);
        }

        [Test]
        public void TryResolveDelay_SeveralPeriodsLate_StillLandsOnTheNextOccurrence()
        {
            Assert.IsTrue(MobileNotificationService.TryResolveDelay(0, true, Day, 3 * Day + 100, out long delay));
            Assert.AreEqual(Day - 100, delay);
        }

        [Test]
        public void TryValidate_AcceptsANotificationWithOnlyABody()
        {
            var notification = new NotificationData(string.Empty, "Body", 60);

            Assert.IsTrue(notification.TryValidate(out string error), error);
        }

        [Test]
        public void TryValidate_RejectsEmptyContent()
        {
            var notification = new NotificationData(string.Empty, " ", 60);

            Assert.IsFalse(notification.TryValidate(out _));
        }

        [Test]
        public void TryValidate_RejectsARepeatShorterThanAMinute()
        {
            var notification = new NotificationData("Title", "Body", 60) { repeats = true, repeatInterval = 30 };

            Assert.IsFalse(notification.TryValidate(out _));
        }

        [Test]
        public void Clone_CopiesTheAttachmentListInsteadOfSharingIt()
        {
            var original = new NotificationData("Title", "Body", 60);
            original.attachmentUrls.Add("file:///a.png");

            NotificationData copy = original.Clone();
            copy.attachmentUrls.Add("file:///b.png");

            Assert.AreEqual(1, original.attachmentUrls.Count);
            Assert.AreEqual(2, copy.attachmentUrls.Count);
        }

        [Test]
        public void ScenarioTryValidate_RejectsAutoIds()
        {
            var scenario = ScriptableObject.CreateInstance<NotificationScenario>();
            scenario.notifications.Add(new NotificationData("Title", "Body", 60));

            Assert.IsFalse(scenario.TryValidate(out _));
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void ScenarioTryValidate_RejectsDuplicateIds()
        {
            var scenario = ScriptableObject.CreateInstance<NotificationScenario>();
            scenario.notifications.Add(new NotificationData("A", "Body", 60) { identifier = 7 });
            scenario.notifications.Add(new NotificationData("B", "Body", 120) { identifier = 7 });

            Assert.IsFalse(scenario.TryValidate(out _));
            Object.DestroyImmediate(scenario);
        }

        [Test]
        public void ScenarioTryValidate_AcceptsUniqueFixedIds()
        {
            var scenario = ScriptableObject.CreateInstance<NotificationScenario>();
            scenario.notifications.Add(new NotificationData("A", "Body", 60) { identifier = 7 });
            scenario.notifications.Add(new NotificationData("B", "Body", 120) { identifier = 8 });

            Assert.IsTrue(scenario.TryValidate(out string error), error);
            Object.DestroyImmediate(scenario);
        }
    }
}
