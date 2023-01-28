using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity {
    public class Accumulator : BlockEntity {
        private Behavior.Electricity Electricity {
            get => GetBehavior<Behavior.Electricity>();
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null) {
            base.OnBlockPlaced(byItemStack);

            this.Electricity.Connection = Facing.DownAll;
        }
    }
}
