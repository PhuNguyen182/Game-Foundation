namespace DracoRuan.Foundation.Initializers.Interfaces
{
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Check if this service has been initialized complete yet,
        /// All services MUST inherit this interface for waiting in the located lifetime scope consistently.
        /// Which services inherit this interface should be registered as EntryPoint
        /// </summary>
        public bool IsInitialized();
    }
}