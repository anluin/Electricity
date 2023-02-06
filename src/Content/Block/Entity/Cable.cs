using System;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.Content.Block.Entity {
    public class Cable : BlockEntity {
        private Facing switches = Facing.None;

        private Behavior.Electricity Electricity => this.GetBehavior<Behavior.Electricity>();

        public Facing Connection {
            get => this.Electricity.Connection;
            set => this.Electricity.Connection = value;
        }

        public Facing Switches {
            get => this.switches;
            set => this.Electricity.Interruption &= this.switches = value;
        }

        public Facing SwitchesState {
            get => ~this.Electricity.Interruption;
            set => this.Electricity.Interruption = this.switches & ~value;
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetBytes("electricity:switches", SerializerUtil.Serialize(this.switches));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            try {
                this.switches = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:switches"));
            } catch (Exception exception) {
                this.Api?.Logger.Error(exception.ToString());
            }
        }
    }
}
