using DracoRuan.Foundation.Initializers.AutoRegisterAttributes;
using VContainer;

namespace Test
{
    [RegisterService(LifetimeScope = nameof(Lifetime.Singleton), AsImplementedInterfaces = false, IsEntryPoint = true,
        AsSelf = true, WithKey = "123")]
    public class ExampleClassServiceA
    {

    }

    [RegisterService(LifetimeScope = nameof(Lifetime.Transient), AsImplementedInterfaces = true)]
    public class ExampleClassServiceB
    {

    }
}