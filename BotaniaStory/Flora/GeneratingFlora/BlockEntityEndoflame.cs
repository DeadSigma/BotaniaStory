using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory.Flora.GeneratingFlora
{
    public class BlockEntityEndoflame : BlockEntity
    {
        public int CurrentMana = 0;
        public int MaxMana = 300; // У Эндофлейма маленький буфер
        public BlockPos LinkedSpreader = null;

        public int BurnTicksLeft = 0; // Сколько времени еще будет гореть уголь

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            if (api.Side == EnumAppSide.Server)
                RegisterGameTickListener(OnServerTick, 100); // 10 раз в секунду
            else
                RegisterGameTickListener(OnClientTick, 100); // Для частиц огня
        }

        private void OnServerTick(float dt)
        {
            bool dirty = false;

            // ==========================================
            // 1. ГЕНЕРАЦИЯ МАНЫ И ГОРЕНИЕ
            // ==========================================
            if (BurnTicksLeft > 0)
            {
                BurnTicksLeft--;
                dirty = true;

                if (CurrentMana < MaxMana)
                {
                    CurrentMana += 3; // 3 маны за 1 тик (30 маны в секунду)
                    if (CurrentMana > MaxMana) CurrentMana = MaxMana;
                }
            }
            // ==========================================
            // 2. ПОИСК ТОПЛИВА 
            // ==========================================
            // Начинаем искать, только если маны меньше половины (чтобы не жечь уголь впустую)
            else if (CurrentMana <= MaxMana / 2)
            {
                // Ищем выброшенные предметы в радиусе 3 блоков
                Entity[] entities = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 0.5, 0.5), 3, 3, (e) => e is EntityItem);
                foreach (Entity entity in entities)
                {
                    EntityItem entityItem = (EntityItem)entity;
                    ItemStack stack = entityItem.Itemstack;

                    // Проверяем, может ли этот предмет гореть (уголь, палки, доски)
                    if (stack?.Collectible?.CombustibleProps != null && stack.Collectible.CombustibleProps.BurnDuration > 0)
                    {
                        // Берем время горения из ванильной игры
                        float durationSec = stack.Collectible.CombustibleProps.BurnDuration;
                        BurnTicksLeft = (int)(durationSec * 10); // 10 тиков в секунду

                        // Съедаем ровно 1 предмет из стака
                        stack.StackSize--;
                        if (stack.StackSize <= 0) entityItem.Die(); // Если это был последний уголь - удаляем его с земли
                        else entityItem.WatchedAttributes.MarkAllDirty(); // Иначе обновляем количество на земле

                        dirty = true;
                        break; // За один раз едим только 1 предмет!
                    }
                }
            }

            // ==========================================
            // 3. ПРОВЕРКА СВЯЗИ
            // ==========================================
            if (LinkedSpreader != null)
            {
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (!(be is BlockEntityManaSpreader))
                {
                    LinkedSpreader = null;
                    dirty = true;
                }
            }

            if (LinkedSpreader == null)
            {
                FindSpreader();
                if (LinkedSpreader != null) dirty = true;
            }

            // ==========================================
            // 4. ПЕРЕДАЧА МАНЫ
            // ==========================================
            if (LinkedSpreader != null && CurrentMana > 0)
            {
                BlockEntity be = Api.World.BlockAccessor.GetBlockEntity(LinkedSpreader);
                if (be is BlockEntityManaSpreader spreader)
                {
                    int space = spreader.MaxMana - spreader.CurrentMana;
                    int toMove = Math.Min(CurrentMana, space);

                    if (toMove > 0)
                    {
                        spreader.CurrentMana += toMove;
                        spreader.MarkDirty(true);
                        this.CurrentMana -= toMove;
                        dirty = true;
                    }
                }
            }

            if (dirty) MarkDirty(true);
        }

        // ==========================================
        // КЛИЕНТ: ЧАСТИЦЫ ОГНЯ!
        // ==========================================
        private void OnClientTick(float dt)
        {
            if (BurnTicksLeft > 0 && Api.World.Rand.NextDouble() < 0.3) // Если горим - спавним огоньки
            {
                SimpleParticleProperties flame = new SimpleParticleProperties(
                    1, 2,
                    ColorUtil.ToRgba(255, 255, 120, 0), // Ярко-оранжевый цвет огня
                    new Vec3d(Pos.X + 0.3, Pos.Y + 0.1, Pos.Z + 0.3),
                    new Vec3d(Pos.X + 0.7, Pos.Y + 0.4, Pos.Z + 0.7),
                    new Vec3f(-0.2f, 0.5f, -0.2f),
                    new Vec3f(0.2f, 1.0f, 0.2f),
                    1f, 0f, 0.2f, 0.5f, EnumParticleModel.Quad
                );

                Api.World.SpawnParticles(flame);
            }
        }

        private void FindSpreader()
        {
            int radius = 6;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos checkPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(checkPos) is BlockEntityManaSpreader)
                        {
                            LinkedSpreader = checkPos;
                            return;
                        }
                    }
                }
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);
            tree.SetInt("burn", BurnTicksLeft);
            if (LinkedSpreader != null)
            {
                tree.SetInt("lx", LinkedSpreader.X);
                tree.SetInt("ly", LinkedSpreader.Y);
                tree.SetInt("lz", LinkedSpreader.Z);
                tree.SetBool("hasSpreader", true);
            }
            else
            {
                tree.SetBool("hasSpreader", false);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana");
            BurnTicksLeft = tree.GetInt("burn");
            if (tree.GetBool("hasSpreader"))
                LinkedSpreader = new BlockPos(tree.GetInt("lx"), tree.GetInt("ly"), tree.GetInt("lz"));
            else
                LinkedSpreader = null;
        }
    }
}