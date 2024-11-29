namespace Pool
{
    public interface IPoolObject
    {
        public PoolObjectKey poolObjectKey { get; set; }
        public void Use();
    }
}