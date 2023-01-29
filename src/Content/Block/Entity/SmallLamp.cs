using System;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.Content.Block.Entity {
    public class SmallLamp : BlockEntity {
        private Facing facing = Facing.None;

        private Behavior.Electricity Electricity {
            get => GetBehavior<Behavior.Electricity>();
        }

        private Behavior.SmallLamp Behavior {
            get => GetBehavior<Behavior.SmallLamp>();
        }

        public Facing Facing {
            get => this.facing;
            set {
                if (value != this.facing) {
                    this.Electricity.Connection = this.facing = value;
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
                this.Api?.Logger.Error(exception.ToString());
            }
        }
    }
}
