using System.Text;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.Util;

namespace Electricity.Content.Block.Entity.Behavior {
    public class Electricity : BlockEntityBehavior {
        private IElectricAccumulator? accumulator;

        private Facing connection;
        private IElectricConsumer? consumer;
        private bool dirty = true;
        private Facing interruption;
        private IElectricProducer? producer;

        public Electricity(BlockEntity blockEntity)
            : base(blockEntity) {
        }

        public global::Electricity.Electricity? System => this.Api?.ModLoader.GetModSystem<global::Electricity.Electricity>();

        public Facing Connection {
            get => this.connection;
            set {
                if (this.connection != value) {
                    this.connection = value;
                    this.dirty = true;
                    this.Update();
                }
            }
        }

        public Facing Interruption {
            get => this.interruption;
            set {
                if (this.interruption != value) {
                    this.interruption = value;
                    this.dirty = true;
                    this.Update();
                }
            }
        }

        public override void Initialize(ICoreAPI api, JsonObject properties) {
            base.Initialize(api, properties);
            this.Update();
        }

        public void Update(bool force = false) {
            if (this.dirty || force) {
                var system = this.System;

                if (system is { }) {
                    this.dirty = false;


                    this.consumer = null;
                    this.producer = null;
                    this.accumulator = null;

                    foreach (var entityBehavior in this.Blockentity.Behaviors) {
                        switch (entityBehavior) {
                            case IElectricConsumer { } consumer:
                                this.consumer = consumer;

                                break;
                            case IElectricProducer { } producer:
                                this.producer = producer;

                                break;
                            case IElectricAccumulator { } accumulator:
                                this.accumulator = accumulator;

                                break;
                        }
                    }

                    system.SetConsumer(this.Blockentity.Pos, this.consumer);
                    system.SetProducer(this.Blockentity.Pos, this.producer);
                    system.SetAccumulator(this.Blockentity.Pos, this.accumulator);

                    if (system.Update(this.Blockentity.Pos, this.connection & ~this.interruption)) {
                        this.Blockentity.MarkDirty(true);
                    }
                }
                else {
                    this.dirty = true;
                }
            }
        }

        public override void OnBlockRemoved() {
            base.OnBlockRemoved();
            this.System?.Remove(this.Blockentity.Pos);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);
            var networkInformation = this.System?.GetNetworks(this.Blockentity.Pos, this.Connection);

            stringBuilder
                .AppendLine("Electricity")
                // .AppendLine("├ Number of consumers: " + networkInformation?.NumberOfConsumers)
                // .AppendLine("├ Number of producers: " + networkInformation?.NumberOfProducers)
                // .AppendLine("├ Number of accumulators: " + networkInformation?.NumberOfAccumulators)
                // .AppendLine("├ Block: " + networkInformation?.NumberOfBlocks)
                .AppendLine("├ Production: " + networkInformation?.Production + "⚡   ")
                .AppendLine("├ Consumption: " + networkInformation?.Consumption + "⚡   ")
                .AppendLine("└ Overflow: " + networkInformation?.Overflow + "⚡   ");
        }


        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetBytes("electricity:connection", SerializerUtil.Serialize(this.connection));
            tree.SetBytes("electricity:interruption", SerializerUtil.Serialize(this.interruption));
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldAccessForResolve) {
            base.FromTreeAttributes(tree, worldAccessForResolve);

            var connection = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:connection"));
            var interruption = SerializerUtil.Deserialize<Facing>(tree.GetBytes("electricity:interruption"));

            if (connection != this.connection || interruption != this.interruption) {
                this.interruption = interruption;
                this.connection = connection;
                this.dirty = true;
                this.Update();
            }
        }
    }
}
