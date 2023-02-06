using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class Lamp : BlockEntityBehavior, IElectricConsumer {
        public Lamp(BlockEntity blockEntity) : base(blockEntity) { }

        public int LightLevel { get; private set; }

        public ConsumptionRange ConsumptionRange => new ConsumptionRange(1, 8);

        public void Consume(int lightLevel) {
            if (this.Api is { } api) {
                if (lightLevel != this.LightLevel) {
                    if (this.LightLevel == 0 && lightLevel > 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "enabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    if (this.LightLevel > 0 && lightLevel == 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "disabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    this.Blockentity.Block.LightHsv = new[] {
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 8),
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 2),
                        (byte)FloatHelper.Remap(lightLevel, 0, 8, 0, 21)
                    };

                    this.Blockentity.MarkDirty(true);
                    this.LightLevel = lightLevel;
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar((this.LightLevel * 100.0f) / 8.0f));
            stringBuilder.AppendLine("└ Consumption: " + this.LightLevel + "/" + 8 + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
