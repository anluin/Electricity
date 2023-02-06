using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Content.Block {
    public class Cable : Vintagestory.API.Common.Block {
        private readonly static ConcurrentDictionary<CacheDataKey, Dictionary<Facing, Cuboidf[]>> CollisionBoxesCache =
            new ConcurrentDictionary<CacheDataKey, Dictionary<Facing, Cuboidf[]>>();

        private readonly static Dictionary<CacheDataKey, Dictionary<Facing, Cuboidf[]>> SelectionBoxesCache =
            new Dictionary<CacheDataKey, Dictionary<Facing, Cuboidf[]>>();

        private readonly static Dictionary<CacheDataKey, MeshData> MeshDataCache =
            new Dictionary<CacheDataKey, MeshData>();

        private BlockVariant? disabledSwitchVariant;

        private BlockVariant? dotVariant;
        private BlockVariant? enabledSwitchVariant;
        private BlockVariant? partVariant;

        public override void OnLoaded(ICoreAPI api) {
            base.OnLoaded(api);

            /* preload switch-assets */
            {
                var assetLocation = new AssetLocation("electricity:switch-enabled");
                var block = api.World.BlockAccessor.GetBlock(assetLocation);

                this.enabledSwitchVariant = new BlockVariant(api, block, "enabled");
                this.disabledSwitchVariant = new BlockVariant(api, block, "disabled");
            }

            this.dotVariant = new BlockVariant(api, this, "dot");
            this.partVariant = new BlockVariant(api, this, "part");
        }

        public override bool IsReplacableBy(Vintagestory.API.Common.Block block) {
            return base.IsReplacableBy(block) || block is Cable || block is Switch;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSelection, ItemStack byItemStack) {
            var selection = new Selection(blockSelection);
            var facing = FacingHelper.From(selection.Face, selection.Direction);

            /* update existing cable */
            {
                if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.Cable entity) {
                    if ((entity.Connection & facing) != 0) {
                        return false;
                    }

                    entity.Connection |= facing;

                    return true;
                }
            }

            if (base.DoPlaceBlock(world, byPlayer, blockSelection, byItemStack)) {
                if (world.BlockAccessor.GetBlockEntity(blockSelection.Position) is Entity.Cable entity) {
                    entity.Connection = facing;
                }

                return true;
            }

            return false;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos position, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
            if (this.api is ICoreClientAPI) {
                return;
            }

            if (world.BlockAccessor.GetBlockEntity(position) is Entity.Cable entity) {
                if (byPlayer is { CurrentBlockSelection: { } blockSelection }) {
                    var key = CacheDataKey.FromEntity(entity);
                    var hitPosition = blockSelection.HitPosition;
                    var selectedFacing = (
                            from keyValuePair in CalculateBoxes(
                                key,
                                SelectionBoxesCache,
                                this.dotVariant!.SelectionBoxes,
                                this.partVariant!.SelectionBoxes,
                                this.enabledSwitchVariant!.SelectionBoxes,
                                this.disabledSwitchVariant!.SelectionBoxes
                            )
                            let selectionFacing = keyValuePair.Key
                            let selectionBoxes = keyValuePair.Value
                            from selectionBox in selectionBoxes
                            where selectionBox.Clone()
                                .OmniGrowBy(0.01f)
                                .Contains(hitPosition.X, hitPosition.Y, hitPosition.Z)
                            select selectionFacing
                        )
                        .Aggregate(
                            Facing.None,
                            (current, selectionFacing) =>
                                current | selectionFacing
                        );

                    var selectedSwitches = entity.Switches & selectedFacing;

                    if (selectedSwitches != Facing.None) {
                        var stackSize = FacingHelper.Faces(selectedSwitches).Count();

                        if (stackSize > 0) {
                            entity.Switches &= ~selectedFacing;
                            entity.MarkDirty(true);

                            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) {
                                var assetLocation = new AssetLocation("electricity:switch-enabled");
                                var block = world.BlockAccessor.GetBlock(assetLocation);
                                var itemStack = new ItemStack(block, stackSize);
                                world.SpawnItemEntity(itemStack, position.ToVec3d());
                            }

                            return;
                        }
                    }

                    var connection = entity.Connection & ~selectedFacing;

                    if (connection != Facing.None) {
                        var stackSize = FacingHelper.Count(selectedFacing);

                        if (stackSize > 0) {
                            entity.Connection = connection;
                            entity.MarkDirty(true);

                            if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative) {
                                var assetLocation = new AssetLocation("electricity:cable-dot");
                                var block = world.BlockAccessor.GetBlock(assetLocation);
                                var itemStack = new ItemStack(block, stackSize);

                                world.SpawnItemEntity(itemStack, position.ToVec3d());
                            }

                            return;
                        }
                    }
                }
            }

            base.OnBlockBroken(world, position, byPlayer, dropQuantityMultiplier);
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos position, IPlayer byPlayer, float dropQuantityMultiplier = 1) {
            if (world.BlockAccessor.GetBlockEntity(position) is Entity.Cable entity) {
                var assetLocation = new AssetLocation("electricity:cable-dot");
                var block = world.BlockAccessor.GetBlock(assetLocation);
                var stackSize = FacingHelper.Count(entity.Connection);
                var itemStack = new ItemStack(block, stackSize);

                return new[] { itemStack };
            }

            return base.GetDrops(world, position, byPlayer, dropQuantityMultiplier);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos) {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.BlockAccessor.GetBlockEntity(pos) is Entity.Cable entity) {
                var blockFacing = BlockFacing.FromVector(neibpos.X - pos.X, neibpos.Y - pos.Y, neibpos.Z - pos.Z);
                var selectedFacing = FacingHelper.FromFace(blockFacing);

                if ((entity.Connection & ~ selectedFacing) == Facing.None) {
                    world.BlockAccessor.BreakBlock(pos, null);

                    return;
                }

                var selectedSwitches = entity.Switches & selectedFacing;

                if (selectedSwitches != Facing.None) {
                    var stackSize = FacingHelper.Faces(selectedSwitches).Count();

                    if (stackSize > 0) {
                        var assetLocation = new AssetLocation("electricity:switch-enabled");
                        var block = world.BlockAccessor.GetBlock(assetLocation);
                        var itemStack = new ItemStack(block, stackSize);
                        world.SpawnItemEntity(itemStack, pos.ToVec3d());
                    }

                    entity.Switches &= ~selectedFacing;
                }

                var selectedConnection = entity.Connection & selectedFacing;

                if (selectedConnection != Facing.None) {
                    var stackSize = FacingHelper.Count(selectedConnection);

                    if (stackSize > 0) {
                        var assetLocation = new AssetLocation("electricity:cable-dot");
                        var block = world.BlockAccessor.GetBlock(assetLocation);
                        var itemStack = new ItemStack(block, stackSize);
                        world.SpawnItemEntity(itemStack, pos.ToVec3d());

                        entity.Connection &= ~selectedConnection;
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            if (this.api is ICoreClientAPI) {
                return true;
            }

            var hitPosition = blockSel.HitPosition;

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is Entity.Cable entity) {
                var key = CacheDataKey.FromEntity(entity);
                var selectedFacing = (
                        from keyValuePair in CalculateBoxes(
                            key,
                            SelectionBoxesCache,
                            this.dotVariant!.SelectionBoxes,
                            this.partVariant!.SelectionBoxes,
                            this.enabledSwitchVariant!.SelectionBoxes,
                            this.disabledSwitchVariant!.SelectionBoxes
                        )
                        let selectionFacing = keyValuePair.Key
                        let selectionBoxes = keyValuePair.Value
                        from selectionBox in selectionBoxes
                        where selectionBox.Clone()
                            .OmniGrowBy(0.005f)
                            .Contains(hitPosition.X, hitPosition.Y, hitPosition.Z)
                        select selectionFacing
                    )
                    .Aggregate(Facing.None, (current, selectionFacing) => current | selectionFacing);

                foreach (var face in FacingHelper.Faces(selectedFacing)) {
                    selectedFacing |= FacingHelper.FromFace(face);
                }

                var selectedSwitches = selectedFacing & entity.Switches;

                if (selectedSwitches != 0) {
                    entity.SwitchesState ^= selectedSwitches;

                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos position) {
            if (blockAccessor.GetBlockEntity(position) is Entity.Cable entity) {
                var key = CacheDataKey.FromEntity(entity);

                return CalculateBoxes(
                        key,
                        SelectionBoxesCache,
                        this.dotVariant!.SelectionBoxes,
                        this.partVariant!.SelectionBoxes,
                        this.enabledSwitchVariant!.SelectionBoxes,
                        this.disabledSwitchVariant!.SelectionBoxes
                    ).Values
                    .SelectMany(x => x)
                    .Distinct()
                    .ToArray();
            }

            return base.GetSelectionBoxes(blockAccessor, position);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos position) {
            if (blockAccessor.GetBlockEntity(position) is Entity.Cable entity) {
                var key = CacheDataKey.FromEntity(entity);

                return CalculateBoxes(
                        key,
                        CollisionBoxesCache,
                        this.dotVariant!.CollisionBoxes,
                        this.partVariant!.CollisionBoxes,
                        this.enabledSwitchVariant!.CollisionBoxes,
                        this.disabledSwitchVariant!.CollisionBoxes
                    ).Values
                    .SelectMany(x => x)
                    .Distinct()
                    .ToArray();
            }

            return base.GetSelectionBoxes(blockAccessor, position);
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos position, Vintagestory.API.Common.Block[] chunkExtBlocks, int extIndex3d) {
            if (this.api.World.BlockAccessor.GetBlockEntity(position) is Entity.Cable entity && entity.Connection != Facing.None) {
                var key = CacheDataKey.FromEntity(entity);

                if (!MeshDataCache.TryGetValue(key, out var meshData)) {
                    var origin = new Vec3f(0.5f, 0.5f, 0.5f);

                    // Connections
                    if ((key.Connection & Facing.NorthAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 0.0f));

                        if ((key.Connection & Facing.NorthEast) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 270.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.NorthWest) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 90.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.NorthUp) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.NorthDown) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD, 0.0f));
                        }
                    }

                    if ((key.Connection & Facing.EastAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 90.0f * GameMath.DEG2RAD));

                        if ((key.Connection & Facing.EastNorth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.EastSouth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.EastUp) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.EastDown) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 90.0f * GameMath.DEG2RAD));
                        }
                    }

                    if ((key.Connection & Facing.SouthAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 0.0f));

                        if ((key.Connection & Facing.SouthEast) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 270.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.SouthWest) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 90.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.SouthUp) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.SouthDown) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f * GameMath.DEG2RAD, 0.0f));
                        }
                    }

                    if ((key.Connection & Facing.WestAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 270.0f * GameMath.DEG2RAD));

                        if ((key.Connection & Facing.WestNorth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.WestSouth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.WestUp) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.WestDown) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 270.0f * GameMath.DEG2RAD));
                        }
                    }

                    if ((key.Connection & Facing.UpAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 180.0f * GameMath.DEG2RAD));

                        if ((key.Connection & Facing.UpNorth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.UpEast) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.UpSouth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                        }

                        if ((key.Connection & Facing.UpWest) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                        }
                    }

                    if ((key.Connection & Facing.DownAll) != 0) {
                        AddMeshData(ref meshData, this.dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 0.0f));

                        if ((key.Connection & Facing.DownNorth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.DownEast) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.DownSouth) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 0.0f));
                        }

                        if ((key.Connection & Facing.DownWest) != 0) {
                            AddMeshData(ref meshData, this.partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 0.0f));
                        }
                    }

                    // Switches
                    if ((key.Switches & Facing.NorthEast) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                90.0f * GameMath.DEG2RAD,
                                0.0f
                            )
                        );
                    }

                    if ((key.Switches & Facing.NorthWest) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                270.0f * GameMath.DEG2RAD,
                                0.0f
                            )
                        );
                    }

                    if ((key.Switches & Facing.NorthUp) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD,
                                0.0f
                            )
                        );
                    }

                    if ((key.Switches & Facing.NorthDown) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                0.0f * GameMath.DEG2RAD,
                                0.0f
                            )
                        );
                    }

                    if ((key.Switches & Facing.EastNorth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                180.0f * GameMath.DEG2RAD,
                                0.0f,
                                90.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.EastSouth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f * GameMath.DEG2RAD,
                                0.0f,
                                90.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.EastUp) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                270.0f * GameMath.DEG2RAD,
                                0.0f,
                                90.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.EastDown) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                0.0f,
                                90.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.SouthEast) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.SouthWest) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                270.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.SouthUp) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.SouthDown) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                0.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.WestNorth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                180.0f * GameMath.DEG2RAD,
                                0.0f,
                                270.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.WestSouth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f * GameMath.DEG2RAD,
                                0.0f,
                                270.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.WestUp) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                270.0f * GameMath.DEG2RAD,
                                0.0f,
                                270.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.WestDown) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                90.0f * GameMath.DEG2RAD,
                                0.0f,
                                270.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.UpNorth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f,
                                180.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.UpEast) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f,
                                90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.UpSouth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f,
                                0.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.UpWest) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone().Rotate(
                                origin,
                                0.0f,
                                270.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD
                            )
                        );
                    }

                    if ((key.Switches & Facing.DownNorth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 0.0f)
                        );
                    }

                    if ((key.Switches & Facing.DownEast) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 0.0f)
                        );
                    }

                    if ((key.Switches & Facing.DownSouth) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 0.0f)
                        );
                    }

                    if ((key.Switches & Facing.DownWest) != 0) {
                        AddMeshData(
                            ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? this.enabledSwitchVariant
                                : this.disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 0.0f)
                        );
                    }

                    MeshDataCache[key] = meshData!;
                }

                sourceMesh = meshData ?? sourceMesh;
            }

            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, position, chunkExtBlocks, extIndex3d);
        }

        private static Dictionary<Facing, Cuboidf[]> CalculateBoxes(CacheDataKey key, IDictionary<CacheDataKey, Dictionary<Facing, Cuboidf[]>> boxesCache,
            Cuboidf[] dotBoxes, Cuboidf[] partBoxes,
            Cuboidf[] enabledSwitchBoxes, Cuboidf[] disabledSwitchBoxes) {
            if (!boxesCache.TryGetValue(key, out var boxes)) {
                var origin = new Vec3d(0.5, 0.5, 0.5);

                boxesCache[key] = boxes = new Dictionary<Facing, Cuboidf[]>();

                // Connections
                if ((key.Connection & Facing.NorthAll) != 0) {
                    boxes.Add(Facing.NorthAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.NorthEast) != 0) {
                    boxes.Add(Facing.NorthEast, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 270.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.NorthWest) != 0) {
                    boxes.Add(Facing.NorthWest, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.NorthUp) != 0) {
                    boxes.Add(Facing.NorthUp, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.NorthDown) != 0) {
                    boxes.Add(Facing.NorthDown, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 180.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.EastAll) != 0) {
                    boxes.Add(Facing.EastAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.EastNorth) != 0) {
                    boxes.Add(Facing.EastNorth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.EastSouth) != 0) {
                    boxes.Add(Facing.EastSouth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 90.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.EastUp) != 0) {
                    boxes.Add(Facing.EastUp, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 90.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.EastDown) != 0) {
                    boxes.Add(Facing.EastDown, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 90.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.SouthAll) != 0) {
                    boxes.Add(Facing.SouthAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.SouthEast) != 0) {
                    boxes.Add(Facing.SouthEast, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 270.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.SouthWest) != 0) {
                    boxes.Add(Facing.SouthWest, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 90.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.SouthUp) != 0) {
                    boxes.Add(Facing.SouthUp, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 180.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.SouthDown) != 0) {
                    boxes.Add(Facing.SouthDown, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.WestAll) != 0) {
                    boxes.Add(Facing.WestAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.WestNorth) != 0) {
                    boxes.Add(Facing.WestNorth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.WestSouth) != 0) {
                    boxes.Add(Facing.WestSouth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 270.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.WestUp) != 0) {
                    boxes.Add(Facing.WestUp, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 270.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.WestDown) != 0) {
                    boxes.Add(Facing.WestDown, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 270.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.UpAll) != 0) {
                    boxes.Add(Facing.UpAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.UpNorth) != 0) {
                    boxes.Add(Facing.UpNorth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.UpEast) != 0) {
                    boxes.Add(Facing.UpEast, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 180.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.UpSouth) != 0) {
                    boxes.Add(Facing.UpSouth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 180.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.UpWest) != 0) {
                    boxes.Add(Facing.UpWest, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 180.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.DownAll) != 0) {
                    boxes.Add(Facing.DownAll, dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.DownNorth) != 0) {
                    boxes.Add(Facing.DownNorth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.DownEast) != 0) {
                    boxes.Add(Facing.DownEast, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.DownSouth) != 0) {
                    boxes.Add(Facing.DownSouth, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 0.0f, origin)).ToArray());
                }

                if ((key.Connection & Facing.DownWest) != 0) {
                    boxes.Add(Facing.DownWest, partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 0.0f, origin)).ToArray());
                }

                // Switches
                if ((key.Switches & Facing.NorthEast) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.NorthWest) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 270.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.NorthUp) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 180.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.NorthDown) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.EastNorth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 90.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.EastSouth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.EastUp) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 90.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.EastDown) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 90.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.SouthEast) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.SouthWest) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes).Select(
                            selectionBox =>
                                selectionBox.RotatedCopy(90.0f, 270.0f, 180.0f, origin)
                        ).ToArray()
                    );
                }

                if ((key.Switches & Facing.SouthUp) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes).Select(
                            selectionBox =>
                                selectionBox.RotatedCopy(90.0f, 180.0f, 180.0f, origin)
                        ).ToArray()
                    );
                }

                if ((key.Switches & Facing.SouthDown) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.WestNorth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 270.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.WestSouth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.WestUp) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 270.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.WestDown) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 270.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.UpNorth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.UpEast) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.UpSouth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.UpWest) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 180.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.DownNorth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.DownEast) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.DownSouth) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray()
                    );
                }

                if ((key.Switches & Facing.DownWest) != 0) {
                    AddBoxes(
                        ref boxes,
                        Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 0.0f, origin)).ToArray()
                    );
                }
            }

            return boxes;
        }

        private static void AddBoxes(ref Dictionary<Facing, Cuboidf[]> cache, Facing key, Cuboidf[] boxes) {
            if (cache.ContainsKey(key)) {
                cache[key] = cache[key].Concat(boxes).ToArray();
            }
            else {
                cache[key] = boxes;
            }
        }

        private static void AddMeshData(ref MeshData? sourceMesh, MeshData? meshData) {
            if (meshData != null) {
                if (sourceMesh != null) {
                    sourceMesh.AddMeshData(meshData);
                }
                else {
                    sourceMesh = meshData;
                }
            }
        }

        internal struct CacheDataKey {
            public readonly Facing Connection;
            public readonly Facing Switches;
            public readonly Facing SwitchesState;

            public CacheDataKey(Facing connection, Facing switches, Facing switchesState) {
                this.Connection = connection;
                this.Switches = switches;
                this.SwitchesState = switchesState;
            }

            public static CacheDataKey FromEntity(Entity.Cable entity) {
                return new CacheDataKey(
                    entity.Connection,
                    entity.Switches,
                    entity.SwitchesState
                );
            }
        }
    }
}
