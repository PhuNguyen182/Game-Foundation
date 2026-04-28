using System;
using VContainer;

namespace DracoRuan.Foundation.Initializers.AutoRegisterAttributes
{
    /// <summary>
    /// Use this attribute for automatically installing your service.
    /// </summary>
    /// <param name="LifetimeScope"> Same as VContainer.Lifetime (Singleton, Transient, Scoped).</param>>
    /// <param name="LifetimeScopeName"> The name of LifetimeScope that your installer living on</param>
    /// <example>nameof(ProjectLifetimeScope), nameof(HomeSceneLifetimeScope), etc...</example>
    /// <param name="AsImplementedInterfaces"> Toggle usage AsImplementedInterfaces in VContainer when register service</param>
    /// <code>.AsImplementedInterfaces()</code>
    /// <param name="IsEntryPoint"> Is this service entry point?</param>
    /// <code>
    /// builder.RegisterEntryPoint(Lifetime.Scope); // If is entry point service
    /// builder.Register(Lifetime.Singleton); // If is normal service
    /// </code>
    /// <param name="AsSelf"> Register service AsSelf() in VContainer</param>
    /// <code>.AsSelf()</code>
    /// <param name="WithKey"> Register service with specified key</param>
    /// <code>.Keyed("123")</code>
    [AttributeUsage(AttributeTargets.Class)]
    public class RegisterServiceAttribute : Attribute
    {
        public string LifetimeScope { get; set; } = nameof(Lifetime.Scoped);
        public string LifetimeScopeName { get; set; } = nameof(ProjectLifetimeScope);
        public bool AsImplementedInterfaces { get; set; }
        public bool IsEntryPoint { get; set; }
        public bool AsSelf { get; set; }
        public string WithKey { get; set; }
    }
}
