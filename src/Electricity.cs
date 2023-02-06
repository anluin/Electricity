using System;
using System.Collections.Generic;
using System.Linq;
using Electricity.Content.Block;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

[assembly: ModDependency("game", "1.17.10")]
[assembly: ModInfo(
    "Electricity",
    "electricity",
    Website = "https://github.com/anluin/electricity",
    Description = "Brings electricity into the game!",
    Version = "0.0.7",
    Authors = new[] { "Anluin" }
)]

namespace Electricity {
    public class Electricity : ModSystem {
        private readonly List<Consumer> consumers = new List<Consumer>();
        private readonly HashSet<Network> networks = new HashSet<Network>();
        private readonly Dictionary<BlockPos, NetworkPart> parts = new Dictionary<BlockPos, NetworkPart>();

        public override void Start(ICoreAPI api) {
            base.Start(api);

            api.RegisterBlockClass("Cable", typeof(Cable));
            api.RegisterBlockEntityClass("Cable", typeof(Content.Block.Entity.Cable));

            api.RegisterBlockClass("Switch", typeof(Switch));

            api.RegisterBlockClass("ElectricForge", typeof(ElectricForge));
            api.RegisterBlockEntityClass("ElectricForge", typeof(Content.Block.Entity.ElectricForge));
            api.RegisterBlockEntityBehaviorClass("ElectricForge", typeof(Content.Block.Entity.Behavior.ElectricForge));

            api.RegisterBlockClass("Heater", typeof(Heater));
            api.RegisterBlockEntityClass("Heater", typeof(Content.Block.Entity.Heater));
            api.RegisterBlockEntityBehaviorClass("Heater", typeof(Content.Block.Entity.Behavior.Heater));

            api.RegisterBlockClass("Generator", typeof(Generator));
            api.RegisterBlockEntityClass("Generator", typeof(Content.Block.Entity.Generator));
            api.RegisterBlockEntityBehaviorClass("Generator", typeof(Content.Block.Entity.Behavior.Generator));

            api.RegisterBlockClass("Motor", typeof(Motor));
            api.RegisterBlockEntityClass("Motor", typeof(Content.Block.Entity.Motor));
            api.RegisterBlockEntityBehaviorClass("Motor", typeof(Content.Block.Entity.Behavior.Motor));

            api.RegisterBlockClass("Lamp", typeof(Lamp));
            api.RegisterBlockEntityClass("Lamp", typeof(Content.Block.Entity.Lamp));
            api.RegisterBlockEntityBehaviorClass("Lamp", typeof(Content.Block.Entity.Behavior.Lamp));

            api.RegisterBlockClass("SmallLamp", typeof(SmallLamp));
            api.RegisterBlockEntityClass("SmallLamp", typeof(Content.Block.Entity.SmallLamp));
            api.RegisterBlockEntityBehaviorClass("SmallLamp", typeof(Content.Block.Entity.Behavior.SmallLamp));

            api.RegisterBlockClass("Accumulator", typeof(Accumulator));
            api.RegisterBlockEntityClass("Accumulator", typeof(Content.Block.Entity.Accumulator));
            api.RegisterBlockEntityBehaviorClass("Accumulator", typeof(Content.Block.Entity.Behavior.Accumulator));

            api.RegisterBlockEntityBehaviorClass("Electricity", typeof(Content.Block.Entity.Behavior.Electricity));

            api.Event.RegisterGameTickListener(this.OnGameTick, 500);
        }

        public bool Update(BlockPos position, Facing facing) {
            if (!this.parts.TryGetValue(position, out var part)) {
                if (facing == Facing.None) {
                    return false;
                }

                part = this.parts[position] = new NetworkPart(position);
            }

            if (facing == part.Connection) {
                return false;
            }

            var addedConnections = ~part.Connection & facing;
            var removedConnections = part.Connection & ~facing;

            part.Connection = facing;

            this.AddConnections(ref part, addedConnections);
            this.RemoveConnections(ref part, removedConnections);

            if (part.Connection == Facing.None) {
                this.parts.Remove(position);
            }

            return true;
        }

        public void Remove(BlockPos position) {
            if (this.parts.TryGetValue(position, out var part)) {
                this.parts.Remove(position);
                this.RemoveConnections(ref part, part.Connection);
            }
        }

        private void OnGameTick(float _) {
            var accumulators = new List<IElectricAccumulator>();

            foreach (var network in this.networks) {
                this.consumers.Clear();

                var production = 0;

                foreach (var producer in network.Producers) {
                    production += producer.Produce();
                }

                var totalRequiredEnergy = 0;

                foreach (var electricConsumer in network.Consumers) {
                    var consumer = new Consumer(electricConsumer);
                    totalRequiredEnergy += consumer.Consumption.Max;
                    this.consumers.Add(consumer);
                }

                if (production < totalRequiredEnergy) {
                    do {
                        accumulators.Clear();

                        foreach (var accumulator in network.Accumulators) {
                            if (accumulator.GetCapacity() > 0) {
                                accumulators.Add(accumulator);
                            }
                        }

                        if (accumulators.Count > 0) {
                            var rest = (totalRequiredEnergy - production) / accumulators.Count;

                            if (rest == 0) {
                                break;
                            }

                            foreach (var accumulator in accumulators) {
                                var capacity = Math.Min(accumulator.GetCapacity(), rest);

                                if (capacity > 0) {
                                    production += capacity;
                                    accumulator.Release(capacity);
                                }
                            }
                        }
                    } while (accumulators.Count > 0 && totalRequiredEnergy - production > 0);
                }

                var availableEnergy = production;

                var activeConsumers = this.consumers
                    .OrderBy(consumer => consumer.Consumption.Min)
                    .GroupBy(consumer => consumer.Consumption.Min)
                    .Where(
                        grouping => {
                            var range = grouping.First().Consumption;
                            var totalMinConsumption = range.Min * grouping.Count();

                            if (totalMinConsumption <= availableEnergy) {
                                availableEnergy -= totalMinConsumption;

                                foreach (var consumer in grouping) {
                                    consumer.GivenEnergy += range.Min;
                                }

                                return true;
                            }

                            return false;
                        }
                    )
                    .SelectMany(grouping => grouping)
                    .ToArray();

                var requiredEnergy = int.MaxValue;

                while (availableEnergy > 0 && requiredEnergy != 0) {
                    requiredEnergy = 0;

                    var dissatisfiedConsumers = activeConsumers
                        .Where(consumer => consumer.Consumption.Max > consumer.GivenEnergy)
                        .ToArray();

                    var numberOfDissatisfiedConsumers = dissatisfiedConsumers.Count();

                    if (numberOfDissatisfiedConsumers == 0) {
                        break;
                    }

                    var distributableEnergy = Math.Max(1, availableEnergy / numberOfDissatisfiedConsumers);

                    foreach (var consumer in dissatisfiedConsumers) {
                        if (availableEnergy == 0) {
                            break;
                        }

                        var giveableEnergy = Math.Min(
                            distributableEnergy,
                            consumer.Consumption.Max - consumer.GivenEnergy
                        );

                        availableEnergy -= giveableEnergy;
                        consumer.GivenEnergy += giveableEnergy;

                        requiredEnergy += consumer.Consumption.Max - consumer.GivenEnergy;
                    }
                }

                foreach (var consumer in this.consumers) {
                    consumer.ElectricConsumer.Consume(consumer.GivenEnergy);
                }

                network.Production = production;
                network.Consumption = production - availableEnergy;

                while ((network.Overflow = network.Production - network.Consumption) > 0) {
                    accumulators.Clear();

                    foreach (var accumulator in network.Accumulators) {
                        if (accumulator.GetMaxCapacity() - accumulator.GetCapacity() > 0) {
                            accumulators.Add(accumulator);
                        }
                    }

                    if (accumulators.Count == 0) {
                        break;
                    }

                    var giveableEnergy = network.Overflow / accumulators.Count;

                    if (giveableEnergy == 0) {
                        break;
                    }

                    foreach (var accumulator in accumulators) {
                        var energy = Math.Min(giveableEnergy, accumulator.GetMaxCapacity() - accumulator.GetCapacity());

                        accumulator.Store(energy);
                        network.Consumption += energy;
                    }
                }
            }
        }

        private Network MergeNetworks(HashSet<Network> networks) {
            Network? outNetwork = null;

            foreach (var network in networks) {
                if (outNetwork == null || outNetwork.PartPositions.Count < network.PartPositions.Count) {
                    outNetwork = network;
                }
            }

            if (outNetwork != null) {
                foreach (var network in networks) {
                    if (outNetwork == network) {
                        continue;
                    }

                    foreach (var position in network.PartPositions) {
                        var part = this.parts[position];

                        foreach (var face in BlockFacing.ALLFACES) {
                            if (part.Networks[face.Index] == network) {
                                part.Networks[face.Index] = outNetwork;
                            }
                        }

                        if (part.Consumer is { } consumer) {
                            outNetwork.Consumers.Add(consumer);
                        }

                        if (part.Producer is { } producer) {
                            outNetwork.Producers.Add(producer);
                        }

                        if (part.Accumulator is { } accumulator) {
                            outNetwork.Accumulators.Add(accumulator);
                        }

                        outNetwork.PartPositions.Add(position);
                    }

                    network.PartPositions.Clear();
                    this.networks.Remove(network);
                }
            }

            return outNetwork ?? this.CreateNetwork();
        }

        private void RemoveNetwork(ref Network network) {
            var partPositions = new BlockPos[network.PartPositions.Count];
            network.PartPositions.CopyTo(partPositions);
            this.networks.Remove(network);

            foreach (var position in partPositions) {
                if (this.parts.TryGetValue(position, out var part)) {
                    foreach (var face in BlockFacing.ALLFACES) {
                        if (part.Networks[face.Index] == network) {
                            part.Networks[face.Index] = null;
                        }
                    }
                }
            }

            foreach (var position in partPositions) {
                if (this.parts.TryGetValue(position, out var part)) {
                    this.AddConnections(ref part, part.Connection);
                }
            }
        }

        private Network CreateNetwork() {
            var network = new Network();
            this.networks.Add(network);

            return network;
        }

        private void AddConnections(ref NetworkPart part, Facing addedConnections) {
            if (addedConnections == Facing.None) {
                return;
            }

            var networksByFace = new[] {
                new HashSet<Network>(), new HashSet<Network>(), new HashSet<Network>(),
                new HashSet<Network>(), new HashSet<Network>(), new HashSet<Network>()
            };

            foreach (var face in FacingHelper.Faces(part.Connection)) {
                networksByFace[face.Index].Add(part.Networks[face.Index] ?? this.CreateNetwork());
            }

            foreach (var direction in FacingHelper.Directions(addedConnections)) {
                var directionFilter = FacingHelper.FromDirection(direction);
                var neighborPosition = part.Position.AddCopy(direction);

                if (this.parts.TryGetValue(neighborPosition, out var neighborPart)) {
                    foreach (var face in FacingHelper.Faces(addedConnections & directionFilter)) {
                        if ((neighborPart.Connection & FacingHelper.From(face, direction.Opposite)) != 0) {
                            if (neighborPart.Networks[face.Index] is { } network) {
                                networksByFace[face.Index].Add(network);
                            }
                        }

                        if ((neighborPart.Connection & FacingHelper.From(direction.Opposite, face)) != 0) {
                            if (neighborPart.Networks[direction.Opposite.Index] is { } network) {
                                networksByFace[face.Index].Add(network);
                            }
                        }
                    }
                }
            }

            foreach (var direction in FacingHelper.Directions(addedConnections)) {
                var directionFilter = FacingHelper.FromDirection(direction);

                foreach (var face in FacingHelper.Faces(addedConnections & directionFilter)) {
                    var neighborPosition = part.Position.AddCopy(direction).AddCopy(face);

                    if (this.parts.TryGetValue(neighborPosition, out var neighborPart)) {
                        if ((neighborPart.Connection & FacingHelper.From(direction.Opposite, face.Opposite)) != 0) {
                            if (neighborPart.Networks[direction.Opposite.Index] is { } network) {
                                networksByFace[face.Index].Add(network);
                            }
                        }

                        if ((neighborPart.Connection & FacingHelper.From(face.Opposite, direction.Opposite)) != 0) {
                            if (neighborPart.Networks[face.Opposite.Index] is { } network) {
                                networksByFace[face.Index].Add(network);
                            }
                        }
                    }
                }
            }

            foreach (var face in FacingHelper.Faces(part.Connection)) {
                var network = this.MergeNetworks(networksByFace[face.Index]);

                if (part.Consumer is { } consumer) {
                    network.Consumers.Add(consumer);
                }

                if (part.Producer is { } producer) {
                    network.Producers.Add(producer);
                }

                if (part.Accumulator is { } accumulator) {
                    network.Accumulators.Add(accumulator);
                }

                network.PartPositions.Add(part.Position);
                part.Networks[face.Index] = network;
            }

            foreach (var direction in FacingHelper.Directions(part.Connection)) {
                var directionFilter = FacingHelper.FromDirection(direction);

                foreach (var face in FacingHelper.Faces(part.Connection & directionFilter)) {
                    if ((part.Connection & FacingHelper.From(direction, face)) != 0) {
                        if (part.Networks[face.Index] is { } network1 && part.Networks[direction.Index] is { } network2) {
                            var networks = new HashSet<Network> {
                                network1,
                                network2
                            };

                            this.MergeNetworks(networks);
                        }
                        else {
                            throw new Exception();
                        }
                    }
                }
            }
        }

        private void RemoveConnections(ref NetworkPart part, Facing removedConnections) {
            if (removedConnections == Facing.None) {
                return;
            }

            foreach (var blockFacing in FacingHelper.Faces(removedConnections)) {
                if (part.Networks[blockFacing.Index] is { } network) {
                    this.RemoveNetwork(ref network);
                }
            }
        }

        public void SetConsumer(BlockPos position, IElectricConsumer? consumer) {
            if (!this.parts.TryGetValue(position, out var part)) {
                if (consumer == null) {
                    return;
                }

                part = this.parts[position] = new NetworkPart(position);
            }

            if (part.Consumer != consumer) {
                foreach (var network in part.Networks) {
                    if (part.Consumer is { }) {
                        network?.Consumers.Remove(part.Consumer);
                    }

                    if (consumer is { }) {
                        network?.Consumers.Add(consumer);
                    }
                }

                part.Consumer = consumer;
            }
        }

        public void SetProducer(BlockPos position, IElectricProducer? producer) {
            if (!this.parts.TryGetValue(position, out var part)) {
                if (producer == null) {
                    return;
                }

                part = this.parts[position] = new NetworkPart(position);
            }

            if (part.Producer != producer) {
                foreach (var network in part.Networks) {
                    if (part.Producer is { }) {
                        network?.Producers.Remove(part.Producer);
                    }

                    if (producer is { }) {
                        network?.Producers.Add(producer);
                    }
                }

                part.Producer = producer;
            }
        }

        public void SetAccumulator(BlockPos position, IElectricAccumulator? accumulator) {
            if (!this.parts.TryGetValue(position, out var part)) {
                if (accumulator == null) {
                    return;
                }

                part = this.parts[position] = new NetworkPart(position);
            }

            if (part.Accumulator != accumulator) {
                foreach (var network in part.Networks) {
                    if (part.Accumulator is { }) {
                        network?.Accumulators.Remove(part.Accumulator);
                    }

                    if (accumulator is { }) {
                        network?.Accumulators.Add(accumulator);
                    }
                }

                part.Accumulator = accumulator;
            }
        }

        public NetworkInformation GetNetworks(BlockPos position, Facing facing) {
            var result = new NetworkInformation();

            if (this.parts.TryGetValue(position, out var part)) {
                var networks = new HashSet<Network>();

                foreach (var blockFacing in FacingHelper.Faces(facing)) {
                    if (part.Networks[blockFacing.Index] is { } network) {
                        networks.Add(network);
                        result.Facing |= FacingHelper.FromFace(blockFacing);
                    }
                }

                foreach (var network in networks) {
                    result.NumberOfBlocks += network.PartPositions.Count;
                    result.NumberOfConsumers += network.Consumers.Count;
                    result.NumberOfProducers += network.Producers.Count;
                    result.NumberOfAccumulators += network.Accumulators.Count;
                    result.Production += network.Production;
                    result.Consumption += network.Consumption;
                    result.Overflow += network.Overflow;
                }
            }

            return result;
        }
    }

    internal class Network {
        public readonly HashSet<IElectricAccumulator> Accumulators = new HashSet<IElectricAccumulator>();
        public readonly HashSet<IElectricConsumer> Consumers = new HashSet<IElectricConsumer>();
        public readonly HashSet<BlockPos> PartPositions = new HashSet<BlockPos>();
        public readonly HashSet<IElectricProducer> Producers = new HashSet<IElectricProducer>();

        public int Consumption;
        public int Overflow;
        public int Production;
    }

    internal class NetworkPart {
        public readonly Network?[] Networks = { null, null, null, null, null, null };
        public readonly BlockPos Position;
        public IElectricAccumulator? Accumulator;
        public Facing Connection = Facing.None;
        public IElectricConsumer? Consumer;
        public IElectricProducer? Producer;

        public NetworkPart(BlockPos position) {
            this.Position = position;
        }
    }

    public class NetworkInformation {
        public int Consumption;
        public Facing Facing = Facing.None;
        public int NumberOfAccumulators;
        public int NumberOfBlocks;
        public int NumberOfConsumers;
        public int NumberOfProducers;
        public int Overflow;
        public int Production;
    }

    internal class Consumer {
        public readonly ConsumptionRange Consumption;
        public readonly IElectricConsumer ElectricConsumer;
        public int GivenEnergy;

        public Consumer(IElectricConsumer electricConsumer) {
            this.ElectricConsumer = electricConsumer;
            this.Consumption = electricConsumer.ConsumptionRange;
        }
    }
}
