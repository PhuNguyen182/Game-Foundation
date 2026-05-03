using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using VContainer;
using VContainer.Unity;

namespace Test
{
    [AutoRegister(LifetimeScope = nameof(Lifetime.Singleton), AsImplementedInterfaces = false, IsEntryPoint = true,
        AsSelf = true, WithKey = "123")]
    public class ExampleClassServiceA
    {

    }

    [AutoRegister(LifetimeScope = nameof(Lifetime.Transient), AsImplementedInterfaces = true)]
    public class ExampleClassServiceB
    {

    }
}