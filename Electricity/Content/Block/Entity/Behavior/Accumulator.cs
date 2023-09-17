using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace Electricity.Content.Block.Entity.Behavior {
    public class Accumulator : BlockEntityBehavior, IElectricAccumulator {
        private int capacity;

        public Accumulator(BlockEntity blockEntity) : base(blockEntity) {
        }

        public int GetMaxCapacity() {
            return 16000;
        }

        public int GetCapacity() {
            return this.capacity;
        }

        public void Store(int amount) {
            this.capacity += amount;
        }

        public void Release(int amount) {
            this.capacity -= amount;
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetInt("electricity:capacity", this.capacity);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            this.capacity = tree.GetInt("electricity:capacity");
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar(this.GetCapacity() * 100.0f / this.GetMaxCapacity()));
            stringBuilder.AppendLine("└ Storage: " + this.GetCapacity() + "/" + this.GetMaxCapacity() + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
