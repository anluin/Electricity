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
    public sealed class BEBehaviorMotor : BEBehaviorMPBase, IElectricConsumer
    {
        private const float AccelerationFactor = 1.0f;


        private static CompositeShape? _compositeShape;
        private double _capableSpeed;

        private int _powerSetting;
        private float _resistance = 0.03f;

        public BEBehaviorMotor(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public override BlockFacing OutFacingForNetworkDiscovery
        {
            get
            {
                if (Blockentity is BlockEntityMotor entity && entity.Facing != Facing.None)
                    return FacingHelper.Directions(entity.Facing).First();

                return BlockFacing.NORTH;
            }
        }

        private float TargetSpeed => 0.01f * _powerSetting;
        private float TorqueFactor => 0.007f * _powerSetting;

        public override int[] AxisSign =>
            OutFacingForNetworkDiscovery.Index switch
            {
                0 => new[] { +0, +0, -1 },
                1 => new[] { -1, +0, +0 },
                2 => new[] { +0, +0, -1 },
                3 => new[] { -1, +0, +0 },
                4 => new[] { +0, +1, +0 },
                5 => new[] { +0, +1, +0 },
                _ => throw new Exception()
            };

        public ConsumptionRange ConsumptionRange => new ConsumptionRange(10, 100);

        public void Consume(int amount)
        {
            if (_powerSetting != amount)
            {
                _powerSetting = amount;
                Blockentity.MarkDirty(true);
            }
        }

        public override float GetResistance()
        {
            return _powerSetting != 0 ? FloatHelper.Remap(_powerSetting / 100.0f, 0.0f, 1.0f, 0.01f, 0.075f) : 0.25f;
        }

        public override float GetTorque(long tick, float speed, out float resistance)
        {
            _resistance = GetResistance();
            _capableSpeed += (TargetSpeed - _capableSpeed) * AccelerationFactor;
            var csFloat = (float)_capableSpeed;

            var dir = propagationDir == OutFacingForNetworkDiscovery ? 1f : -1f;
            var absSpeed = Math.Abs(speed);
            var excessSpeed = absSpeed - csFloat;
            var wrongDirection = dir * speed < 0f;

            resistance = wrongDirection
                ? _resistance * TorqueFactor * Math.Min(0.8f, absSpeed * 400f)
                : excessSpeed > 0
                    ? _resistance * Math.Min(0.2f, excessSpeed * excessSpeed * 80f)
                    : 0f;

            var power = wrongDirection
                ? csFloat
                : csFloat - absSpeed;

            return Math.Max(0f, power) * TorqueFactor * dir;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing)
        {
        }

        protected override CompositeShape? GetShape()
        {
            if (Api is { } api && Blockentity is BlockEntityMotor entity && entity.Facing != Facing.None)
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