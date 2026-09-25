using DracoRuan.PrebuildServices.UISystem.Logic;
using NUnit.Framework;

namespace DracoRuan.PrebuildServices.UISystem.Tests
{
    [TestFixture]
    public sealed class BackRouterTests
    {
        private BackRouter _router;

        [SetUp]
        public void SetUp() => this._router = new BackRouter();

        [Test]
        public void TryHandleBack_WhileLocked_IsSwallowed_NoLayerInvoked()
        {
            bool systemInvoked = false;
            this._router.RegisterLayer(() =>
            {
                systemInvoked = true;
                return UIBackResult.PassThrough;
            });

            bool handled = this._router.TryHandleBack(isLocked: true);

            Assert.That(handled, Is.False);
            Assert.That(systemInvoked, Is.False);
        }

        [Test]
        public void TryHandleBack_TopLayerConsumes_LowerLayersNotInvoked()
        {
            bool screenInvoked = false;
            this._router.RegisterLayer(() => UIBackResult.Consume); // System
            this._router.RegisterLayer(() => { screenInvoked = true; return UIBackResult.PassThrough; }); // Screen

            bool handled = this._router.TryHandleBack(isLocked: false);

            Assert.That(handled, Is.True);
            Assert.That(screenInvoked, Is.False);
        }

        [Test]
        public void TryHandleBack_TopLayerPassesThrough_NextLayerIsInvoked()
        {
            bool screenInvoked = false;
            this._router.RegisterLayer(() => UIBackResult.PassThrough); // System (nothing open)
            this._router.RegisterLayer(() => { screenInvoked = true; return UIBackResult.Close; }); // Screen closes a popup

            bool handled = this._router.TryHandleBack(isLocked: false);

            Assert.That(handled, Is.True);
            Assert.That(screenInvoked, Is.True);
        }

        [Test]
        public void TryHandleBack_AllLayersPassThrough_FiresBackAtRootAndReturnsFalse()
        {
            bool backAtRootFired = false;
            this._router.BackAtRoot += () => backAtRootFired = true;
            this._router.RegisterLayer(() => UIBackResult.PassThrough);
            this._router.RegisterLayer(() => UIBackResult.PassThrough);

            bool handled = this._router.TryHandleBack(isLocked: false);

            Assert.That(handled, Is.False);
            Assert.That(backAtRootFired, Is.True);
        }

        [Test]
        public void TryHandleBack_NoLayersRegistered_FiresBackAtRoot()
        {
            bool backAtRootFired = false;
            this._router.BackAtRoot += () => backAtRootFired = true;

            bool handled = this._router.TryHandleBack(isLocked: false);

            Assert.That(handled, Is.False);
            Assert.That(backAtRootFired, Is.True);
        }
    }
}
