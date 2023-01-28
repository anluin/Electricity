using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Content.Block {
    public class Accumulator : Vintagestory.API.Common.Block {
        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode) {
            return world.BlockAccessor
                       .GetBlock(blockSel.Position.AddCopy(BlockFacing.DOWN))
                       .SideSolid[BlockFacing.indexUP] &&
                   base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (
                !world.BlockAccessor
                    .GetBlock(pos.AddCopy(BlockFacing.DOWN))
                    .SideSolid[BlockFacing.indexUP]
            )
                world.BlockAccessor.BreakBlock(pos, null);
        }
    }
}
