public static class TypeCreator
{
    public static T Create<T>() where T : class, new() => new();
}
