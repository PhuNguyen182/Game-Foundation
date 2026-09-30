using DracoRuan.PrebuildServices.UISystem.Logic.DracoRuan.PrebuildServices.UISystem.Logic.Badge;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class InMemoryBadgeServiceTests
    {
        [Test]
        public void SetCount_ThenGetCount_ReturnsExactValue()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Shop", 3);

            Assert.AreEqual(3, service.GetCount("Shop"));
        }

        [Test]
        public void GetCount_OnParent_SumsDescendantCounts()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Shop/Weapons", 2);
            service.SetCount("Shop/Armor", 5);

            Assert.AreEqual(7, service.GetCount("Shop"));
        }

        [Test]
        public void GetCount_IncludesOwnCountPlusDescendants()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Shop", 1);
            service.SetCount("Shop/Weapons", 2);

            Assert.AreEqual(3, service.GetCount("Shop"));
        }

        [Test]
        public void GetCount_UnrelatedKey_DoesNotContribute()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Mail", 10);

            Assert.AreEqual(0, service.GetCount("Shop"));
        }

        [Test]
        public void HasAny_ZeroCount_ReturnsFalse()
        {
            var service = new InMemoryBadgeService();

            Assert.IsFalse(service.HasAny("Shop"));
        }

        [Test]
        public void HasAny_PositiveDescendantCount_ReturnsTrue()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Shop/Weapons", 1);

            Assert.IsTrue(service.HasAny("Shop"));
        }

        [Test]
        public void SetCount_Negative_ClampsToZero()
        {
            var service = new InMemoryBadgeService();

            service.SetCount("Shop", -5);

            Assert.AreEqual(0, service.GetCount("Shop"));
        }

        [Test]
        public void SetCount_ToZero_RemovesEntryAndStopsContributing()
        {
            var service = new InMemoryBadgeService();
            service.SetCount("Shop/Weapons", 4);

            service.SetCount("Shop/Weapons", 0);

            Assert.AreEqual(0, service.GetCount("Shop"));
        }

        [Test]
        public void Changed_FiresWithTheExactKeyThatWasSet()
        {
            var service = new InMemoryBadgeService();
            BadgeKey? observed = null;
            service.Changed += k => observed = k;

            service.SetCount("Shop/Weapons", 1);

            Assert.AreEqual(new BadgeKey("Shop/Weapons"), observed);
        }

        [Test]
        public void Changed_DoesNotFire_WhenSettingTheSameValueAgain()
        {
            var service = new InMemoryBadgeService();
            service.SetCount("Shop", 3);
            int fireCount = 0;
            service.Changed += _ => fireCount++;

            service.SetCount("Shop", 3);

            Assert.AreEqual(0, fireCount);
        }
    }
}
