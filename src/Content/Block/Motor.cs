using System.Collections.Generic;
using System.Linq;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Electricity.Content.Block {
    public class Motor : Vintagestory.API.Common.Block, IMechanicalPowerBlock {
        private readonly static Dictionary<Facing, MeshData> MeshData = new Dictionary<Facing, MeshData>();

        public MechanicalNetwork? GetNetwork(IWorldAccessor world, BlockPos pos) {
            if (world.BlockAccessor.GetBlockEntity(pos)?.GetBehavior<BEBehaviorMPBase>() is IMechanicalPowerDevice device) 
                return device.Network;

            return null;
        }

        public bool HasMechPowerConnectorAt(IWorldAccessor world, BlockPos pos, BlockFacing face) {
            if (world.BlockAccessor.GetBlockEntity(pos) is Entity.Motor entity && entity.Facing != Facing.None)
                return FacingHelper.Directions(entity.Facing).First() == face;

            return false;
        }

        public void DidConnectAt(IWorldAccessor world, BlockPos pos, BlockFacing face) { }

        public override void OnLoaded(ICoreAPI coreApi) {
            base.OnLoaded(coreApi);
        }

        public override bool TryPlaceBlock(IWorldAccessor world, IPlayer byPlayer, ItemStack itemstack, BlockSelection blockSel, ref string failureCode) {
            var selection = new Selection(blockSel);
            var facing = FacingHelper.From(selection.Face, selection.Direction);

            if (
                FacingHelper.Faces(facing).First() is { } blockFacing &&
                !world.BlockAccessor
                    .GetBlock(blockSel.Position.AddCopy(blockFacing))
                    .SideSolid[blockFacing.Opposite.Index]
            )
                return false;

            return base.TryPlaceBlock(world, byPlayer, itemstack, blockSel, ref failureCode);
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel, ItemStack byItemStack) {
            var selection = new Selection(blockSel);
            var facing = FacingHelper.From(selection.Face, selection.Direction);

            if (
                base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack) &&
                world.BlockAccessor.GetBlockEntity(blockSel.Position) is Entity.Motor entity
            ) {
                entity.Facing = facing;

                var blockFacing = FacingHelper.Directions(entity.Facing).First();
                var blockPos = blockSel.Position;
                var blockPos1 = blockPos.AddCopy(blockFacing);

                if (
                    world.BlockAccessor.GetBlock(blockPos1) is IMechanicalPowerBlock block &&
                    block.HasMechPowerConnectorAt(world, blockPos1, blockFacing.Opposite)
                ) {
                    block.DidConnectAt(world, blockPos1, blockFacing.Opposite);
                    world.BlockAccessor.GetBlockEntity(blockPos)?
                        .GetBehavior<BEBehaviorMPBase>()?.tryConnect(blockFacing);
                }

                return true;
            }

            return false;
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (
                world.BlockAccessor.GetBlockEntity(pos) is Entity.Motor entity &&
                FacingHelper.Faces(entity.Facing).First() is { } blockFacing &&
                !world.BlockAccessor.GetBlock(pos.AddCopy(blockFacing)).SideSolid[blockFacing.Opposite.Index]
            )
                world.BlockAccessor.BreakBlock(pos, null);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos pos, Vintagestory.API.Common.Block[] chunkExtBlocks, int extIndex3d) {
            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, pos, chunkExtBlocks, extIndex3d);

            if (this.api is ICoreClientAPI clientApi && this.api.World.BlockAccessor.GetBlockEntity(pos) is Entity.Motor entity &&
                entity.Facing != Facing.None
               ) {
                var facing = entity.Facing;

                if (!MeshData.TryGetValue(facing, out var meshData)) {
                    var origin = new Vec3f(0.5f, 0.5f, 0.5f);
                    var block = clientApi.World.GetBlock(new AssetLocation("electricity:motor-stator"));

                    clientApi.Tesselator.TesselateBlock(block, out meshData);

                    if ((facing & Facing.NorthEast) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 270.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.NorthWest) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 90.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.NorthUp) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.NorthDown) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.EastNorth) != 0)
                        meshData.Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.EastSouth) != 0)
                        meshData.Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.EastUp) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.EastDown) != 0)
                        meshData.Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.SouthEast) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.SouthWest) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.SouthUp) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.SouthDown) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.WestNorth) != 0)
                        meshData.Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.WestSouth) != 0)
                        meshData.Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.WestUp) != 0)
                        meshData.Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.WestDown) != 0)
                        meshData.Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.UpNorth) != 0)
                        meshData.Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.UpEast) != 0)
                        meshData.Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.UpSouth) != 0)
                        meshData.Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.UpWest) != 0)
                        meshData.Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD);

                    if ((facing & Facing.DownNorth) != 0)
                        meshData.Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.DownEast) != 0)
                        meshData.Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.DownSouth) != 0)
                        meshData.Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 0.0f);

                    if ((facing & Facing.DownWest) != 0)
                        meshData.Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 0.0f);

                    MeshData.Add(facing, meshData);
                }

                sourceMesh = meshData;
            }
        }
    }
}
