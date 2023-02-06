using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Content.Block {
    public class Switch : Vintagestory.API.Common.Block {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack) {
            var selection = new Selection(blockSel);
            var face = FacingHelper.FromFace(selection.Face);

            if (
                !(world.BlockAccessor.GetBlockEntity(blockSel.Position) is Entity.Cable blockEntity &&
                  blockEntity.GetBehavior<Entity.Behavior.Electricity>() is { } electricity &&
                  (blockEntity.Switches & face) == 0 &&
                  (electricity.Connection & face) != 0)
            ) {
                return false;
            }

            blockEntity.Switches = (blockEntity.Switches & ~face) | selection.Facing;
            blockEntity.SwitchesState |= face;
            blockEntity.MarkDirty(true);

            return true;
        }
    }
}
