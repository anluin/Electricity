using System;
using System.Linq;
using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent.Mechanics;

namespace Electricity.Content.Block.Entity.Behavior {
    public class Motor : BEBehaviorMPBase, IElectricConsumer {
        private const float AccelerationFactor = 1.0f;
        private static CompositeShape? compositeShape;

        private double capableSpeed;
        private int powerSetting;
        private float resistance = 0.03f;

        public Motor(BlockEntity blockEntity) : base(blockEntity) {
        }

        public override BlockFacing OutFacingForNetworkDiscovery {
            get {
                if (this.Blockentity is Entity.Motor entity && entity.Facing != Facing.None) {
                    return FacingHelper.Directions(entity.Facing).First();
                }

                return BlockFacing.NORTH;
            }
        }

        private float TargetSpeed => 0.01f * this.powerSetting;

        private float TorqueFactor => 0.007f * this.powerSetting;

        public override int[] AxisSign => this.OutFacingForNetworkDiscovery.Index switch {
            0 => new[] {
                +0,
                +0,
                -1
            },
            1 => new[] {
                -1,
                +0,
                +0
            },
            2 => new[] {
                +0,
                +0,
                -1
            },
            3 => new[] {
                -1,
                +0,
                +0
            },
            4 => new[] {
                +0,
                +1,
                +0
            },
            5 => new[] {
                +0,
                +1,
                +0
            },
            _ => throw new Exception()
        };

        public ConsumptionRange ConsumptionRange => new(10, 100);

        public void Consume(int amount) {
            if (this.powerSetting != amount) {
                this.powerSetting = amount;
                this.Blockentity.MarkDirty(true);
            }
        }

        public override void JoinNetwork(MechanicalNetwork network) {
            base.JoinNetwork(network);

            if (this.Api is ICoreServerAPI api && this.network is not null && (
                    from mechanicalPowerNode in this.network.nodes
                    let block = api.World.BlockAccessor.GetBlockEntity(mechanicalPowerNode.Key)
                    where block?.GetBehavior<Generator>() is not null
                    select mechanicalPowerNode).Any()) {
                api.Event.EnqueueMainThreadTask(() => api.World.BlockAccessor.BreakBlock(this.Position, null), "break-motor");
            }
        }

        public override float GetResistance() {
            return this.powerSetting != 0
                ? FloatHelper.Remap(this.powerSetting / 100.0f, 0.0f, 1.0f, 0.01f, 0.075f)
                : 0.25f;
        }

        public override float GetTorque(long tick, float speed, out float resistance) {
            this.resistance = this.GetResistance();
            this.capableSpeed += (this.TargetSpeed - this.capableSpeed) * Motor.AccelerationFactor;
            var csFloat = (float)this.capableSpeed;

            var dir = this.propagationDir == this.OutFacingForNetworkDiscovery
                ? 1f
                : -1f;

            var absSpeed = Math.Abs(speed);
            var excessSpeed = absSpeed - csFloat;
            var wrongDirection = dir * speed < 0f;

            resistance = wrongDirection
                ? this.resistance * this.TorqueFactor * Math.Min(0.8f, absSpeed * 400f)
                : excessSpeed > 0
                    ? this.resistance * Math.Min(0.2f, excessSpeed * excessSpeed * 80f)
                    : 0f;

            var power = wrongDirection
                ? csFloat
                : csFloat - absSpeed;

            return Math.Max(0f, power) * this.TorqueFactor * dir;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing) {
        }

        protected override CompositeShape? GetShape() {
            if (this.Api is { } api && this.Blockentity is Entity.Motor entity && entity.Facing != Facing.None) {
                var direction = this.OutFacingForNetworkDiscovery;

                if (Motor.compositeShape == null) {
                    var location = this.Block.CodeWithVariant("type", "rotor");
                    Motor.compositeShape = api.World.BlockAccessor.GetBlock(location).Shape.Clone();
                }

                var shape = Motor.compositeShape.Clone();

                if (direction == BlockFacing.NORTH) {
                    shape.rotateY = 0;
                }

                if (direction == BlockFacing.EAST) {
                    shape.rotateY = 270;
                }

                if (direction == BlockFacing.SOUTH) {
                    shape.rotateY = 180;
                }

                if (direction == BlockFacing.WEST) {
                    shape.rotateY = 90;
                }

                if (direction == BlockFacing.UP) {
                    shape.rotateX = 90;
                }

                if (direction == BlockFacing.DOWN) {
                    shape.rotateX = 270;
                }

                return shape;
            }

            return null;
        }

        protected override void updateShape(IWorldAccessor worldForResolve) {
            this.Shape = this.GetShape();
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator) {
            return false;
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            stringBuilder.AppendLine(StringHelper.Progressbar(this.powerSetting));
            stringBuilder.AppendLine("└ Consumption: " + this.powerSetting + "/" + 100 + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
