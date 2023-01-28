using Electricity.BlockEntityBehavior;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.BlockEntity
{
    public class BlockEntityCable : Vintagestory.API.Common.BlockEntity
    {
        private Facing _switches = Facing.None;

        private BEBehaviorElectricity Electricity
            => GetBehavior<BEBehaviorElectricity>();

        public Facing Connection
        {
            get => Electricity.Connection;
            set => Electricity.Connection = value;
        }

        public Facing Switches
        {
            get => _switches;
            set => Electricity.Interruption &= _switches = value;
        }

        public Facing SwitchesState
        {
            get => ~Electricity.Interruption;
            set => Electricity.Interruption = ~value;
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("electricity:switches", SerializerUtil.Serialize(_switches));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _switches = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:switches"));
        }
    }
}