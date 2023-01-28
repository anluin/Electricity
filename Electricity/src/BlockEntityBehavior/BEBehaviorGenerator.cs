using System;
using System.Linq;
using Electricity.BlockEntity;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent.Mechanics;

namespace Electricity.BlockEntityBehavior
{
    public sealed class BEBehaviorGenerator : BEBehaviorMPBase, IElectricProducer
    {
        private static CompositeShape? _compositeShape;
        private int _powerSetting;

        public BEBehaviorGenerator(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public override BlockFacing OutFacingForNetworkDiscovery
        {
            get
            {
                if (Blockentity is BlockEntityGenerator entity && entity.Facing != Facing.None)
                    return FacingHelper.Directions(entity.Facing).First();

                return BlockFacing.NORTH;
            }
        }

        public override int[] AxisSign =>
            OutFacingForNetworkDiscovery.Index switch
            {
                0 => new[] { +0, +0, -1 },
                1 => new[] { -1, +0, +0 },
                2 => new[] { +0, +0, -1 },
                3 => new[] { -1, +0, +0 },
                4 => new[] { +0, +1, +0 },
                5 => new[] { +0, -1, +0 },
                _ => AxisSign
            };

        public int Produce()
        {
            var speed = GameMath.Clamp(Math.Abs(network?.Speed ?? 0.0f), 0.0f, 1.0f);
            var powerSetting = (int)(speed * 100.0f);

            if (powerSetting != _powerSetting)
            {
                _powerSetting = powerSetting;
                Blockentity.MarkDirty(true);
            }

            return (int)(speed * 100.0f);
        }

        public override float GetResistance()
        {
            return _powerSetting != 0 ? FloatHelper.Remap(_powerSetting / 100.0f, 0.0f, 1.0f, 0.01f, 0.075f) : 0.05f;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
        }

        protected override CompositeShape? GetShape()
        {
            if (Api is { } api && Blockentity is BlockEntityGenerator entity && entity.Facing != Facing.None)
            {
                var direction = OutFacingForNetworkDiscovery;

                if (_compositeShape == null)
                {
                    var location = Block.CodeWithVariant("type", "rotor");
                    _compositeShape = api.World.BlockAccessor.GetBlock(location).Shape.Clone();
                }

                var shape = _compositeShape.Clone();

                if (direction == BlockFacing.NORTH) shape.rotateY = 0;
                if (direction == BlockFacing.EAST) shape.rotateY = 270;
                if (direction == BlockFacing.SOUTH) shape.rotateY = 180;
                if (direction == BlockFacing.WEST) shape.rotateY = 90;
                if (direction == BlockFacing.UP) shape.rotateX = 90;
                if (direction == BlockFacing.DOWN) shape.rotateX = 270;

                return shape;
            }

            return null;
        }

        protected override void updateShape(IWorldAccessor worldForResolve)
        {
            Shape = GetShape();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return false;
        }
    }
}