using botaniastory;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    // НАСЛЕДУЕМСЯ от BlockEntityGeneratingFlower
    public class BlockEntityEndoflame : BlockEntityGeneratingFlower
    {
        public int BurnTicksLeft = 0; 

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            MaxMana = 300; // Настраиваем максимум для Эндофлейма

            if (api.Side == EnumAppSide.Server)
                RegisterGameTickListener(OnServerTick, 100); 
            else
                RegisterGameTickListener(OnClientTick, 100); 
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            int manaPerCycle = 7;  // Сколько маны давать за одно срабатывание
            int ticksPerCycle = 2; // Раз в сколько тиков давать ману

            // 1. ГЕНЕРАЦИЯ МАНЫ И ГОРЕНИЕ
            if (BurnTicksLeft > 0)
            {
                BurnTicksLeft--;
                dirty = true;

                if (CurrentMana < MaxMana)
                {
                    // Проверяем, наступил ли нужный тик для выдачи маны
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
                Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, (e) => e is EntityItem);
                foreach (Entity entity in entities)
                {
                    EntityItem entityItem = (EntityItem)entity;

                    // === ЗАЩИТА ОТ ДЮПА ===
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
                            entityItem.Die(); // Убиваем сущность
                        }
                        else
                        {
                            entityItem.WatchedAttributes.MarkAllDirty();
                        }


                        LexiconConfig config = Api.LoadModConfig<LexiconConfig>("lexicon_client.json");
                        float volumeMultiplier = (config != null) ? (config.FlowerVolume / 100f) : 0.5f;

                        if (volumeMultiplier > 0f)
                        {
                            Api.World.PlaySoundAt(
                                new AssetLocation("botaniastory", "sounds/ignite"),
                                Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5,
                                null,
                                randomizePitch: true,
                                range: 16,
                                volume: 0.8f * volumeMultiplier // <--- ПРИМЕНЯЕМ ПОЛЗУНОК
                            );
                        }

                        dirty = true;
                        break; 
                    }
                }
            }

            // 3. ПРОВЕРКА И ПЕРЕДАЧА МАНЫ 
            ProcessManaTransfer(ref dirty);

            if (dirty) MarkDirty(true);
        }

        // КЛИЕНТ: ЧАСТИЦЫ ОГНЯ
        private void OnClientTick(float dt)
        {
            if (BurnTicksLeft > 0 && Api.World.Rand.NextDouble() < 0.05)
            {
                SimpleParticleProperties flame = new SimpleParticleProperties(
                    1, 1, ColorUtil.ToRgba(255, 255, 140, 20),
                    new Vec3d(Pos.X + 0.35, Pos.Y + 0.1, Pos.Z + 0.35),
                    new Vec3d(Pos.X + 0.65, Pos.Y + 0.35, Pos.Z + 0.65),
                    new Vec3f(-0.1f, 0.4f, -0.1f),
                    new Vec3f(0.1f, 0.7f, 0.1f),
                    0.6f, 0f, 0.3f, 0.6f, EnumParticleModel.Quad
                );

                Api.World.SpawnParticles(flame);
            }
        }

        //  ПЕРЕОПРЕДЕЛЯЕМ сохранение, чтобы добавить переменную горения, 
        // но при этом обязательно вызываем base.ToTreeAttributes(), чтобы родитель сохранил ману.
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