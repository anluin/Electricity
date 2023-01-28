using Electricity.BlockEntityBehavior;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.BlockEntity
{
    public class BlockEntityAccumulator : Vintagestory.API.Common.BlockEntity
    {
        private BEBehaviorElectricity Electricity
            => GetBehavior<BEBehaviorElectricity>();

        public override void OnBlockPlaced(ItemStack? byItemStack = null)
        {
            base.OnBlockPlaced(byItemStack);
            Electricity.Connection = Facing.DownAll;
        }
    }
}