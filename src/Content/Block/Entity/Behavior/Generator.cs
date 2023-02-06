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
    public sealed class Generator : BEBehaviorMPBase, IElectricProducer {
        private static CompositeShape? CompositeShape;

        private int powerSetting;

        public Generator(BlockEntity blockEntity) : base(blockEntity) { }

        public override BlockFacing OutFacingForNetworkDiscovery {
            get {
                if (this.Blockentity is Entity.Generator entity && entity.Facing != Facing.None) {
                    return FacingHelper.Directions(entity.Facing).First();
                }

                return BlockFacing.NORTH;
            }
        }

        public override int[] AxisSign => this.OutFacingForNetworkDiscovery.Index switch {
            0 => new[] { +0, +0, -1 },
            1 => new[] { -1, +0, +0 },
            2 => new[] { +0, +0, -1 },
            3 => new[] { -1, +0, +0 },
            4 => new[] { +0, +1, +0 },
            5 => new[] { +0, -1, +0 },
            _ => this.AxisSign
        };

        public int Produce() {
            var speed = GameMath.Clamp(Math.Abs(this.network?.Speed ?? 0.0f), 0.0f, 1.0f);
            var powerSetting = (int)(speed * 100.0f);

            if (powerSetting != this.powerSetting) {
                this.powerSetting = powerSetting;

                this.Blockentity.MarkDirty(true);
            }

            return (int)(speed * 100.0f);
        }

        public override void JoinNetwork(MechanicalNetwork network) {
            base.JoinNetwork(network);

            if (this.Api is ICoreServerAPI api && this.network is { }) {
                foreach (var block in this.network.nodes.Select(mechanicalPowerNode => api.World.BlockAccessor.GetBlockEntity(mechanicalPowerNode.Key))) {
                    if (block?.GetBehavior<Motor>() is { } motor) {
                        api.Event.EnqueueMainThreadTask(() => api.World.BlockAccessor.BreakBlock(motor.Position, null), "break-motor");
                    }
                }
            }
        }

        public override float GetResistance() {
            return this.powerSetting != 0
                ? FloatHelper.Remap(this.powerSetting / 100.0f, 0.0f, 1.0f, 0.01f, 0.075f)
                : 0.05f;
        }

        public override void WasPlaced(BlockFacing connectedOnFacing) { }

        protected override CompositeShape? GetShape() {
            if (this.Api is { } api && this.Blockentity is Entity.Generator entity && entity.Facing != Facing.None) {
                var direction = this.OutFacingForNetworkDiscovery;

                if (CompositeShape == null) {
                    var location = this.Block.CodeWithVariant("type", "rotor");

                    CompositeShape = api.World.BlockAccessor.GetBlock(location).Shape.Clone();
                }

                var shape = CompositeShape.Clone();

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
            stringBuilder.AppendLine("└ Production: " + this.powerSetting + "/" + 100 + "⚡   ");
            stringBuilder.AppendLine();
        }
    }
}
