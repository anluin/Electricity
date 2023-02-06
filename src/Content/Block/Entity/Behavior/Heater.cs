using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class Heater : BlockEntityBehavior, IElectricConsumer {
        public Heater(BlockEntity blockEntity) : base(blockEntity) { }

        public int HeatLevel { get; private set; }

        public ConsumptionRange ConsumptionRange => new ConsumptionRange(1, 8);

        public void Consume(int heatLevel) {
            if (this.Api is { } api) {
                if (heatLevel != this.HeatLevel) {
                    if (this.HeatLevel == 0 && heatLevel > 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "enabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    if (this.HeatLevel > 0 && heatLevel == 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "disabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    this.Blockentity.Block.LightHsv = new[] {
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 8),
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 2),
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 21)
                    };

                    this.HeatLevel = heatLevel;
                    this.Blockentity.MarkDirty(true);
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar((this.HeatLevel * 100.0f) / 8.0f));
            stringBuilder.AppendLine("└ Consumption: " + this.HeatLevel + "/" + 8 + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
