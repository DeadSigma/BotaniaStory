using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools; // Добавили для работы с цветами и векторами

namespace BotaniaStory
{
    public class BlockEntityManaPool : BlockEntity
    {
        public int CurrentMana = 0;
        public int MaxMana = 1000000; // Вмещает миллион маны!

        // ==========================================
        // ИНИЦИАЛИЗАЦИЯ (Запускаем таймер частиц)
        // ==========================================
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Если это клиент (игрок), запускаем генератор частиц 10 раз в секунду (каждые 100 мс)
            if (api.Side == EnumAppSide.Client)
            {
                RegisterGameTickListener(SpawnManaParticles, 100);
            }
        }

        // Сохраняем ману
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetInt("mana", CurrentMana);
        }

        // Загружаем ману
        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            CurrentMana = tree.GetInt("mana", 0);

            if (Api?.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }

        // ==========================================
        // МАГИЯ ОТРИСОВКИ ЖИДКОСТИ
        // ==========================================
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            if (CurrentMana <= 0) return base.OnTesselation(mesher, tesselator);

            float fillRatio = (float)CurrentMana / MaxMana;
            float heightPixels = 2.01f + (fillRatio * 5.0f); //высота маны в пикселях  
            float height = heightPixels / 16f;

            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manapool_liquid.json");
            Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape != null)
            {
                MeshData liquidMesh;
                tesselator.TesselateShape(Block, shape, out liquidMesh);
                liquidMesh.Translate(0, height, 0);
                mesher.AddMeshData(liquidMesh);
            }

            return base.OnTesselation(mesher, tesselator);
        }

        // ==========================================
        // ГЕНЕРАЦИЯ ИСКР
        // ==========================================
        private void SpawnManaParticles(float dt)
        {
            if (CurrentMana <= 0) return;
            if (Api.World.Rand.NextDouble() > 0.3) return; // Шанс спавна 30% каждый тик

            // Считаем высоту маны, чтобы искры появлялись точно на её поверхности
            float fillRatio = (float)CurrentMana / MaxMana;
            float heightPixels = 2.01f + (fillRatio * 5.89f);
            float height = heightPixels / 16f;

            SimpleParticleProperties particles = new SimpleParticleProperties(
                1, 2,
                ColorUtil.ToRgba(255, 100, 255, 255), // Яркий бирюзовый цвет (альфа 255)
                new Vec3d(Pos.X + 0.15, Pos.Y + height, Pos.Z + 0.15), // МИН позиция (угол бассейна)
                new Vec3d(Pos.X + 0.85, Pos.Y + height + 0.05, Pos.Z + 0.85), // МАКС позиция (противоположный угол)!
                new Vec3f(-0.05f, 0.1f, -0.05f), // Минимальная скорость
                new Vec3f(0.1f, 0.2f, 0.1f), // Разброс скорости
                1.5f, // Время жизни
                -0.02f, // Гравитация (отрицательная = летят вверх)
                0.1f, 0.35f, // Размер
                EnumParticleModel.Quad
            );

            Api.World.SpawnParticles(particles);
        }
    }
}