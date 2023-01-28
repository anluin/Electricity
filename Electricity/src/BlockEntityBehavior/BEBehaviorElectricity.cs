using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.BlockEntityBehavior
{
    public sealed class BEBehaviorElectricity : Vintagestory.API.Common.BlockEntityBehavior
    {
        private IElectricAccumulator? _accumulator;
        private Facing _connection = Facing.None;
        private IElectricConsumer? _consumer;
        private bool _dirty = true;
        private Facing _interruption = Facing.None;
        private IElectricProducer? _producer;

        public BEBehaviorElectricity(Vintagestory.API.Common.BlockEntity blockEntity) : base(blockEntity)
        {
        }

        public Facing Connection
        {
            get => _connection;
            set
            {
                if (_connection != value)
                {
                    _connection = value;
                    Update();
                }
            }
        }

        public Facing Interruption
        {
            get => _interruption;
            set
            {
                if (_interruption != value)
                {
                    _interruption = value;
                    Update();
                }
            }
        }

        private void Update()
        {
            _dirty = true;
            if (Api is { } api && api.ModLoader.GetModSystem<Electricity>() is { } electricity)
            {
                _dirty = false;

                _consumer = null;
                _producer = null;
                _accumulator = null;

                foreach (var entityBehavior in Blockentity.Behaviors)
                    switch (entityBehavior)
                    {
                        case IElectricConsumer { } consumer:
                            _consumer = consumer;
                            break;
                        case IElectricProducer { } producer:
                            _producer = producer;
                            break;
                        case IElectricAccumulator { } accumulator:
                            _accumulator = accumulator;
                            break;
                    }

                electricity.Update(Blockentity.Pos, _connection & ~_interruption);
                electricity.SetConsumer(Blockentity.Pos, _consumer);
                electricity.SetProducer(Blockentity.Pos, _producer);
                electricity.SetAccumulator(Blockentity.Pos, _accumulator);

                Blockentity.MarkDirty(true);
            }
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder)
        {
            base.GetBlockInfo(forPlayer, stringBuilder);
            var networkInformation = Api.ModLoader.GetModSystem<Electricity>().GetNetworks(Blockentity.Pos, Connection);

            stringBuilder.AppendLine("Electricity");
            // stringBuilder.AppendLine("├ Number of consumers: " + networkInformation.NumberOfConsumers);
            // stringBuilder.AppendLine("├ Number of producers: " + networkInformation.NumberOfProducers);
            // stringBuilder.AppendLine("├ Number of accumulators: " + networkInformation.NumberOfAccumulators);
            stringBuilder.AppendLine("├ Production: " + networkInformation.Production + "⚡   ");
            stringBuilder.AppendLine("├ Consumption: " + networkInformation.Consumption + "⚡   ");
            stringBuilder.AppendLine("└ Overflow: " + networkInformation.Overflow + "⚡   ");
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            if (_dirty) Update();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api is { } api) api.ModLoader.GetModSystem<Electricity>().Remove(Blockentity.Pos);
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBytes("electricity:connection", SerializerUtil.Serialize(_connection));
            tree.SetBytes("electricity:interruption", SerializerUtil.Serialize(_interruption));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve)
        {
            base.FromTreeAttributes(tree, worldAccessForResolve);
            _connection = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:connection"));
            _interruption = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:interruption"));
            Update();
        }
    }
}