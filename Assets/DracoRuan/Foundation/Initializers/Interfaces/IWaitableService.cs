namespace DracoRuan.Foundation.Initializers.Interfaces
{
    public interface IWaitableService
    {
        /// <summary>
        /// Check if this service has been initialized complete yet
        /// </summary>
        public bool IsInitialized { get; }
        
        /// <summary>
        /// Use this to notify the service orchestrator marks this service as initialized complete 
        /// </summary>
        /// <param name="lifetimeScopeIdentifier">Name of lifetime scope that service belong to</param>
        /// <example>nameof(ProjectLifetimeScope), nameof(HomeSceneLifetimeScope), etc.</example>
        public void MarkServiceComplete(string lifetimeScopeIdentifier);
    }
}