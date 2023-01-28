using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace Electricity.Utils {
    internal class BlockVariant {
        public readonly Cuboidf[] CollisionBoxes;
        public readonly MeshData? MeshData;
        public readonly Cuboidf[] SelectionBoxes;

        public BlockVariant(ICoreAPI api, CollectibleObject baseBlock, string variant) {
            var assetLocation = baseBlock.CodeWithVariant("type", variant);
            var block = api.World.GetBlock(assetLocation);

            this.CollisionBoxes = block.CollisionBoxes;
            this.SelectionBoxes = block.SelectionBoxes;

            if (api is ICoreClientAPI clientApi) {
                var cachedShape = clientApi.TesselatorManager.GetCachedShape(block.Shape.Base);

                clientApi.Tesselator.TesselateShape(baseBlock, cachedShape, out this.MeshData);
            }
        }
    }
}
