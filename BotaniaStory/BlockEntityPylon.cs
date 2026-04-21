using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityPylon : BlockEntity
    {
        private PylonRenderer modelRenderer;
        private PylonParticleRenderer particleRenderer;
        private EnumPylonType currentType;

        // Позиция портала/алтаря, к которому подключен пилон
        public BlockPos LinkedTarget = null;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api is ICoreClientAPI capi)
            {
                currentType = EnumPylonType.Mana;
                if (this.Block.Code.Path.Contains("natura")) currentType = EnumPylonType.Natura;
                else if (this.Block.Code.Path.Contains("gaia")) currentType = EnumPylonType.Gaia;

                modelRenderer = new PylonRenderer(capi, Pos, currentType);
                particleRenderer = new PylonParticleRenderer(capi);

                // Запускаем игровой тик каждые 110мс
                RegisterGameTickListener(SpawnParticlesTick, 110);
            }
        }
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            if (LinkedTarget != null) tree.SetBlockPos("linkedTarget", LinkedTarget);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            LinkedTarget = tree.GetBlockPos("linkedTarget");
        }
        private void SpawnParticlesTick(float dt)
        {
            var rand = Api.World.Rand;

            // Цвета точно как в TilePylon.java Botania
            Vec4f pylonColor = new Vec4f(0.5f, 0.8f, 1.0f, 1.0f);
            if (currentType == EnumPylonType.Natura) pylonColor = new Vec4f(0.5f, 1.0f, 0.5f, 1.0f); // Зеленый
            else if (currentType == EnumPylonType.Gaia) pylonColor = new Vec4f(1.0f, 0.5f, 1.0f, 1.0f); // Розовый

            // 1. ПАССИВНЫЕ ИСКРЫ (SparkleFX)
            // В оригинале: rand.nextBoolean()
            if (rand.NextDouble() > 0.5)
            {
                particleRenderer.Particles.Add(new PylonParticle()
                {
                    // В оригинале: xCoord + Math.random(), yCoord + Math.random() * 1.5, zCoord + Math.random()
                    Position = new Vec3d(Pos.X + rand.NextDouble(), Pos.Y + rand.NextDouble() * 1.5, Pos.Z + rand.NextDouble()),
                    Velocity = new Vec3d(0, 0.05, 0), // Чуть-чуть всплывают
                    Color = pylonColor,
                    Size = 0.1f + (float)rand.NextDouble() * 0.1f,
                    Life = 1.0f,
                    MaxLife = 1.0f,
                    TextureIndex = rand.Next(0, 4) // Случайная текстура искры (0, 1, 2, 3)
                });
            }

            // 2. АКТИВНЫЕ ЭФФЕКТЫ (Подключение к Альфхейму/Алтарю)
            if (LinkedTarget != null)
            {
                Vec3d targetCenter = new Vec3d(LinkedTarget.X + 0.5, LinkedTarget.Y + 0.75, LinkedTarget.Z + 0.5);

                if (currentType == EnumPylonType.Natura)
                {
                    // === 1. ЭФФЕКТ СВЯЗИ С ПОРТАЛОМ (Медленные и виляющие) ===
                    double linkTime = Api.World.ElapsedMilliseconds / 350.0;
                    float linkRadius = 0.8f;

                    // Точка спавна на окружности пилона
                    double linkStartX = Pos.X + 0.5 + Math.Cos(linkTime) * linkRadius;
                    double linkStartY = Pos.Y + 0.4;
                    double linkStartZ = Pos.Z + 0.5 + Math.Sin(linkTime) * linkRadius;

                    Vec3d linkStartPos = new Vec3d(linkStartX, linkStartY, linkStartZ);

                    // 1. ЗАМЕДЛЕНИЕ В 4 РАЗА: уменьшаем множитель с 2.5 до 0.6
                    // 2. ХАОТИЧНОСТЬ НАПРАВЛЕНИЯ: добавляем небольшой случайный разброс к вектору цели
                    Vec3d targetDir = targetCenter.Sub(linkStartPos).Normalize();
                    targetDir.X += (rand.NextDouble() - 0.5) * 0.15;
                    targetDir.Y += (rand.NextDouble() - 0.5) * 0.15;
                    targetDir.Z += (rand.NextDouble() - 0.5) * 0.15;

                    Vec3d linkMovement = targetDir.Normalize() * 0.6;

                    if (rand.NextDouble() < 0.4)
                    {
                        particleRenderer.Particles.Add(new PylonParticle()
                        {
                            Position = linkStartPos,
                            Velocity = linkMovement,
                            Color = new Vec4f(pylonColor.X, pylonColor.Y, pylonColor.Z, 0.9f),

                            Size = 0.45f + (float)rand.NextDouble() * 0.15f,

                            // 3. УВЕЛИЧИВАЕМ ЖИЗНЬ В 4 РАЗА: так как скорость упала, время должно вырасти
                            Life = 6.0f,
                            MaxLife = 6.0f,
                            TextureIndex = 4,
                            ShrinkOnDeath = false // Они не сдуваются в конце, а просто исчезают
                        });
                }

                    // === 2. ОРГАНИЧНАЯ СПИРАЛЬ ВОКРУГ ПИЛОНА ===
                    double spiralTime = Api.World.ElapsedMilliseconds / 600.0;
                    double spiralRadius = 0.7f;
                    double velY = 0.2;

                    // 1. Шанс 15% просто пропустить спавн частицы, чтобы порвать "пунктир"
                    if (rand.NextDouble() > 0.15)
                    {
                        // 2. Добавляем микро-сдвиг угла (jitter), чтобы частицы слегка гуляли влево-вправо
                        double jitterAngle = (rand.NextDouble() - 0.5) * 0.15;

                        double spawnX = Pos.X + 0.5 + Math.Cos(spiralTime + jitterAngle) * spiralRadius;

                        // 3. Небольшой разброс по стартовой высоте
                        double spawnY = Pos.Y + 0.05 + rand.NextDouble() * 0.1;

                        double spawnZ = Pos.Z + 0.5 + Math.Sin(spiralTime + jitterAngle) * spiralRadius;

                        particleRenderer.Particles.Add(new PylonParticle()
                        {
                            Position = new Vec3d(spawnX, spawnY, spawnZ),
                            Velocity = new Vec3d(0, velY, 0),
                            Color = new Vec4f(pylonColor.X, pylonColor.Y, pylonColor.Z, 0.9f),

                            // 4. УВЕЛИЧИЛИ РАЗМЕР: теперь от 0.45 до 0.60 (было 0.25)
                            Size = 0.45f + (float)rand.NextDouble() * 0.15f,

                            // 5. Разная продолжительность жизни, чтобы они не исчезали на одной идеальной линии
                            Life = 10.0f + (float)rand.NextDouble() * 1.5f,
                            MaxLife = 11.5f,
                            TextureIndex = 4
                        });
                    }
                }
            }
        }

        // === ТОТ САМЫЙ МЕТОД ===
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessellator)
        {
            // Возвращаем true, чтобы движок НЕ генерировал стандартную JSON-модель в самом мире.
            // При этом в инвентаре и в руках JSON-модель будет отображаться корректно!
            return true;
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            modelRenderer?.Dispose();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            modelRenderer?.Dispose();
        }
    }
}