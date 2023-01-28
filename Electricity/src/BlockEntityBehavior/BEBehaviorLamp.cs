using Electricity.Interface;
using Electricity.Utils;

namespace Electricity.BlockEntityBehavior
{
    public sealed class BEBehaviorLamp : Vintagestory.API.Common.BlockEntityBehavior, IElectricConsumer
    {
        private int _lightLevel;

        public BEBehaviorLamp(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public ConsumptionRange ConsumptionRange => new ConsumptionRange(1, 8);

        public void Consume(int lightLevel)
        {
            if (Api is { } api)
                if (lightLevel != _lightLevel)
                {
                    if (_lightLevel == 0 && lightLevel > 0)
                    {
                        var assetLocation = Blockentity.Block.CodeWithVariant("state", "enabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, Blockentity.Pos);
                    }

                    if (_lightLevel > 0 && lightLevel == 0)
                    {
                        var assetLocation = Blockentity.Block.CodeWithVariant("state", "disabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, Blockentity.Pos);
                    }

                    Blockentity.Block.LightHsv = new[]
                    {
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 8),
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 2),
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 21)
                    };

                    Blockentity.MarkDirty(true);
                    _lightLevel = lightLevel;
                }
        }
    }
}