using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Util;

namespace Electricity.Content.Block {
    public class ElectricForge : Vintagestory.API.Common.Block {
        private WorldInteraction[]? interactions;

        public override void OnLoaded(ICoreAPI api) {
            base.OnLoaded(api);

            if (api is ICoreClientAPI clientApi) {
                this.interactions = ObjectCacheUtil.GetOrCreate(
                    api,
                    "forgeBlockInteractions",
                    () =>
                    {
                        var heatableStacklist = new List<ItemStack>();

                        foreach (
                            var stacks in
                            from obj in api.World.Collectibles
                            let firstCodePart = obj.FirstCodePart()
                            where firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem"
                            select obj.GetHandBookStacks(clientApi)
                            into stacks
                            where stacks != null
                            select stacks
                        ) {
                            heatableStacklist.AddRange(stacks);
                        }

                        return new[] {
                            new WorldInteraction {
                                ActionLangCode = "blockhelp-forge-addworkitem",
                                HotKeyCode = "shift",
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks = heatableStacklist.ToArray(),
                                GetMatchingStacks = (worldInteraction, blockSelection, entitySelection) =>
                                {
                                    if (api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.ElectricForge { Contents: { } } bef) {
                                        return worldInteraction.Itemstacks.Where(stack => stack.Equals(api.World, bef.Contents, GlobalConstants.IgnoredStackAttributes)).ToArray();
                                    }

                                    return worldInteraction.Itemstacks;
                                }
                            },
                            new WorldInteraction {
                                ActionLangCode = "blockhelp-forge-takeworkitem",
                                HotKeyCode = null,
                                MouseButton = EnumMouseButton.Right,
                                Itemstacks = heatableStacklist.ToArray(),
                                GetMatchingStacks = (worldInteraction, blockSelection, entitySelection) =>
                                {
                                    if (api.World.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.ElectricForge { Contents: { } } bef) {
                                        return new[] { bef.Contents };
                                    }

                                    return null;
                                }
                            }
                        };
                    }
                );
            }
        }

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

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is Entity.ElectricForge entity) {
                return entity.OnPlayerInteract(world, byPlayer, blockSel);
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override WorldInteraction[] GetPlacedBlockInteractionHelp(IWorldAccessor world, BlockSelection selection, IPlayer forPlayer) {
            return this.interactions.Append(base.GetPlacedBlockInteractionHelp(world, selection, forPlayer));
        }
    }
}
