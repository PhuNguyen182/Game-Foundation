using System;
using DracoRuan.PrebuildServices.PlayerLoopSystem.Core.Handlers;

namespace DracoRuan.PrebuildServices.PlayerLoopSystem.UpdateServices.Tests
{
    /// <summary>Handler usable by both managers; counts ticks and runs an optional callback inside the tick.</summary>
    public sealed class TestHandler : IUpdateHandler, IFixedUpdateHandler
    {
        public readonly string Name;
        public readonly int Id;

        public int Ticks;
        public float LastDeltaTime;
        public Action<TestHandler> OnTick;

        public TestHandler(string name = "", int id = -1)
        {
            this.Name = name;
            this.Id = id;
        }

        void IUpdateHandler.Tick(float deltaTime)
        {
            this.LastDeltaTime = deltaTime;
            this.Fire();
        }

        void IFixedUpdateHandler.Tick() => this.Fire();

        private void Fire()
        {
            this.Ticks++;
            this.OnTick?.Invoke(this);
        }
    }

    /// <summary>Lets one set of contract tests run against both service managers.</summary>
    public interface ITickDriver
    {
        void Register(TestHandler handler);
        void Deregister(TestHandler handler);
        void Tick();
        void Clear();
    }

    public sealed class UpdateDriver : ITickDriver
    {
        public void Register(TestHandler handler) => UpdateServiceManager.RegisterUpdateHandler(handler);
        public void Deregister(TestHandler handler) => UpdateServiceManager.DeregisterUpdateHandler(handler);
        public void Tick() => UpdateServiceManager.UpdateTime();
        public void Clear() => UpdateServiceManager.Clear();
    }

    public sealed class FixedUpdateDriver : ITickDriver
    {
        public void Register(TestHandler handler) => FixedUpdateServiceManager.RegisterFixedUpdateHandler(handler);
        public void Deregister(TestHandler handler) => FixedUpdateServiceManager.DeregisterFixedUpdateHandler(handler);
        public void Tick() => FixedUpdateServiceManager.FixedUpdateTime();
        public void Clear() => FixedUpdateServiceManager.Clear();
    }
}
