namespace Electricity.Interface
{
    public struct ConsumptionRange
    {
        public readonly int Min;
        public readonly int Max;

        public ConsumptionRange(int min, int max)
        {
            Min = min;
            Max = max;
        }
    }

    public interface IElectricConsumer
    {
        public ConsumptionRange ConsumptionRange { get; }
        public void Consume(int amount);
    }
}