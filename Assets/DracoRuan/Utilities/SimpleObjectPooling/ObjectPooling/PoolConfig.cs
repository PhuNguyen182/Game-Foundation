namespace ObjectPooling
{
    public class PoolConfig<T> where T : class
    {
        public int InitialCapacity { get; set; } = 10;
        public int MaxCapacity { get; set; }
    }
}
