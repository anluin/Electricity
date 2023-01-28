using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Utils
{
    internal class BlockVariant
    {
        public readonly Cuboidf[] CollisionBoxes;
        public readonly MeshData? MeshData;
        public readonly Cuboidf[] SelectionBoxes;

        public BlockVariant(ICoreAPI coreApi, CollectibleObject baseBlock, string variant)
        {
            var block = coreApi.World.GetBlock(baseBlock.CodeWithVariant("type", variant));

            CollisionBoxes = block.CollisionBoxes;
            SelectionBoxes = block.SelectionBoxes;

            if (coreApi is ICoreClientAPI clientApi)
            {
                var shape = clientApi.TesselatorManager.GetCachedShape(block.Shape.Base);
                clientApi.Tesselator.TesselateShape(baseBlock, shape, out MeshData);
            }
        }
    }
}