using DracoRuan.PrebuildServices.UISystem.Motion.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public class UIMotionValueResolverTests
    {
        private static readonly Float4 Rest = new Float4(10f, 20f, 0f, 0f);
        private static readonly Float4 Start = new Float4(1f, 2f, 0f, 0f);
        private static readonly Float4 ParentSize = new Float4(100f, 50f, 0f, 0f);

        [Test]
        public void Resolve_Absolute_ReturnsRawValueUnchanged()
        {
            var raw = new Float4(5f, 6f, 7f, 8f);
            Float4 result = UIMotionValueResolver.Resolve(UIMotionValueMode.Absolute, raw, Rest, Start, ParentSize);
            Assert.AreEqual(raw, result);
        }

        [Test]
        public void Resolve_RelativeToRest_AddsRawValueToRest()
        {
            var raw = new Float4(1f, -1f, 0f, 0f);
            Float4 result = UIMotionValueResolver.Resolve(UIMotionValueMode.RelativeToRest, raw, Rest, Start, ParentSize);
            Assert.AreEqual(new Float4(11f, 19f, 0f, 0f), result);
        }

        [Test]
        public void Resolve_RelativeToStart_AddsRawValueToStart()
        {
            var raw = new Float4(100f, 0f, 0f, 0f);
            Float4 result = UIMotionValueResolver.Resolve(UIMotionValueMode.RelativeToStart, raw, Rest, Start, ParentSize);
            Assert.AreEqual(new Float4(101f, 2f, 0f, 0f), result);
        }

        [Test]
        public void Resolve_FractionOfParent_MultipliesByParentSize()
        {
            var raw = new Float4(0.5f, -1f, 0f, 0f);
            Float4 result = UIMotionValueResolver.Resolve(UIMotionValueMode.FractionOfParent, raw, Rest, Start, ParentSize);
            Assert.AreEqual(new Float4(50f, -50f, 0f, 0f), result);
        }

        [Test]
        public void ResolveRest_ReturnsRestValueRegardlessOfRawInput()
        {
            Float4 result = UIMotionValueResolver.ResolveRest(Rest);
            Assert.AreEqual(Rest, result);
        }
    }
}
