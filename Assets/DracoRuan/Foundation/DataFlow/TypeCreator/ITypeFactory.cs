using System;

namespace DracoRuan.Foundation.DataFlow.TypeCreator
{
    /// <summary>
    /// Interface for high-performance type instantiation factory.
    /// Provides significantly faster object creation compared to Activator.CreateInstance().
    /// </summary>
    /// <remarks>
    /// This interface defines the contract for factories that use compiled expression trees
    /// to cache constructor delegates, achieving performance improvements of 100x or more
    /// over standard reflection.
    /// 
    /// NOTE: The default TypeFactory implementation is a static class for maximum performance.
    /// This interface is provided for scenarios where you need instance-based factories,
    /// such as dependency injection or testability requirements.
    /// 
    /// Use TypeFactoryWrapper to wrap the static TypeFactory as an ITypeFactory instance.
    /// </remarks>
    /// <example>
    /// <code>
    /// // For most cases, use the static TypeFactory directly:
    /// var player = TypeFactory.Create&lt;Player&gt;();
    /// 
    /// // For DI scenarios, use wrapper:
    /// ITypeFactory factory = new TypeFactoryWrapper();
    /// var enemy = factory.Create&lt;Enemy&gt;();
    /// </code>
    /// </example>
    public interface ITypeFactory
    {
        /// <summary>
        /// Creates a new instance of the specified type with maximum performance.
        /// </summary>
        /// <typeparam name="T">The type to instantiate (must have parameterless constructor)</typeparam>
        /// <returns>A new instance of type T</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when type T does not have an accessible parameterless constructor
        /// </exception>
        public T Create<T>() where T : class;
        
        /// <summary>
        /// Creates a new instance of the specified type using cached compiled delegates.
        /// </summary>
        /// <param name="type">The type to instantiate</param>
        /// <returns>A new instance of the specified type</returns>
        /// <exception cref="ArgumentNullException">Thrown when type is null</exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown when type does not have an accessible parameterless constructor
        /// </exception>
        public object Create(Type type);
        
        /// <summary>
        /// Checks if the factory can create instances of the specified type.
        /// </summary>
        /// <typeparam name="T">The type to check</typeparam>
        /// <returns>True if the type can be instantiated, false otherwise</returns>
        public bool CanCreate<T>() where T : class;
        
        /// <summary>
        /// Checks if the factory can create instances of the specified type.
        /// </summary>
        /// <param name="type">The type to check</param>
        /// <returns>True if the type can be instantiated, false otherwise</returns>
        public bool CanCreate(Type type);
        
        /// <summary>
        /// Clears all cached constructor delegates to free memory.
        /// </summary>
        /// <remarks>
        /// Use this method carefully as it will require recompilation of delegates
        /// on the next Create() call, which may cause temporary performance degradation.
        /// </remarks>
        public void ClearCache();
        
        /// <summary>
        /// Gets the current number of cached constructor delegates.
        /// </summary>
        /// <value>The count of cached type constructors</value>
        public int CachedTypeCount { get; }
    }
}

