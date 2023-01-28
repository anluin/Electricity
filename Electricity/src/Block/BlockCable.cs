using System.Collections.Generic;
using System.Linq;
using Electricity.BlockEntity;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Block
{
    internal struct MeshDataKey
    {
        public readonly Facing Connection;
        public readonly Facing Switches;
        public readonly Facing SwitchesState;

        public MeshDataKey(Facing connection, Facing switches, Facing switchesState)
        {
            Connection = connection;
            Switches = switches;
            SwitchesState = switchesState;
        }
    }

    public class BlockCable : Vintagestory.API.Common.Block
    {
        private static readonly Dictionary<MeshDataKey, Dictionary<Facing, Cuboidf[]>> CollisionBoxesCache =
            new Dictionary<MeshDataKey, Dictionary<Facing, Cuboidf[]>>();

        private static readonly Dictionary<MeshDataKey, Dictionary<Facing, Cuboidf[]>> SelectionBoxesCache =
            new Dictionary<MeshDataKey, Dictionary<Facing, Cuboidf[]>>();

        private static readonly Dictionary<MeshDataKey, MeshData?> MeshDataCache =
            new Dictionary<MeshDataKey, MeshData?>();

        private BlockVariant? _disabledSwitchVariant;

        private BlockVariant? _dotVariant;
        private BlockVariant? _enabledSwitchVariant;
        private BlockVariant? _partVariant;

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            _dotVariant = new BlockVariant(api, this, "dot");
            _partVariant = new BlockVariant(api, this, "part");

            var assetLocation = new AssetLocation("electricity:switch-enabled");
            var block = api.World.BlockAccessor.GetBlock(assetLocation);
            _enabledSwitchVariant = new BlockVariant(api, block, "enabled");
            _disabledSwitchVariant = new BlockVariant(api, block, "disabled");
        }

        public override bool IsReplacableBy(Vintagestory.API.Common.Block block)
        {
            return base.IsReplacableBy(block) || block is BlockCable || block is BlockSwitch;
        }

        public override bool DoPlaceBlock(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel,
            ItemStack byItemStack)
        {
            var hitSelection = new Selection(blockSel);
            var hitConnection = FacingHelper.From(hitSelection.Face, hitSelection.Direction);

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCable entity1)
            {
                if ((entity1.Connection & hitConnection) != 0) return false;

                entity1.Connection |= hitConnection;
                return true;
            }

            if (base.DoPlaceBlock(world, byPlayer, blockSel, byItemStack)
                && world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCable entity2)
            {
                entity2.Connection = hitConnection;

                return true;
            }

            return false;
        }

        public override ItemStack[] GetDrops(IWorldAccessor world, BlockPos position, IPlayer byPlayer,
            float dropQuantityMultiplier = 1)
        {
            if (world.BlockAccessor.GetBlockEntity(position) is BlockEntityCable entity)
            {
                var assetLocation = new AssetLocation("electricity:cable-part");
                var block = world.BlockAccessor.GetBlock(assetLocation);
                var stackSize = FacingHelper.Count(entity.Connection);
                var itemStack = new ItemStack(block, stackSize);

                return new[] { itemStack };
            }

            return base.GetDrops(world, position, byPlayer, dropQuantityMultiplier);
        }

        private bool Break(IWorldAccessor world, BlockPos position, Vec3d hitPosition, IPlayer byPlayer)
        {
            if (world.BlockAccessor.GetBlockEntity(position) is BlockEntityCable entity)
            {
                var selectedFacing = Facing.None;

                if (_dotVariant?.SelectionBoxes is { } dotBoxes
                    && _partVariant?.SelectionBoxes is { } partBoxes
                    && _enabledSwitchVariant?.SelectionBoxes is { } enabledSwitchBoxes
                    && _disabledSwitchVariant?.SelectionBoxes is { } disabledSwitchBoxes)
                {
                    var key = new MeshDataKey(entity.Connection, entity.Switches, entity.SwitchesState);

                    foreach (var keyValuePair in CalculateBoxes(SelectionBoxesCache, dotBoxes, partBoxes,
                                 enabledSwitchBoxes, disabledSwitchBoxes, key))
                    {
                        var selectionFacing = keyValuePair.Key;
                        var selectionBoxes = keyValuePair.Value;

                        foreach (var selectionBox in selectionBoxes)
                            if (selectionBox.Clone().OmniGrowBy(0.005f)
                                .Contains(hitPosition.X, hitPosition.Y, hitPosition.Z))
                                selectedFacing |= selectionFacing;
                    }
                }

                var selectedSwitches = entity.Switches & selectedFacing;

                if (selectedSwitches != Facing.None)
                {
                    entity.Switches &= ~selectedFacing;
                    var stackSize = FacingHelper.Faces(selectedSwitches).Count();

                    if (stackSize > 0)
                    {
                        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            var assetLocation = new AssetLocation("electricity:switch-enabled");
                            var block = world.BlockAccessor.GetBlock(assetLocation);
                            var itemStack = new ItemStack(block, stackSize);
                            world.SpawnItemEntity(itemStack, position.ToVec3d());
                        }

                        return true;
                    }
                }

                var connection = entity.Connection & ~ selectedFacing;

                if (connection != Facing.None)
                {
                    var stackSize = FacingHelper.Count(selectedFacing);

                    if (stackSize > 0)
                    {
                        if (byPlayer.WorldData.CurrentGameMode != EnumGameMode.Creative)
                        {
                            var assetLocation = new AssetLocation("electricity:cable-part");
                            var block = world.BlockAccessor.GetBlock(assetLocation);
                            var itemStack = new ItemStack(block, stackSize);
                            world.SpawnItemEntity(itemStack, position.ToVec3d());
                        }

                        entity.Connection = connection;
                        return true;
                    }
                }
            }

            return false;
        }

        public override void OnBlockBroken(IWorldAccessor world, BlockPos pos, IPlayer byPlayer,
            float dropQuantityMultiplier = 1)
        {
            // if (api is ICoreClientAPI) return;

            if (byPlayer is { CurrentBlockSelection: { } blockSelection })
            {
                var hitPosition = blockSelection.HitPosition;

                if (Break(world, pos, hitPosition, byPlayer)) return;
            }

            base.OnBlockBroken(world, pos, byPlayer, dropQuantityMultiplier);
        }

        public override void OnNeighbourBlockChange(IWorldAccessor world, BlockPos pos, BlockPos neibpos)
        {
            base.OnNeighbourBlockChange(world, pos, neibpos);

            if (world.BlockAccessor.GetBlockEntity(pos) is BlockEntityCable entity)
            {
                var blockFacing = BlockFacing.FromVector(neibpos.X - pos.X, neibpos.Y - pos.Y, neibpos.Z - pos.Z);
                var selectedFacing = FacingHelper.FromFace(blockFacing);

                if ((entity.Connection & ~ selectedFacing) == Facing.None)
                {
                    world.BlockAccessor.BreakBlock(pos, null);
                    return;
                }

                var selectedSwitches = entity.Switches & selectedFacing;

                if (selectedSwitches != Facing.None)
                {
                    var stackSize = FacingHelper.Faces(selectedSwitches).Count();

                    if (stackSize > 0)
                    {
                        var assetLocation = new AssetLocation("electricity:switch-enabled");
                        var block = world.BlockAccessor.GetBlock(assetLocation);
                        var itemStack = new ItemStack(block, stackSize);
                        world.SpawnItemEntity(itemStack, pos.ToVec3d());
                    }

                    entity.Switches &= ~selectedFacing;
                }

                var selectedConnection = entity.Connection & selectedFacing;

                if (selectedConnection != Facing.None)
                {
                    var stackSize = FacingHelper.Count(selectedConnection);

                    if (stackSize > 0)
                    {
                        var assetLocation = new AssetLocation("electricity:cable-part");
                        var block = world.BlockAccessor.GetBlock(assetLocation);
                        var itemStack = new ItemStack(block, stackSize);
                        world.SpawnItemEntity(itemStack, pos.ToVec3d());

                        entity.Connection &= ~selectedConnection;
                    }
                }
            }
        }

        public override bool OnBlockInteractStart(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel)
        {
            if (api is ICoreClientAPI) return true;

            var hitPosition = blockSel.HitPosition;

            if (world.BlockAccessor.GetBlockEntity(blockSel.Position) is BlockEntityCable entity)
            {
                var selectedFacing = Facing.None;

                if (_dotVariant?.SelectionBoxes is { } dotBoxes
                    && _partVariant?.SelectionBoxes is { } partBoxes
                    && _enabledSwitchVariant?.SelectionBoxes is { } enabledSwitchBoxes
                    && _disabledSwitchVariant?.SelectionBoxes is { } disabledSwitchBoxes)
                {
                    var key = new MeshDataKey(entity.Connection, entity.Switches, entity.SwitchesState);

                    foreach (var keyValuePair in CalculateBoxes(SelectionBoxesCache, dotBoxes, partBoxes,
                                 enabledSwitchBoxes, disabledSwitchBoxes, key))
                    {
                        var selectionFacing = keyValuePair.Key;
                        var selectionBoxes = keyValuePair.Value;

                        foreach (var selectionBox in selectionBoxes)
                            if (selectionBox.Clone().OmniGrowBy(0.005f)
                                .Contains(hitPosition.X, hitPosition.Y, hitPosition.Z))
                                selectedFacing |= selectionFacing;
                    }
                }

                foreach (var face in FacingHelper.Faces(selectedFacing)) selectedFacing |= FacingHelper.FromFace(face);

                var selectedSwitches = selectedFacing & entity.Switches;

                if (selectedSwitches != 0)
                {
                    entity.SwitchesState ^= selectedSwitches;
                    return true;
                }
            }

            return base.OnBlockInteractStart(world, byPlayer, blockSel);
        }

        private static void AddBoxes(ref Dictionary<Facing, Cuboidf[]> cache, Facing key, Cuboidf[] boxes)
        {
            if (cache.ContainsKey(key))
                cache[key] = cache[key].Concat(boxes).ToArray();
            else
                cache[key] = boxes;
        }

        private static Dictionary<Facing, Cuboidf[]> CalculateBoxes(
            IDictionary<MeshDataKey, Dictionary<Facing, Cuboidf[]>> boxesCache, Cuboidf[] dotBoxes, Cuboidf[] partBoxes,
            Cuboidf[] enabledSwitchBoxes, Cuboidf[] disabledSwitchBoxes, MeshDataKey key)
        {
            if (!boxesCache.TryGetValue(key, out var boxes))
            {
                boxesCache[key] = boxes = new Dictionary<Facing, Cuboidf[]>();
                var origin = new Vec3d(0.5, 0.5, 0.5);

                if ((key.Connection & Facing.NorthAll) != 0)
                    boxes.Add(Facing.NorthAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin)).ToArray());
                if ((key.Connection & Facing.NorthEast) != 0)
                    boxes.Add(Facing.NorthEast,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 270.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.NorthWest) != 0)
                    boxes.Add(Facing.NorthWest,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.NorthUp) != 0)
                    boxes.Add(Facing.NorthUp,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.NorthDown) != 0)
                    boxes.Add(Facing.NorthDown,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 180.0f, 0.0f, origin))
                            .ToArray());

                if ((key.Connection & Facing.EastAll) != 0)
                    boxes.Add(Facing.EastAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin)).ToArray());
                if ((key.Connection & Facing.EastNorth) != 0)
                    boxes.Add(Facing.EastNorth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.EastSouth) != 0)
                    boxes.Add(Facing.EastSouth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 90.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.EastUp) != 0)
                    boxes.Add(Facing.EastUp,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 90.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.EastDown) != 0)
                    boxes.Add(Facing.EastDown,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 90.0f, origin))
                            .ToArray());

                if ((key.Connection & Facing.SouthAll) != 0)
                    boxes.Add(Facing.SouthAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.SouthEast) != 0)
                    boxes.Add(Facing.SouthEast,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 270.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.SouthWest) != 0)
                    boxes.Add(Facing.SouthWest,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 90.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.SouthUp) != 0)
                    boxes.Add(Facing.SouthUp,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 180.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.SouthDown) != 0)
                    boxes.Add(Facing.SouthDown,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 0.0f, origin))
                            .ToArray());

                if ((key.Connection & Facing.WestAll) != 0)
                    boxes.Add(Facing.WestAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.WestNorth) != 0)
                    boxes.Add(Facing.WestNorth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.WestSouth) != 0)
                    boxes.Add(Facing.WestSouth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 270.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.WestUp) != 0)
                    boxes.Add(Facing.WestUp,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 270.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.WestDown) != 0)
                    boxes.Add(Facing.WestDown,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 270.0f, origin))
                            .ToArray());

                if ((key.Connection & Facing.UpAll) != 0)
                    boxes.Add(Facing.UpAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.UpNorth) != 0)
                    boxes.Add(Facing.UpNorth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.UpEast) != 0)
                    boxes.Add(Facing.UpEast,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 180.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.UpSouth) != 0)
                    boxes.Add(Facing.UpSouth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 180.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.UpWest) != 0)
                    boxes.Add(Facing.UpWest,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 180.0f, origin))
                            .ToArray());

                if ((key.Connection & Facing.DownAll) != 0)
                    boxes.Add(Facing.DownAll,
                        dotBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray());
                if ((key.Connection & Facing.DownNorth) != 0)
                    boxes.Add(Facing.DownNorth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray());
                if ((key.Connection & Facing.DownEast) != 0)
                    boxes.Add(Facing.DownEast,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.DownSouth) != 0)
                    boxes.Add(Facing.DownSouth,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 0.0f, origin))
                            .ToArray());
                if ((key.Connection & Facing.DownWest) != 0)
                    boxes.Add(Facing.DownWest,
                        partBoxes.Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 0.0f, origin))
                            .ToArray());

                if ((key.Switches & Facing.NorthEast) != 0)
                    AddBoxes(ref boxes, Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.NorthWest) != 0)
                    AddBoxes(ref boxes, Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 270.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.NorthUp) != 0)
                    AddBoxes(ref boxes, Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 180.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.NorthDown) != 0)
                    AddBoxes(ref boxes, Facing.NorthAll,
                        ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 0.0f, origin)).ToArray());

                if ((key.Switches & Facing.EastNorth) != 0)
                    AddBoxes(ref boxes, Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 90.0f, origin)).ToArray());
                if ((key.Switches & Facing.EastSouth) != 0)
                    AddBoxes(ref boxes, Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 90.0f, origin)).ToArray());
                if ((key.Switches & Facing.EastUp) != 0)
                    AddBoxes(ref boxes, Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 90.0f, origin)).ToArray());
                if ((key.Switches & Facing.EastDown) != 0)
                    AddBoxes(ref boxes, Facing.EastAll,
                        ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 90.0f, origin)).ToArray());

                if ((key.Switches & Facing.SouthEast) != 0)
                    AddBoxes(ref boxes, Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 90.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.SouthWest) != 0)
                    AddBoxes(ref boxes, Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes).Select(selectionBox =>
                            selectionBox.RotatedCopy(90.0f, 270.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.SouthUp) != 0)
                    AddBoxes(ref boxes, Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes).Select(selectionBox =>
                            selectionBox.RotatedCopy(90.0f, 180.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.SouthDown) != 0)
                    AddBoxes(ref boxes, Facing.SouthAll,
                        ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 180.0f, origin)).ToArray());

                if ((key.Switches & Facing.WestNorth) != 0)
                    AddBoxes(ref boxes, Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(180.0f, 0.0f, 270.0f, origin)).ToArray());
                if ((key.Switches & Facing.WestSouth) != 0)
                    AddBoxes(ref boxes, Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 270.0f, origin)).ToArray());
                if ((key.Switches & Facing.WestUp) != 0)
                    AddBoxes(ref boxes, Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(270.0f, 0.0f, 270.0f, origin)).ToArray());
                if ((key.Switches & Facing.WestDown) != 0)
                    AddBoxes(ref boxes, Facing.WestAll,
                        ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(90.0f, 0.0f, 270.0f, origin)).ToArray());

                if ((key.Switches & Facing.UpNorth) != 0)
                    AddBoxes(ref boxes, Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.UpEast) != 0)
                    AddBoxes(ref boxes, Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.UpSouth) != 0)
                    AddBoxes(ref boxes, Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 180.0f, origin)).ToArray());
                if ((key.Switches & Facing.UpWest) != 0)
                    AddBoxes(ref boxes, Facing.UpAll,
                        ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 180.0f, origin)).ToArray());

                if ((key.Switches & Facing.DownNorth) != 0)
                    AddBoxes(ref boxes, Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 180.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.DownEast) != 0)
                    AddBoxes(ref boxes, Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 90.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.DownSouth) != 0)
                    AddBoxes(ref boxes, Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 0.0f, 0.0f, origin)).ToArray());
                if ((key.Switches & Facing.DownWest) != 0)
                    AddBoxes(ref boxes, Facing.DownAll,
                        ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                            ? enabledSwitchBoxes
                            : disabledSwitchBoxes)
                        .Select(selectionBox => selectionBox.RotatedCopy(0.0f, 270.0f, 0.0f, origin)).ToArray());
            }

            return boxes;
        }

        private static void AddMeshData(ref MeshData? sourceMesh, MeshData? meshData)
        {
            if (meshData != null)
            {
                if (sourceMesh != null)
                    sourceMesh.AddMeshData(meshData);
                else
                    sourceMesh = meshData;
            }
        }

        public override void OnJsonTesselation(ref MeshData sourceMesh, ref int[] lightRgbsByCorner, BlockPos position,
            Vintagestory.API.Common.Block[] chunkExtBlocks, int extIndex3d)
        {
            if (api.World.BlockAccessor.GetBlockEntity(position) is BlockEntityCable entity &&
                entity.Connection != Facing.None)
            {
                var key = new MeshDataKey(entity.Connection, entity.Switches, entity.SwitchesState);

                if (!MeshDataCache.TryGetValue(key, out var meshData))
                {
                    var origin = new Vec3f(0.5f, 0.5f, 0.5f);

                    if ((key.Connection & Facing.NorthAll) != 0)
                    {
                        AddMeshData(ref meshData,
                            _dotVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f, 0.0f));
                        if ((key.Connection & Facing.NorthEast) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                    270.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.NorthWest) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                    90.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.NorthUp) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                    0.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.NorthDown) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD, 0.0f));
                    }

                    if ((key.Connection & Facing.EastAll) != 0)
                    {
                        AddMeshData(ref meshData,
                            _dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 90.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.EastNorth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f,
                                    90.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.EastSouth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f,
                                    90.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.EastUp) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f,
                                    90.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.EastDown) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f,
                                    90.0f * GameMath.DEG2RAD));
                    }

                    if ((key.Connection & Facing.SouthAll) != 0)
                    {
                        AddMeshData(ref meshData,
                            _dotVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f, 0.0f));
                        if ((key.Connection & Facing.SouthEast) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                    270.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.SouthWest) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                    90.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.SouthUp) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.SouthDown) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                    0.0f * GameMath.DEG2RAD, 0.0f));
                    }

                    if ((key.Connection & Facing.WestAll) != 0)
                    {
                        AddMeshData(ref meshData,
                            _dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 270.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.WestNorth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD, 0.0f,
                                    270.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.WestSouth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD, 0.0f,
                                    270.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.WestUp) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD, 0.0f,
                                    270.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.WestDown) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD, 0.0f,
                                    270.0f * GameMath.DEG2RAD));
                    }

                    if ((key.Connection & Facing.UpAll) != 0)
                    {
                        AddMeshData(ref meshData,
                            _dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 180.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.UpNorth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.UpEast) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.UpSouth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD));
                        if ((key.Connection & Facing.UpWest) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD,
                                    180.0f * GameMath.DEG2RAD));
                    }

                    if ((key.Connection & Facing.DownAll) != 0)
                    {
                        AddMeshData(ref meshData, _dotVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f, 0.0f));
                        if ((key.Connection & Facing.DownNorth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.DownEast) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.DownSouth) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 0.0f));
                        if ((key.Connection & Facing.DownWest) != 0)
                            AddMeshData(ref meshData,
                                _partVariant?.MeshData?.Clone().Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 0.0f));
                    }

                    if ((key.Switches & Facing.NorthEast) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                90.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.NorthWest) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                270.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.NorthUp) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.NorthDown) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.NorthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                0.0f * GameMath.DEG2RAD, 0.0f));

                    if ((key.Switches & Facing.EastNorth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD,
                                0.0f, 90.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.EastSouth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD,
                                0.0f, 90.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.EastUp) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                0.0f, 90.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.EastDown) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.EastAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                0.0f, 90.0f * GameMath.DEG2RAD));

                    if ((key.Switches & Facing.SouthEast) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.SouthWest) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.SouthUp) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                180.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.SouthDown) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.SouthAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                0.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));

                    if ((key.Switches & Facing.WestNorth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 180.0f * GameMath.DEG2RAD,
                                0.0f, 270.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.WestSouth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f * GameMath.DEG2RAD,
                                0.0f, 270.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.WestUp) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 270.0f * GameMath.DEG2RAD,
                                0.0f, 270.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.WestDown) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.WestAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 90.0f * GameMath.DEG2RAD,
                                0.0f, 270.0f * GameMath.DEG2RAD));

                    if ((key.Switches & Facing.UpNorth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f,
                                180.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.UpEast) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f,
                                90.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.UpSouth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f,
                                0.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));
                    if ((key.Switches & Facing.UpWest) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.UpAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone().Rotate(origin, 0.0f,
                                270.0f * GameMath.DEG2RAD, 180.0f * GameMath.DEG2RAD));

                    if ((key.Switches & Facing.DownNorth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 180.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.DownEast) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 90.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.DownSouth) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 0.0f * GameMath.DEG2RAD, 0.0f));
                    if ((key.Switches & Facing.DownWest) != 0)
                        AddMeshData(ref meshData,
                            ((key.Switches & key.SwitchesState & Facing.DownAll) != 0
                                ? _enabledSwitchVariant
                                : _disabledSwitchVariant)?.MeshData?.Clone()
                            .Rotate(origin, 0.0f, 270.0f * GameMath.DEG2RAD, 0.0f));

                    MeshDataCache[key] = meshData;
                }

                sourceMesh = meshData ?? sourceMesh;
            }

            base.OnJsonTesselation(ref sourceMesh, ref lightRgbsByCorner, position, chunkExtBlocks, extIndex3d);
        }

        public override Cuboidf[] GetSelectionBoxes(IBlockAccessor blockAccessor, BlockPos position)
        {
            if (_dotVariant?.SelectionBoxes is { } dotBoxes
                && _partVariant?.SelectionBoxes is { } partBoxes
                && _enabledSwitchVariant?.SelectionBoxes is { } enabledSwitchBoxes
                && _disabledSwitchVariant?.SelectionBoxes is { } disabledSwitchBoxes
                && blockAccessor.GetBlockEntity(position) is BlockEntityCable entity)
            {
                var key = new MeshDataKey(entity.Connection, entity.Switches, entity.SwitchesState);
                return CalculateBoxes(SelectionBoxesCache, dotBoxes, partBoxes, enabledSwitchBoxes, disabledSwitchBoxes,
                        key).Values
                    .SelectMany(x => x)
                    .Distinct()
                    .ToArray();
            }

            return base.GetSelectionBoxes(blockAccessor, position);
        }

        public override Cuboidf[] GetCollisionBoxes(IBlockAccessor blockAccessor, BlockPos position)
        {
            if (_dotVariant?.CollisionBoxes is { } dotBoxes
                && _partVariant?.CollisionBoxes is { } partBoxes
                && _enabledSwitchVariant?.CollisionBoxes is { } enabledSwitchBoxes
                && _disabledSwitchVariant?.CollisionBoxes is { } disabledSwitchBoxes
                && blockAccessor.GetBlockEntity(position) is BlockEntityCable entity)
            {
                var key = new MeshDataKey(entity.Connection, entity.Switches, entity.SwitchesState);
                return CalculateBoxes(CollisionBoxesCache, dotBoxes, partBoxes, enabledSwitchBoxes, disabledSwitchBoxes,
                        key).Values
                    .SelectMany(x => x)
                    .Distinct()
                    .ToArray();
            }

            return base.GetSelectionBoxes(blockAccessor, position);
        }
    }
}