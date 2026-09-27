using DracoRuan.PrebuildServices.UISystem.Logic.Badge;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    public class BadgeKeyTests
    {
        [Test]
        public void IsAncestorOrSelfOf_SameKey_ReturnsTrue()
        {
            BadgeKey key = "Shop/Weapons";

            Assert.IsTrue(key.IsAncestorOrSelfOf(key));
        }

        [Test]
        public void IsAncestorOrSelfOf_Descendant_ReturnsTrue()
        {
            BadgeKey parent = "Shop";
            BadgeKey child = "Shop/Weapons/Sword";

            Assert.IsTrue(parent.IsAncestorOrSelfOf(child));
        }

        [Test]
        public void IsAncestorOrSelfOf_UnrelatedSibling_ReturnsFalse()
        {
            BadgeKey a = "Shop/Weapons";
            BadgeKey b = "Shop/Armor";

            Assert.IsFalse(a.IsAncestorOrSelfOf(b));
        }

        [Test]
        public void IsAncestorOrSelfOf_PrefixButNotSegmentBoundary_ReturnsFalse()
        {
            // "Shop/Weapon" must not be considered an ancestor of "Shop/Weapons" - a naive
            // string.StartsWith check would wrongly match here.
            BadgeKey a = "Shop/Weapon";
            BadgeKey b = "Shop/Weapons";

            Assert.IsFalse(a.IsAncestorOrSelfOf(b));
        }

        [Test]
        public void RootKey_IsAncestorOfEverything()
        {
            BadgeKey root = new BadgeKey(null);
            BadgeKey anything = "Shop/Weapons/Sword";

            Assert.IsTrue(root.IsAncestorOrSelfOf(anything));
        }

        [Test]
        public void Parent_OfNestedKey_ReturnsImmediateParent()
        {
            BadgeKey key = "Shop/Weapons/Sword";

            Assert.AreEqual(new BadgeKey("Shop/Weapons"), key.Parent());
        }

        [Test]
        public void Parent_OfTopLevelKey_ReturnsRoot()
        {
            BadgeKey key = "Shop";

            Assert.IsTrue(key.Parent().IsRoot);
        }

        [Test]
        public void Combine_BuildsExpectedPath()
        {
            BadgeKey parent = "Shop";

            BadgeKey combined = BadgeKey.Combine(parent, "Weapons");

            Assert.AreEqual(new BadgeKey("Shop/Weapons"), combined);
        }

        [Test]
        public void Equality_SameSegments_AreEqual()
        {
            BadgeKey a = "Shop/Weapons";
            BadgeKey b = new BadgeKey("Shop/Weapons");

            Assert.AreEqual(a, b);
            Assert.IsTrue(a.Equals(b));
        }
    }
}
