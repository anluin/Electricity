using System;
using System.Linq;
using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class Heater : BlockEntityBehavior, IElectricConsumer {
        private int heatLevel;

        public Heater(BlockEntity blockEntity) : base(blockEntity) { }

        public ConsumptionRange ConsumptionRange {
            get => new ConsumptionRange(1, 8);
        }

        public int HeatLevel {
            get => this.heatLevel;
        }

        public void Consume(int heatLevel) {
            if (this.Api is { } api)
                if (heatLevel != this.heatLevel) {
                    if (this.heatLevel == 0 && heatLevel > 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "enabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    if (this.heatLevel > 0 && heatLevel == 0) {
                        var assetLocation = this.Blockentity.Block.CodeWithVariant("state", "disabled");
                        var block = api.World.BlockAccessor.GetBlock(assetLocation);
                        api.World.BlockAccessor.ExchangeBlock(block.Id, this.Blockentity.Pos);
                    }

                    this.Blockentity.Block.LightHsv = new[] {
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 8),
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 2),
                        (byte)FloatHelper.Remap(heatLevel, 0, 32, 0, 21)
                    };

                    this.heatLevel = heatLevel;
                    this.Blockentity.MarkDirty(true);
                }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar(this.heatLevel * 100.0f / 8.0f));
            stringBuilder.AppendLine("└ Consumption: " + this.heatLevel + "/" + 8 + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
