using Electricity.BlockEntity;
using Electricity.BlockEntityBehavior;
using Electricity.Utils;
using Vintagestory.API.Common;

namespace Electricity.Block
{
    public class BlockSwitch : Vintagestory.API.Common.Block
    {
        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel,
            ItemStack byItemStack)
        {
            var selection = new Selection(blockSel);
            var face = FacingHelper.FromFace(selection.Face);

            if (!(world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCable blockEntity
                  && blockEntity.GetBehavior<BEBehaviorElectricity>() is { } electricity
                  && (blockEntity.Switches & face) == 0 && (electricity.Connection & face) != 0))
                return false;

            blockEntity.Switches = (blockEntity.Switches & ~face) | selection.Facing;
            blockEntity.SwitchesState |= face;
            blockEntity.MarkDirty(true);

            return true;
        }
    }
}