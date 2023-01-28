namespace Electricity.Interface
{
    public interface IElectricAccumulator
    {
        public int GetMaxCapacity();
        public int GetCapacity();
        public void Store(int amount);
        public void Release(int amount);
    }
}