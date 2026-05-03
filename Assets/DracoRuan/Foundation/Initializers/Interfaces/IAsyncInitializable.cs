namespace DracoRuan.Foundation.Initializers.Interfaces
{
    public interface IAsyncInitializable
    {
        /// <summary>
        /// Check if this service has been initialized complete yet
        /// </summary>
        public bool IsInitialized();
    }
}