using BotaniaStory.client.particles;
using BotaniaStory.client.renderers;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.blockentity
{
    public class BlockEntityPylon : BlockEntity
    {
        private PylonRenderer modelRenderer;
        private PylonParticleRenderer particleRenderer;
        public EnumPylonType CurrentType;

        // Позиция портала/алтаря, к которому подключен пилон
        public BlockPos LinkedTarget = null;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Определяем тип пилона для ОБЕИХ сторон (клиента и сервера)
            CurrentType = EnumPylonType.Mana;
            if (this.Block.Code.Path.Contains("natura")) CurrentType = EnumPylonType.Natura;
            else if (this.Block.Code.Path.Contains("gaia")) CurrentType = EnumPylonType.Gaia;

            if (api is ICoreClientAPI capi)
            {
                modelRenderer = new PylonRenderer(capi, Pos, CurrentType);

                // рендер частиц один на весь мод, свой на каждый пилон течет и убивает общие текстуры
                particleRenderer = PylonParticleSystem.Renderer;

                // Клиентский тик для частиц
                RegisterGameTickListener(SpawnParticlesTick, 110);
            }
            else if (api is ICoreServerAPI sapi)
            {
                // Запускаем серверный тик каждые 50мс (1 игровой тик)
                RegisterGameTickListener(ServerTick, 50);
            }
        }

        private void ServerTick(float dt)
        {
            // Тратят ману только природные пилоны, которые подключены к ядру
            if (CurrentType != EnumPylonType.Natura || LinkedTarget == null) return;

            bool hasMana = false;

            // Проверяем блок ровно под пилоном
            BlockPos poolPos = Pos.DownCopy();
            if (Api.World.BlockAccessor.GetBlockEntity(poolPos) is BlockEntityManaPool pool)
            {
                // Пытаемся забрать 1 ману за тик (пассивный режим)
                if (pool.ConsumeMana(1))
                {
                    hasMana = true;
                }
            }

            // Если бассейна нет или мана закончилась - принудительно гасим портал
            if (!hasMana)
            {
                TurnOffGateway();
            }
        }

        private void TurnOffGateway()
        {
            // деактивация портала это серверное дело
            if (Api.Side == EnumAppSide.Client) return;

            if (LinkedTarget != null && Api.World.BlockAccessor.GetBlockEntity(LinkedTarget) is BlockEntityElvenGatewayCore core)
            {
                // Ядро само позаботится об отвязке всех пилонов
                core.Deactivate();
            }
            LinkedTarget = null;
            MarkDirty(false);
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
            if (particleRenderer == null) return;

            var rand = Api.World.Rand;

            Vec4f pylonColor = new Vec4f(0.5f, 0.8f, 1.0f, 1.0f);
            if (CurrentType == EnumPylonType.Natura) pylonColor = new Vec4f(0.5f, 1.0f, 0.5f, 1.0f);
            else if (CurrentType == EnumPylonType.Gaia) pylonColor = new Vec4f(1.0f, 0.5f, 1.0f, 1.0f);

            // 1. ПАССИВНЫЕ ИСКРЫ - вылетают из пилона вбок и извиваются, потом гаснут (с 70% пути).
            if (rand.NextDouble() > 0.5)
            {
                // направление: в основном вбок + немного вверх
                double ang = rand.NextDouble() * GameMath.TWOPI;
                double up = 0.15 + rand.NextDouble() * 0.6;
                double horiz = Math.Sqrt(Math.Max(0.0, 1.0 - up * up));
                double dx = Math.Cos(ang) * horiz;
                double dy = up;
                double dz = Math.Sin(ang) * horiz;

                float speed = 2.6f + (float)rand.NextDouble() * 1.0f;   // разлёт; distance регулируется Drag'ом

                // случайная нормализованная ось закрутки
                double ax = rand.NextDouble() * 2 - 1;
                double ay = rand.NextDouble() * 2 - 1;
                double az = rand.NextDouble() * 2 - 1;
                double al = Math.Sqrt(ax * ax + ay * ay + az * az);
                if (al < 1e-6) { ax = 0; ay = 1; az = 0; al = 1; }

                float life = 0.5f + (float)rand.NextDouble() * 0.25f;

                particleRenderer.Particles.Add(new PylonParticle()
                {
                    Position = new Vec3d(Pos.X + 0.5 + (rand.NextDouble() - 0.5) * 0.15,
                                         Pos.Y + 0.2 + rand.NextDouble() * 1.0,   // сходят со всей высоты пилона
                                         Pos.Z + 0.5 + (rand.NextDouble() - 0.5) * 0.15),
                    Velocity = new Vec3d(dx * speed, dy * speed, dz * speed),
                    Color = pylonColor,
                    Size = 0.1f + (float)rand.NextDouble() * 0.08f,
                    Life = life,
                    MaxLife = life,
                    TextureIndex = rand.Next(0, 4),
                    ShrinkOnDeath = true,
                    Drag = 4.0f,     // "выстрел и стоп". МЕНЬШЕ = летят ДАЛЬШЕ
                    Gravity = 1.0f,  // слегка провисают в конце
                    SwirlAxis = new Vec3d(ax / al, ay / al, az / al),
                    SwirlStrength = (rand.Next(2) == 0 ? -1f : 1f) * (4f + (float)rand.NextDouble() * 5f), // сила извивания
                    WobbleFreq = 5f + (float)rand.NextDouble() * 7f,
                    WobblePhase = (float)(rand.NextDouble() * GameMath.TWOPI),
                    FadeIn = 0.04f,
                    FadeStart = 0.7f
                });
            }

            // 2. АКТИВНЫЕ ЭФФЕКТЫ
            if (LinkedTarget != null)
            {
                Vec3d targetCenter = new Vec3d(LinkedTarget.X + 0.5, LinkedTarget.Y + 0.75, LinkedTarget.Z + 0.5);

                if (CurrentType == EnumPylonType.Natura)
                {
                    // 2a. ПОТОК К ЯДРУ - конец задает Target, а не время жизни
                    double linkTime = Api.World.ElapsedMilliseconds / 350.0;
                    float linkRadius = 0.8f;

                    Vec3d linkStartPos = new Vec3d(
                        Pos.X + 0.5 + Math.Cos(linkTime) * linkRadius,
                        Pos.Y + 0.4,
                        Pos.Z + 0.5 + Math.Sin(linkTime) * linkRadius);

                    // считаем направление руками: Sub и Normalize у Vec3d портят исходный объект
                    double dirX = targetCenter.X - linkStartPos.X;
                    double dirY = targetCenter.Y - linkStartPos.Y;
                    double dirZ = targetCenter.Z - linkStartPos.Z;
                    double dist = Math.Sqrt(dirX * dirX + dirY * dirY + dirZ * dirZ);

                    if (dist > 0.01 && rand.NextDouble() < 0.4)
                    {
                        Vec3d targetDir = new Vec3d(dirX / dist, dirY / dist, dirZ / dist);

                        // лёгкая хаотичность, промах по цели теперь не страшен
                        targetDir.X += (rand.NextDouble() - 0.5) * 0.15;
                        targetDir.Y += (rand.NextDouble() - 0.5) * 0.15;
                        targetDir.Z += (rand.NextDouble() - 0.5) * 0.15;
                        targetDir.Normalize();

                        float speed = 2.5f;   // блоков в секунду, скорость к ядру альфхейма

                        // жизнь только страховка на случай промаха, с запасом на разброс
                        float life = GameMath.Clamp((float)(dist / speed) * 1.3f + 0.2f, 0.3f, 8f);

                        particleRenderer.Particles.Add(new PylonParticle()
                        {
                            Position = linkStartPos.Clone(),
                            Velocity = new Vec3d(targetDir.X * speed, targetDir.Y * speed, targetDir.Z * speed),
                            Color = new Vec4f(pylonColor.X, pylonColor.Y, pylonColor.Z, 0.9f),
                            Size = 0.45f + (float)rand.NextDouble() * 0.15f,
                            Life = life,
                            MaxLife = life,
                            TextureIndex = 4,
                            ShrinkOnDeath = false,
                            FadeStart = 0.95f,

                            Target = targetCenter.Clone(),
                            TargetRadius = 0.5,
                            ImpactOnArrive = true
                        });
                    }

                    // Ломалась ТОЛЬКО из-за Drag'а: частицы переставали подниматься. Теперь Drag=0 => винт вернулся.
                    // Длинную жизнь НЕ трогаем - именно она даёт ~2.5 витка одновременно.
                    double spiralTime = Api.World.ElapsedMilliseconds / 600.0;
                    double spiralRadius = 0.7;

                    if (rand.NextDouble() > 0.05)
                    {
                        double jitterAngle = (rand.NextDouble() - 0.5) * 0.15;
                        double spawnX = Pos.X + 0.5 + Math.Cos(spiralTime + jitterAngle) * spiralRadius;
                        double spawnY = Pos.Y + 0.05 + rand.NextDouble() * 0.1;
                        double spawnZ = Pos.Z + 0.5 + Math.Sin(spiralTime + jitterAngle) * spiralRadius;

                        float life = 5.0f + (float)rand.NextDouble() * 1.5f;

                        particleRenderer.Particles.Add(new PylonParticle()
                        {
                            Position = new Vec3d(spawnX, spawnY, spawnZ),
                            Velocity = new Vec3d(0, 0.2, 0),   // подъём. Винт высокий -> уменьши, или подними Drag до 0.1
                            Color = new Vec4f(pylonColor.X, pylonColor.Y, pylonColor.Z, 0.9f),
                            Size = 0.45f + (float)rand.NextDouble() * 0.15f,
                            Life = life,
                            MaxLife = life,
                            TextureIndex = 4,
                            ShrinkOnDeath = false,
                            FadeStart = 0.001f
                        });
                    }
                }
            }
        }

        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tessellator)
        {
            // Возвращаем true, чтобы движок НЕ генерировал стандартную JSON-модель в самом мире.
            // При этом в инвентаре и в руках JSON-модель будет отображаться корректно!
            return true;
        }

        public override void OnBlockRemoved()
        {
            TurnOffGateway();

            base.OnBlockRemoved();

            modelRenderer?.Dispose();
            modelRenderer = null;

            // particleRenderer общий, его освобождает PylonParticleSystem
            particleRenderer = null;
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();

            modelRenderer?.Dispose();
            modelRenderer = null;
            particleRenderer = null;
        }
    }
}