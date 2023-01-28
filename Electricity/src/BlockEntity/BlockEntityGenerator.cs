using Electricity.BlockEntityBehavior;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.BlockEntity
{
    public class BlockEntityGenerator : Vintagestory.API.Common.BlockEntity
    {
        private Facing _facing = Facing.None;

        private BEBehaviorElectricity Electricity
            => GetBehavior<BEBehaviorElectricity>();

        public Facing Facing
        {
            get => _facing;
            set
            {
                if (_facing != value) Electricity.Connection = FacingHelper.FullFace(_facing = value);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("electricity:facing", SerializerUtil.Serialize(_facing));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _facing = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:facing"));
        }
    }
}