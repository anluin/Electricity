using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public sealed class Forge : BlockEntityBehavior, IElectricConsumer {
        private int maxTemp;
        private int powerSetting;

        public Forge(BlockEntity blockEntity) : base(blockEntity) { }

        public ConsumptionRange ConsumptionRange {
            get => new ConsumptionRange(10, 100);
        }

        public void Consume(int amount) {
            if (this.powerSetting != amount) {
                this.powerSetting = amount;
                this.maxTemp = amount * 1100 / 100;

                if (this.Blockentity is Entity.Forge entity) {
                    entity.MaxTemp = this.maxTemp;
                    entity.IsBurning = amount > 0;
                }
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar(this.powerSetting));
            stringBuilder.AppendLine("├ Consumption: " + this.powerSetting + "/" + 100 + "⚡   ");
            stringBuilder.AppendLine("└ Temperature: " + this.maxTemp + "° (max.)");
            stringBuilder.AppendLine();
        }
    }
}
