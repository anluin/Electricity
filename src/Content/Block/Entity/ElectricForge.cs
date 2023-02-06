using System;
using System.Collections.Generic;
using System.Text;
using Electricity.Utils;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

namespace Electricity.Content.Block.Entity {
    public class ElectricForge : BlockEntity, IHeatSource {
        private readonly Vec3d tmpPos = new Vec3d();
        private ILoadedSound? ambientSound;
        private bool burning;
        private bool clientSidePrevBurning;
        private double lastTickTotalHours;

        public int MaxTemp = 0;
        private ForgeContentsRenderer? renderer;
        private WeatherSystemBase? weatherSystem;


        public ItemStack? Contents { get; private set; }

        public bool IsBurning {
            get => this.burning;
            set {
                if (this.burning != value) {
                    if (value && !this.burning) {
                        this.renderer?.SetContents(this.Contents, 0, this.burning, false);
                        this.lastTickTotalHours = this.Api.World.Calendar.TotalHours;
                        this.MarkDirty();
                    }

                    this.burning = value;
                }
            }
        }

        private Behavior.Electricity? Electricity => this.GetBehavior<Behavior.Electricity>();

        public float GetHeatStrength(IWorldAccessor world, BlockPos heatSourcePos, BlockPos heatReceiverPos) {
            return this.burning
                ? 7
                : 0;
        }

        public override void Initialize(ICoreAPI api) {
            base.Initialize(api);

            this.Contents?.ResolveBlockOrItem(api.World);

            if (api is ICoreClientAPI clientApi) {
                clientApi.Event.RegisterRenderer(this.renderer = new ForgeContentsRenderer(this.Pos, clientApi), EnumRenderStage.Opaque, "forge");
                this.renderer.SetContents(this.Contents, 0, this.burning, true);

                this.RegisterGameTickListener(this.OnClientTick, 50);
            }

            this.weatherSystem = api.ModLoader.GetModSystem<WeatherSystemBase>();

            this.RegisterGameTickListener(this.OnCommonTick, 200);
        }

        private void OnClientTick(float dt) {
            if (this.Api?.Side == EnumAppSide.Client && this.clientSidePrevBurning != this.burning) {
                this.ToggleAmbientSounds(this.burning);
                this.clientSidePrevBurning = this.burning;
            }

            if (this.burning && this.Api?.World.Rand.NextDouble() < 0.13) {
                BlockEntityCoalPile.SpawnBurningCoalParticles(this.Api, this.Pos.ToVec3d().Add(4 / 16f, 14 / 16f, 4 / 16f), 8 / 16f, 8 / 16f);
            }

            this.renderer?.SetContents(this.Contents, 0, this.burning, false);
        }

        private void OnCommonTick(float dt) {
            if (this.burning) {
                var hoursPassed = this.Api.World.Calendar.TotalHours - this.lastTickTotalHours;

                if (this.Contents != null) {
                    var temp = this.Contents.Collectible.GetTemperature(this.Api.World, this.Contents);

                    if (temp < this.MaxTemp) {
                        var tempGain = (float)(hoursPassed * 1500);

                        this.Contents.Collectible.SetTemperature(this.Api.World, this.Contents, Math.Min(this.MaxTemp, temp + tempGain));
                        this.MarkDirty();
                    }
                }
            }

            this.tmpPos.Set(this.Pos.X + 0.5, this.Pos.Y + 0.5, this.Pos.Z + 0.5);

            double rainLevel = 0;
            var rainCheck = this.Api.Side == EnumAppSide.Server
                            && this.Api.World.Rand.NextDouble() < 0.15
                            && this.Api.World.BlockAccessor.GetRainMapHeightAt(this.Pos.X, this.Pos.Z) <= this.Pos.Y
                            && (rainLevel = this.weatherSystem!.GetPrecipitation(this.tmpPos)) > 0.1;

            if (rainCheck && this.Api.World.Rand.NextDouble() < rainLevel * 5) {
                var playSound = false;

                if (this.burning) {
                    playSound = true;

                    this.MarkDirty();
                }

                var temp = this.Contents == null
                    ? 0
                    : this.Contents.Collectible.GetTemperature(this.Api.World, this.Contents);

                if (temp > 20) {
                    playSound = temp > 100;
                    this.Contents?.Collectible.SetTemperature(this.Api.World, this.Contents, Math.Min(this.MaxTemp, temp - 8), false);
                    this.MarkDirty();
                }

                if (playSound) {
                    this.Api.World.PlaySoundAt(new AssetLocation("sounds/effect/extinguish"), this.Pos.X + 0.5, this.Pos.Y + 0.75, this.Pos.Z + 0.5, null, false, 16);
                }
            }

            this.lastTickTotalHours = this.Api.World.Calendar.TotalHours;
        }

        public void ToggleAmbientSounds(bool on) {
            if (this.Api.Side != EnumAppSide.Client) {
                return;
            }

            if (on) {
                if (!(this.ambientSound is { IsPlaying: true })) {
                    this.ambientSound = ((IClientWorldAccessor)this.Api.World).LoadSound(
                        new SoundParams {
                            Location = new AssetLocation("sounds/effect/embers.ogg"),
                            ShouldLoop = true,
                            Position = this.Pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                            DisposeOnFinish = false,
                            Volume = 1
                        }
                    );

                    this.ambientSound.Start();
                }
            }
            else {
                this.ambientSound?.Stop();
                this.ambientSound?.Dispose();
                this.ambientSound = null;
            }
        }

        internal bool OnPlayerInteract(IWorldAccessor world, IPlayer byPlayer, BlockSelection blockSel) {
            var slot = byPlayer.InventoryManager.ActiveHotbarSlot;

            if (!byPlayer.Entity.Controls.ShiftKey) {
                if (this.Contents == null) {
                    return false;
                }

                var split = this.Contents.Clone();
                split.StackSize = 1;
                this.Contents.StackSize--;

                if (this.Contents.StackSize == 0) {
                    this.Contents = null;
                }

                if (!byPlayer.InventoryManager.TryGiveItemstack(split)) {
                    world.SpawnItemEntity(split, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
                }

                this.renderer?.SetContents(this.Contents, 0, this.burning, true);
                this.MarkDirty();
                this.Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), this.Pos.X, this.Pos.Y, this.Pos.Z, byPlayer, false);

                return true;
            }

            if (slot.Itemstack == null) {
                return false;
            }

            var firstCodePart = slot.Itemstack.Collectible.FirstCodePart();
            var forgableGeneric = slot.Itemstack.Collectible.Attributes?.IsTrue("forgable") == true;

            // Add heatable item
            if (this.Contents == null && (firstCodePart == "ingot" || firstCodePart == "metalplate" || firstCodePart == "workitem" || forgableGeneric)) {
                this.Contents = slot.Itemstack.Clone();
                this.Contents.StackSize = 1;

                slot.TakeOut(1);
                slot.MarkDirty();

                this.renderer?.SetContents(this.Contents, 0, this.burning, true);
                this.MarkDirty();
                this.Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), this.Pos.X, this.Pos.Y, this.Pos.Z, byPlayer, false);

                return true;
            }

            // Merge heatable item
            if (!forgableGeneric && this.Contents != null && this.Contents.Equals(this.Api.World, slot.Itemstack, GlobalConstants.IgnoredStackAttributes) && this.Contents.StackSize < 4 &&
                this.Contents.StackSize < this.Contents.Collectible.MaxStackSize) {
                var myTemp = this.Contents.Collectible.GetTemperature(this.Api.World, this.Contents);
                var histemp = slot.Itemstack.Collectible.GetTemperature(this.Api.World, slot.Itemstack);

                this.Contents.Collectible.SetTemperature(world, this.Contents, ((myTemp * this.Contents.StackSize) + (histemp * 1)) / (this.Contents.StackSize + 1));
                this.Contents.StackSize++;

                slot.TakeOut(1);
                slot.MarkDirty();

                this.renderer?.SetContents(this.Contents, 0, this.burning, true);
                this.Api.World.PlaySoundAt(new AssetLocation("sounds/block/ingot"), this.Pos.X, this.Pos.Y, this.Pos.Z, byPlayer, false);

                this.MarkDirty();

                return true;
            }

            return false;
        }

        public override void OnBlockPlaced(ItemStack? byItemStack = null) {
            base.OnBlockPlaced(byItemStack);

            var electricity = this.Electricity;

            if (electricity != null) {
                electricity.Connection = Facing.DownAll;
            }
        }

        public override void OnBlockRemoved() {
            base.OnBlockRemoved();

            if (this.renderer != null) {
                this.renderer.Dispose();
                this.renderer = null;
            }

            this.ambientSound?.Dispose();
        }

        public override void OnBlockBroken(IPlayer? byPlayer = null) {
            base.OnBlockBroken(byPlayer);

            if (this.Contents != null) {
                this.Api.World.SpawnItemEntity(this.Contents, this.Pos.ToVec3d().Add(0.5, 0.5, 0.5));
            }

            this.ambientSound?.Dispose();
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving) {
            base.FromTreeAttributes(tree, worldForResolving);

            this.Contents = tree.GetItemstack("contents");
            this.burning = tree.GetInt("burning") > 0;
            this.lastTickTotalHours = tree.GetDouble("lastTickTotalHours");

            if (this.Api != null) {
                this.Contents?.ResolveBlockOrItem(this.Api.World);
            }

            this.renderer?.SetContents(this.Contents, 0, this.burning, true);
        }

        public override void ToTreeAttributes(ITreeAttribute tree) {
            base.ToTreeAttributes(tree);

            tree.SetItemstack("contents", this.Contents);
            tree.SetInt("burning", this.burning
                ? 1
                : 0);

            tree.SetDouble("lastTickTotalHours", this.lastTickTotalHours);
        }

        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder stringBuilder) {
            base.GetBlockInfo(forPlayer, stringBuilder);

            if (this.Contents != null) {
                var temp = (int)this.Contents.Collectible.GetTemperature(this.Api.World, this.Contents);

                stringBuilder.AppendLine(
                    temp <= 25
                        ? $"\nContents: {this.Contents.StackSize}x {this.Contents.GetName()}\nTemperature: {Lang.Get("Cold")}"
                        : $"\nContents: {this.Contents.StackSize}x {this.Contents.GetName()}\nTemperature: {temp}°C"
                );
            }
        }

        public override void OnLoadCollectibleMappings(IWorldAccessor worldForResolve, Dictionary<int, AssetLocation> oldBlockIdMapping, Dictionary<int, AssetLocation> oldItemIdMapping,
            int schematicSeed) {
            base.OnLoadCollectibleMappings(worldForResolve, oldBlockIdMapping, oldItemIdMapping, schematicSeed);

            if (this.Contents?.FixMapping(oldBlockIdMapping, oldItemIdMapping, worldForResolve) == false) {
                this.Contents = null;
            }
        }

        public override void OnStoreCollectibleMappings(Dictionary<int, AssetLocation> blockIdMapping, Dictionary<int, AssetLocation> itemIdMapping) {
            base.OnStoreCollectibleMappings(blockIdMapping, itemIdMapping);

            if (this.Contents != null) {
                if (this.Contents.Class == EnumItemClass.Item) {
                    blockIdMapping[this.Contents.Id] = this.Contents.Item.Code;
                }
                else {
                    itemIdMapping[this.Contents.Id] = this.Contents.Block.Code;
                }
            }
        }

        public override void OnBlockUnloaded() {
            base.OnBlockUnloaded();

            this.renderer?.Dispose();
        }
    }
}
