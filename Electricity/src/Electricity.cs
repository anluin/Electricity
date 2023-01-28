using System;
using System.Collections.Generic;
using System.Linq;
using Electricity.Block;
using Electricity.BlockEntity;
using Electricity.BlockEntityBehavior;
using Electricity.Interface;
using Electricity.Utils;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

[assembly: ModDependency("game", "1.16.4")]
[assembly: ModInfo(
    "Electricity",
    "electricity",
    Website = "https://github.com/anluin/electricity",
    Description = "Brings electricity into the game!",
    Version = "0.0.1",
    Authors = new[] { "Anluin" }
)]

namespace Electricity
{
    internal class Network
    {
        public readonly HashSet<IElectricAccumulator> Accumulators = new HashSet<IElectricAccumulator>();
        public readonly HashSet<IElectricConsumer> Consumers = new HashSet<IElectricConsumer>();
        public readonly HashSet<BlockPos> PartPositions = new HashSet<BlockPos>();
        public readonly HashSet<IElectricProducer> Producers = new HashSet<IElectricProducer>();
        public int Consumption;
        public int Overflow;
        public int Production;
    }

    internal class NetworkPart
    {
        public readonly Network?[] Networks = { null, null, null, null, null, null };
        public readonly BlockPos Position;
        public IElectricAccumulator? Accumulator;
        public Facing Connection = Facing.None;
        public IElectricConsumer? Consumer;
        public IElectricProducer? Producer;

        public NetworkPart(BlockPos position)
        {
            Position = position;
        }
    }

    public class NetworkInformation
    {
        public int Consumption;
        public Facing Facing = Facing.None;
        public int NumberOfAccumulators;
        public int NumberOfBlocks;
        public int NumberOfConsumers;
        public int NumberOfProducers;
        public int Overflow;
        public int Production;
    }

    internal class Consumer
    {
        public readonly ConsumptionRange Consumption;
        public readonly IElectricConsumer ElectricConsumer;
        public int GivenEnergy;

        public Consumer(IElectricConsumer electricConsumer)
        {
            ElectricConsumer = electricConsumer;
            Consumption = electricConsumer.ConsumptionRange;
        }
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class Electricity : ModSystem
    {
        private readonly List<Consumer> _consumers = new List<Consumer>();
        private readonly HashSet<Network> _networks = new HashSet<Network>();
        private readonly Dictionary<BlockPos, NetworkPart> _parts = new Dictionary<BlockPos, NetworkPart>();
        private ICoreAPI? _api;

        public override void Start(ICoreAPI api)
        {
            base.Start(_api = api);

            api.RegisterBlockClass("Accumulator", typeof(BlockAccumulator));
            api.RegisterBlockEntityClass("Accumulator", typeof(BlockEntityAccumulator));
            api.RegisterBlockEntityBehaviorClass("Accumulator", typeof(BEBehaviorAccumulator));

            api.RegisterBlockClass("Cable", typeof(BlockCable));
            api.RegisterBlockEntityClass("Cable", typeof(BlockEntityCable));

            api.RegisterBlockClass("Motor", typeof(BlockMotor));
            api.RegisterBlockEntityClass("Motor", typeof(BlockEntityMotor));
            api.RegisterBlockEntityBehaviorClass("Motor", typeof(BEBehaviorMotor));

            api.RegisterBlockClass("Generator", typeof(BlockGenerator));
            api.RegisterBlockEntityClass("Generator", typeof(BlockEntityGenerator));
            api.RegisterBlockEntityBehaviorClass("Generator", typeof(BEBehaviorGenerator));

            api.RegisterBlockClass("Lamp", typeof(BlockLamp));
            api.RegisterBlockEntityClass("Lamp", typeof(BlockEntityLamp));
            api.RegisterBlockEntityBehaviorClass("Lamp", typeof(BEBehaviorLamp));

            api.RegisterBlockClass("Switch", typeof(BlockSwitch));

            api.RegisterBlockEntityBehaviorClass("Electricity", typeof(BEBehaviorElectricity));

            api.Event.RegisterGameTickListener(OnGameTick, 500);
        }

        private void OnGameTick(float _)
        {
            var accumulators = new List<IElectricAccumulator>();

            foreach (var network in _networks)
            {
                _consumers.Clear();

                var production = 0;

                foreach (var producer in network.Producers) production += producer.Produce();

                var totalRequiredEnergy = 0;

                foreach (var electricConsumer in network.Consumers)
                {
                    var consumer = new Consumer(electricConsumer);
                    totalRequiredEnergy += consumer.Consumption.Max;
                    _consumers.Add(consumer);
                }

                if (production < totalRequiredEnergy)
                    do
                    {
                        accumulators.Clear();

                        foreach (var accumulator in network.Accumulators)
                            if (accumulator.GetCapacity() > 0)
                                accumulators.Add(accumulator);

                        if (accumulators.Count > 0)
                        {
                            var rest = (totalRequiredEnergy - production) / accumulators.Count;

                            if (rest == 0) break;

                            foreach (var accumulator in accumulators)
                            {
                                var capacity = Math.Min(accumulator.GetCapacity(), rest);
                                if (capacity > 0)
                                {
                                    production += capacity;
                                    accumulator.Release(capacity);
                                }
                            }
                        }
                    } while (accumulators.Count > 0 && totalRequiredEnergy - production > 0);

                var availableEnergy = production;

                var activeConsumers = _consumers
                    .OrderBy(consumer => consumer.Consumption.Min)
                    .GroupBy(consumer => consumer.Consumption.Min)
                    .Where(grouping =>
                    {
                        var range = grouping.First().Consumption;
                        var totalMinConsumption = range.Min * grouping.Count();

                        if (totalMinConsumption <= availableEnergy)
                        {
                            availableEnergy -= totalMinConsumption;

                            foreach (var consumer in grouping) consumer.GivenEnergy += range.Min;

                            return true;
                        }

                        return false;
                    })
                    .SelectMany(grouping => grouping)
                    .ToArray();

                var requiredEnergy = int.MaxValue;

                while (availableEnergy > 0 && requiredEnergy != 0)
                {
                    requiredEnergy = 0;

                    var dissatisfiedConsumers = activeConsumers
                        .Where(consumer => consumer.Consumption.Max > consumer.GivenEnergy)
                        .ToArray();
                    var numberOfDissatisfiedConsumers = dissatisfiedConsumers.Count();

                    if (numberOfDissatisfiedConsumers == 0) break;

                    var distributableEnergy = Math.Max(1, availableEnergy / numberOfDissatisfiedConsumers);

                    foreach (var consumer in dissatisfiedConsumers)
                    {
                        if (availableEnergy == 0) break;

                        var giveableEnergy = Math.Min(distributableEnergy,
                            consumer.Consumption.Max - consumer.GivenEnergy);

                        availableEnergy -= giveableEnergy;
                        consumer.GivenEnergy += giveableEnergy;

                        requiredEnergy += consumer.Consumption.Max - consumer.GivenEnergy;
                    }
                }

                foreach (var consumer in _consumers) consumer.ElectricConsumer.Consume(consumer.GivenEnergy);

                network.Production = production;
                network.Consumption = production - availableEnergy;

                while ((network.Overflow = network.Production - network.Consumption) > 0)
                {
                    accumulators.Clear();

                    foreach (var accumulator in network.Accumulators)
                        if (accumulator.GetMaxCapacity() - accumulator.GetCapacity() > 0)
                            accumulators.Add(accumulator);

                    if (accumulators.Count == 0) break;

                    var giveableEnergy = network.Overflow / accumulators.Count;

                    if (giveableEnergy == 0) break;

                    foreach (var accumulator in accumulators)
                    {
                        var energy = Math.Min(giveableEnergy, accumulator.GetMaxCapacity() - accumulator.GetCapacity());

                        accumulator.Store(energy);
                        network.Consumption += energy;
                    }
                }
            }
        }

        private Network MergeNetworks(HashSet<Network> networks)
        {
            Network? outNetwork = null;

            foreach (var network in networks)
                if (outNetwork == null || outNetwork.PartPositions.Count < network.PartPositions.Count)
                    outNetwork = network;

            if (outNetwork != null)
                foreach (var network in networks)
                {
                    if (outNetwork == network) continue;

                    foreach (var position in network.PartPositions)
                    {
                        var part = _parts[position];

                        foreach (var face in BlockFacing.ALLFACES)
                            if (part.Networks[face.Index] == network)
                                part.Networks[face.Index] = outNetwork;

                        if (part.Consumer is { } consumer) outNetwork.Consumers.Add(consumer);

                        if (part.Producer is { } producer) outNetwork.Producers.Add(producer);

                        if (part.Accumulator is { } accumulator) outNetwork.Accumulators.Add(accumulator);

                        outNetwork.PartPositions.Add(position);
                    }

                    network.PartPositions.Clear();
                    _networks.Remove(network);
                }

            return outNetwork ?? CreateNetwork();
        }

        private void RemoveNetwork(ref Network network)
        {
            var partPositions = new BlockPos[network.PartPositions.Count];
            network.PartPositions.CopyTo(partPositions);
            _networks.Remove(network);

            foreach (var position in partPositions)
                if (_parts.TryGetValue(position, out var part))
                    foreach (var face in BlockFacing.ALLFACES)
                        if (part.Networks[face.Index] == network)
                            part.Networks[face.Index] = null;

            foreach (var position in partPositions)
                if (_parts.TryGetValue(position, out var part))
                    AddConnections(ref part, part.Connection);
        }

        private Network CreateNetwork()
        {
            var network = new Network();
            _networks.Add(network);

            return network;
        }

        private void AddConnections(ref NetworkPart part, Facing addedConnections)
        {
            if (addedConnections == Facing.None) return;

            var networksByFace = new[]
            {
                new HashSet<Network>(), new HashSet<Network>(), new HashSet<Network>(),
                new HashSet<Network>(), new HashSet<Network>(), new HashSet<Network>()
            };

            foreach (var face in FacingHelper.Faces(part.Connection))
                networksByFace[face.Index].Add(part.Networks[face.Index] ?? CreateNetwork());

            foreach (var direction in FacingHelper.Directions(addedConnections))
            {
                var directionFilter = FacingHelper.FromDirection(direction);
                var neighborPosition = part.Position.AddCopy(direction);

                if (_parts.TryGetValue(neighborPosition, out var neighborPart))
                    foreach (var face in FacingHelper.Faces(addedConnections & directionFilter))
                    {
                        if ((neighborPart.Connection & FacingHelper.From(face, direction.Opposite)) != 0)
                            if (neighborPart.Networks[face.Index] is { } network)
                                networksByFace[face.Index].Add(network);

                        if ((neighborPart.Connection & FacingHelper.From(direction.Opposite, face)) != 0)
                            if (neighborPart.Networks[direction.Opposite.Index] is { } network)
                                networksByFace[face.Index].Add(network);
                    }
            }

            foreach (var direction in FacingHelper.Directions(addedConnections))
            {
                var directionFilter = FacingHelper.FromDirection(direction);

                foreach (var face in FacingHelper.Faces(addedConnections & directionFilter))
                {
                    var neighborPosition = part.Position.AddCopy(direction).AddCopy(face);

                    if (_parts.TryGetValue(neighborPosition, out var neighborPart))
                    {
                        if ((neighborPart.Connection & FacingHelper.From(direction.Opposite, face.Opposite)) != 0)
                            if (neighborPart.Networks[direction.Opposite.Index] is { } network)
                                networksByFace[face.Index].Add(network);

                        if ((neighborPart.Connection & FacingHelper.From(face.Opposite, direction.Opposite)) != 0)
                            if (neighborPart.Networks[face.Opposite.Index] is { } network)
                                networksByFace[face.Index].Add(network);
                    }
                }
            }

            foreach (var face in FacingHelper.Faces(part.Connection))
            {
                var network = MergeNetworks(networksByFace[face.Index]);
                if (part.Consumer is { } consumer) network.Consumers.Add(consumer);
                if (part.Producer is { } producer) network.Producers.Add(producer);
                if (part.Accumulator is { } accumulator) network.Accumulators.Add(accumulator);
                network.PartPositions.Add(part.Position);
                part.Networks[face.Index] = network;
            }

            foreach (var direction in FacingHelper.Directions(part.Connection))
            {
                var directionFilter = FacingHelper.FromDirection(direction);

                foreach (var face in FacingHelper.Faces(part.Connection & directionFilter))
                    if ((part.Connection & FacingHelper.From(direction, face)) != 0)
                    {
                        if (part.Networks[face.Index] is { } network1 && part.Networks[direction.Index] is { } network2)
                        {
                            var networks = new HashSet<Network>();
                            networks.Add(network1);
                            networks.Add(network2);
                            MergeNetworks(networks);
                        }
                        else
                        {
                            throw new Exception();
                        }
                    }
            }
        }

        private void RemoveConnections(ref NetworkPart part, Facing removedConnections)
        {
            if (removedConnections == Facing.None) return;

            foreach (var blockFacing in FacingHelper.Faces(removedConnections))
                if (part.Networks[blockFacing.Index] is { } network)
                    RemoveNetwork(ref network);
        }

        public bool Update(BlockPos position, Facing facing)
        {
            if (!_parts.TryGetValue(position, out var part))
            {
                if (facing == Facing.None) return false;

                part = _parts[position] = new NetworkPart(position);
            }

            if (facing == part.Connection) return false;

            var addedConnections = ~part.Connection & facing;
            var removedConnections = part.Connection & ~facing;

            part.Connection = facing;

            // _api?.Logger.Debug("position: {0}; addedConnections: {1}; removedConnections: {2}",
            //     position.ToString(), addedConnections.ToString(), removedConnections.ToString());

            AddConnections(ref part, addedConnections);
            RemoveConnections(ref part, removedConnections);

            if (part.Connection == Facing.None) _parts.Remove(position);

            return true;
        }

        public void Remove(BlockPos position)
        {
            if (_parts.TryGetValue(position, out var part))
            {
                _parts.Remove(position);
                RemoveConnections(ref part, part.Connection);
            }
        }

        public void SetConsumer(BlockPos position, IElectricConsumer? consumer)
        {
            if (!_parts.TryGetValue(position, out var part))
            {
                if (consumer == null) return;
                part = _parts[position] = new NetworkPart(position);
            }

            if (part.Consumer != consumer)
            {
                foreach (var network in part.Networks)
                {
                    if (part.Consumer is { }) network?.Consumers.Remove(part.Consumer);
                    if (consumer is { }) network?.Consumers.Add(consumer);
                }

                part.Consumer = consumer;
            }
        }

        public void SetProducer(BlockPos position, IElectricProducer? producer)
        {
            if (!_parts.TryGetValue(position, out var part))
            {
                if (producer == null) return;
                part = _parts[position] = new NetworkPart(position);
            }

            if (part.Producer != producer)
            {
                foreach (var network in part.Networks)
                {
                    if (part.Producer is { }) network?.Producers.Remove(part.Producer);
                    if (producer is { }) network?.Producers.Add(producer);
                }

                part.Producer = producer;
            }
        }

        public void SetAccumulator(BlockPos position, IElectricAccumulator? accumulator)
        {
            if (!_parts.TryGetValue(position, out var part))
            {
                if (accumulator == null) return;
                part = _parts[position] = new NetworkPart(position);
            }

            if (part.Accumulator != accumulator)
            {
                foreach (var network in part.Networks)
                {
                    if (part.Accumulator is { }) network?.Accumulators.Remove(part.Accumulator);
                    if (accumulator is { }) network?.Accumulators.Add(accumulator);
                }

                part.Accumulator = accumulator;
            }
        }

        public NetworkInformation GetNetworks(BlockPos position, Facing facing)
        {
            var result = new NetworkInformation();

            if (_parts.TryGetValue(position, out var part))
                foreach (var blockFacing in FacingHelper.Faces(facing))
                    if (part.Networks[blockFacing.Index] is { } network)
                    {
                        result.Facing |= FacingHelper.FromFace(blockFacing);
                        result.NumberOfBlocks += network.PartPositions.Count;
                        result.NumberOfConsumers += network.Consumers.Count;
                        result.NumberOfProducers += network.Producers.Count;
                        result.NumberOfAccumulators += network.Accumulators.Count;
                        result.Production += network.Production;
                        result.Consumption += network.Consumption;
                        result.Overflow += network.Overflow;
                    }

            return result;
        }
    }
}