namespace ObjectPooling
{
    public interface IPoolable
    {
        public void OnGet();
        public void OnRelease();
    }
}
