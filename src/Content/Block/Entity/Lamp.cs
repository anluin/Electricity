using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block.Entity {
    public class Lamp : BlockEntity {
        private Behavior.Electricity Electricity {
            get => GetBehavior<Behavior.Electricity>();
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null) {
            base.OnBlockPlaced(byItemStack);

            this.Electricity.Connection = Facing.UpAll;
        }
    }
}
