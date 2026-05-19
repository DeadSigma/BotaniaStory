using BotaniaStory;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.Flora.GeneratingFlora
{
    // Наследуемся от нашего обновленного базового поведения
    public class BEBehaviorEndoflame : BEBehaviorGeneratingFlower
    {
        public int BurnTicksLeft = 0;

        // Обязательный конструктор
        public BEBehaviorEndoflame(BlockEntity blockentity) : base(blockentity)
        {
        }

        public override void Initialize(ICoreAPI api, JsonObject properties)
        {
            base.Initialize(api, properties);
            MaxMana = 300;

            // Регистрируем тики через this.Blockentity
            if (api.Side == EnumAppSide.Server)
                this.Blockentity.RegisterGameTickListener(OnServerTick, 100);
            else
                this.Blockentity.RegisterGameTickListener(OnClientTick, 100);
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            int manaPerCycle = 7;
            int ticksPerCycle = 2;

            // 1. ГЕНЕРАЦИЯ МАНЫ И ГОРЕНИЕ
            if (BurnTicksLeft > 0)
            {
                BurnTicksLeft--;
                dirty = true;

                if (CurrentMana < MaxMana)
                {
                    if (BurnTicksLeft % ticksPerCycle == 0)
                    {
                        CurrentMana += manaPerCycle;
                    }

                    if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                }
            }
            // 2. ПОИСК ТОПЛИВА 
            else if (CurrentMana <= MaxMana / 2)
            {
                Entity[] entities = this.Api.World.GetEntitiesAround(this.Blockentity.Pos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, (e) => e is EntityItem);
                foreach (Entity entity in entities)
                {
                    EntityItem entityItem = (EntityItem)entity;

                    if (!entityItem.Alive || entityItem.Itemstack == null || entityItem.Itemstack.StackSize <= 0)
                    {
                        continue;
                    }

                    ItemStack stack = entityItem.Itemstack;

                    if (stack.Collectible?.CombustibleProps != null && stack.Collectible.CombustibleProps.BurnDuration > 0)
                    {
                        float durationSec = stack.Collectible.CombustibleProps.BurnDuration;
                        BurnTicksLeft = (int)(durationSec * 10);

                        stack.StackSize--;
                        if (stack.StackSize <= 0)
                        {
                            entityItem.Die();
                        }
                        else
                        {
                            entityItem.WatchedAttributes.MarkAllDirty();
                        }

                        ICoreServerAPI sapi = this.Api as ICoreServerAPI;
                        if (sapi != null)
                        {
                            var channel = sapi.Network.GetChannel("botanianetwork");
                            channel.BroadcastPacket(new PlayManaSoundPacket()
                            {
                                Position = new Vec3d(this.Blockentity.Pos.X + 0.5, this.Blockentity.Pos.Y + 0.5, this.Blockentity.Pos.Z + 0.5),
                                SoundName = "ignite"
                            });
                        }

                        dirty = true;
                        break;
                    }
                }
            }

            // 3. ПРОВЕРКА И ПЕРЕДАЧА МАНЫ (вызов из базового поведения)
            ProcessManaTransfer(ref dirty);

            if (dirty) this.Blockentity.MarkDirty(true);
        }

        // КЛИЕНТ: ЧАСТИЦЫ ОГНЯ
        private void OnClientTick(float dt)
        {
            if (BurnTicksLeft > 0 && this.Api.World.Rand.NextDouble() < 0.05)
            {
                SimpleParticleProperties flame = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(255, 255, 140, 20),
                    new Vec3d(this.Blockentity.Pos.X + 0.35, this.Blockentity.Pos.Y + 0.1, this.Blockentity.Pos.Z + 0.35),
                    new Vec3d(this.Blockentity.Pos.X + 0.65, this.Blockentity.Pos.Y + 0.35, this.Blockentity.Pos.Z + 0.65),
                    new Vec3f(-0.1f, 0.4f, -0.1f),
                    new Vec3f(0.1f, 0.7f, 0.1f),
                    0.6f, 0f, 0.3f, 0.6f, EnumParticleModel.Quad
                );

                this.Api.World.SpawnParticles(flame);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("burn", BurnTicksLeft);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            BurnTicksLeft = tree.GetInt("burn");
        }
    }
}