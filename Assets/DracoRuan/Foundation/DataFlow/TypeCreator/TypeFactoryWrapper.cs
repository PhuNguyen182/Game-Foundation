using System;

namespace DracoRuan.Foundation.DataFlow.TypeCreator
{
    /// <summary>
    /// Wrapper class that implements ITypeFactory by delegating to the static TypeFactory.
    /// Use this when you need an instance-based factory for dependency injection or testing.
    /// </summary>
    /// <remarks>
    /// This wrapper adds minimal overhead (just a method call indirection) while providing
    /// interface compatibility. The actual performance benefits come from the static
    /// TypeFactory's cached delegates.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Use in dependency injection
    /// public class ServiceContainer
    /// {
    ///     private readonly ITypeFactory factory;
    ///     
    ///     public ServiceContainer(ITypeFactory factory = null)
    ///     {
    ///         this.factory = factory ?? new TypeFactoryWrapper();
    ///     }
    ///     
    ///     public T Resolve&lt;T&gt;() where T : class
    ///     {
    ///         return this.factory.Create&lt;T&gt;();
    ///     }
    /// }
    /// </code>
    /// </example>
    public sealed class TypeFactoryWrapper : ITypeFactory
    {
        /// <inheritdoc/>
        public T Create<T>() where T : class
        {
            return TypeFactory.Create<T>();
        }
        
        /// <inheritdoc/>
        public object Create(Type type)
        {
            return TypeFactory.Create(type);
        }
        
        /// <inheritdoc/>
        public bool CanCreate<T>() where T : class
        {
            return TypeFactory.CanCreate<T>();
        }
        
        /// <inheritdoc/>
        public bool CanCreate(Type type)
        {
            return TypeFactory.CanCreate(type);
        }
        
        /// <inheritdoc/>
        public void ClearCache()
        {
            TypeFactory.ClearCache();
        }
        
        /// <inheritdoc/>
        public int CachedTypeCount => TypeFactory.CachedTypeCount;
    }
}