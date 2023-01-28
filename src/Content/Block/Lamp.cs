using System;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Content.Block {
    public class Lamp : Vintagestory.API.Common.Block {
        private readonly Cuboidf[] collisionBoxes = {
            new Cuboidf(0.0f, 1.0f / 16.0f * 15.0f, 0.0f, 1.0f, 1.0f, 1.0f)
        };

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode) {
            return world.BlockAccessor
                       .GetBlock(blockSel.Position.AddCopy(BlockFacing.UP))
                       .SideSolid[BlockFacing.indexDOWN] &&
                   base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos pos, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
            var block = world.GetBlock(new AssetLocation("electricity:lamp-disabled"));

            return new[] {
                new ItemStack(block, (int)Math.Ceiling(dropQuantityMultiplier))
            };
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (
                !world.BlockAccessor
                    .GetBlock(pos.AddCopy(BlockFacing.UP))
                    .SideSolid[BlockFacing.indexDOWN]
            )
                world.BlockAccessor.BreakBlock(pos, null);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) {
            return this.collisionBoxes;
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos pos) {
            return this.collisionBoxes;
        }

        public override Cuboidf[] GetParticleCollisionBoxes(IBlockAccessor blockAccessor, BlockPos pos) {
            return this.collisionBoxes;
        }
    }
}
