namespace DracoRuan.Foundation.DataFlow.TypeCreator
{
    public static class SimpleTypeCreator
    {
        public static T Create<T>() where T : new() => new();
    }
}
