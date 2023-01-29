using System;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.Content.Block.Entity {
    public class Lamp : BlockEntity {
        private Facing facing = Facing.None;

        private Behavior.Electricity Electricity {
            get => GetBehavior<Behavior.Electricity>();
        }

        private Behavior.Lamp Behavior {
            get => GetBehavior<Behavior.Lamp>();
        }

        public Facing Facing {
            get => this.facing;
            set {
                if (value != this.facing) {
                    this.Electricity.Connection =
                        FacingHelper.FullFace(this.facing = value);
                }
            }
        }

        public bool IsEnabled {
            get => this.Behavior.LightLevel > 0;
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetBytes("electricity:facing", SerializerUtil.Serialize(this.facing));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            try {
                this.facing = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:facing"));
            } catch (Exception exception) {
                this.facing = Facing.UpNorth;
                this.Api?.Logger.Error(exception.ToString());
            }
        }
    }
}
