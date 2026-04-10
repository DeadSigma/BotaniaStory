using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityManaPool : BlockEntity
    {
        // Приватное хранилище маны
        private int _currentMana = 0;

        // Свойство: если бассейн творческий, всегда отдаем MaxMana и игнорируем изменения!
        public int CurrentMana
        {
            get { return isCreativePool ? MaxMana : _currentMana; }
            set { if (!isCreativePool) _currentMana = value; }
        }

        public int MaxMana = 1000000;

        private bool isDilutedPool = false;
        private bool isCreativePool = false; // Флаг для творческого бассейна

        // ==========================================
        // ИНИЦИАЛИЗАЦИЯ
        // ==========================================
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (Block != null && Block.Attributes != null)
            {
                isDilutedPool = Block.Attributes["isDilutedPool"].AsBool(false);
                isCreativePool = Block.Attributes["isCreativePool"].AsBool(false);
            }

            // Устанавливаем лимиты
            MaxMana = isDilutedPool ? 10000 : 1000000;

            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(SpawnManaParticles, 100);
            }
        }

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            // Сохраняем реальное значение, а не свойство, чтобы не перезаписать ничего лишнего
            tree.SetInt("mana", _currentMana);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            _currentMana = tree.GetInt("mana", 0);

            if (Api?.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }

        // ==========================================
        // УДАЛЕНИЕ БАССЕЙНА (ДРОП ИСКРЫ)
        // ==========================================
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api.Side == EnumAppSide.Server)
            {
                // Слегка увеличили радиус поиска (с 1.0 до 2.0), чтобы гарантированно зацепить хитбокс
                Entity[] sparks = Api.World.GetEntitiesAround(Pos.ToVec3d().Add(0.5, 1.2, 0.5), 0.2f, 0.5f, e => e is EntitySpark);

                foreach (Entity entity in sparks)
                {
                    if (entity is EntitySpark spark)
                    {
                        Item itemSpark = Api.World.GetItem(new AssetLocation("botaniastory", "spark"));
                        if (itemSpark != null)
                        {
                            ItemStack dropStack = new ItemStack(itemSpark);
                            Api.World.SpawnItemEntity(dropStack, spark.Pos.XYZ);
                        }

                        // ИСПРАВЛЕНИЕ: Используем 'PickedUp' вместо обычной смерти. 
                        // Это заставит сервер удалить искру мгновенно, как будто игрок положил ее в карман.
                        spark.Die(EnumDespawnReason.PickedUp);
                    }
                }
            }
        }

        // ==========================================
        // МАГИЯ ОТРИСОВКИ ЖИДКОСТИ
        // ==========================================
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // 1. СНАЧАЛА ГЕНЕРИРУЕМ И РИСУЕМ САМ БАССЕЙН
            MeshData baseMesh;
            tesselator.TesselateBlock(Block, out baseMesh);
            mesher.AddMeshData(baseMesh);

            // 2. ЕСЛИ ЕСТЬ МАНА, РИСУЕМ ЕЁ ПОВЕРХ
            if (CurrentMana > 0)
            {
                float fillRatio = (float)CurrentMana / MaxMana;

                float baseY = isDilutedPool ? 1.01f : 2.01f;
                float maxRise = isDilutedPool ? 4.0f : 5.0f;

                float heightPixels = baseY + (fillRatio * maxRise);
                float height = heightPixels / 16f;

                string shapeName = isDilutedPool ? "manapool_diluted_liquid.json" : "manapool_liquid.json";
                AssetLocation shapeLoc = new AssetLocation("botaniastory", $"shapes/block/{shapeName}");
                Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

                if (shape != null)
                {
                    MeshData liquidMesh;
                    tesselator.TesselateShape(Block, shape, out liquidMesh);
                    liquidMesh.Translate(0, height, 0);

                    // Создаем массив, если его нет
                    if (liquidMesh.CustomInts == null)
                    {
                        liquidMesh.CustomInts = new CustomMeshDataPartInt(liquidMesh.VerticesCount);
                        liquidMesh.CustomInts.Count = liquidMesh.VerticesCount;
                    }

                    // Задаем правильный проход рендера (Liquid)
                    int[] customInts = liquidMesh.CustomInts.Values;
                    for (int i = 0; i < liquidMesh.VerticesCount; i++)
                    {
                        // Используем ТОЛЬКО это число - оно проверено бочкой!
                        customInts[i] |= 805306368;
                    }

                    mesher.AddMeshData(liquidMesh);
                }
            }

            // 3. ВОЗВРАЩАЕМ TRUE
            // Это очень важно! Мы говорим игре, что полностью взяли рендер на себя.
            return true;
        }

        // ==========================================
        // ГЕНЕРАЦИЯ ИСКР
        // ==========================================
        private void SpawnManaParticles(float dt)
        {
            if (CurrentMana <= 0) return;
            if (Api.World.Rand.NextDouble() > 0.3) return;

            float fillRatio = (float)CurrentMana / MaxMana;

            float baseY = isDilutedPool ? 1.01f : 2.01f;
            float maxRise = isDilutedPool ? 4.89f : 5.89f; // +0.89f чтобы летели с поверхности
            float heightPixels = baseY + (fillRatio * maxRise);
            float height = heightPixels / 16f;

            // Для разбавленного бассейна разброс частиц должен быть шире (от 0.05 до 0.95)
            float minPos = isDilutedPool ? 0.05f : 0.15f;
            float maxPos = isDilutedPool ? 0.95f : 0.85f;

            SimpleParticleProperties particles = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(255, 100, 255, 255),
                new Vec3d(Pos.X + minPos, Pos.Y + height, Pos.Z + minPos),
                new Vec3d(Pos.X + maxPos, Pos.Y + height + 0.05, Pos.Z + maxPos),
                new Vec3f(-0.05f, 0.1f, -0.05f),
                new Vec3f(0.1f, 0.2f, 0.1f),
                1.5f,
                -0.02f,
                0.1f, 0.35f,
                EnumParticleModel.Quad
            );

            Api.World.SpawnParticles(particles);
        }
    }
}