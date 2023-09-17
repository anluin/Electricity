using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity.Behavior {
    public class ElectricForge : BlockEntityBehavior, IElectricConsumer {
        private int maxTemp;
        private int powerSetting;

        public ElectricForge(BlockEntity blockEntity) : base(blockEntity) {
        }

        public ConsumptionRange ConsumptionRange => new(10, 100);

        public void Consume(int amount) {
            if (this.powerSetting != amount) {
                this.powerSetting = amount;
                this.maxTemp = amount * 1100 / 100;

                if (this.Blockentity is Entity.ElectricForge entity) {
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
