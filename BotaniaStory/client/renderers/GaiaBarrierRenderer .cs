using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using OpenTK.Graphics.OpenGL;
using BotaniaStory.entities;

namespace BotaniaStory.client.renderers
{
   
    public class GaiaBarrierRenderer : IRenderer
    {
        private const float Radius = EntityGaiaGuardian.ArenaRadius;
        private const float RingThickness = 0.5f;
        private const float FloorOffset = -1f;                         // Гайа на маяке (на 1 выше пола) - огонь к полу
        private const float BaseAlpha = 0.55f;
        private const int MaxParticles = 4500;

        //БАРЬЕР
        private const int BaseSpawnPerSec = 640;
        private const float BaseLifeMin = 0.35f;
        private const float BaseLifeMax = 0.7f;
        private const float BaseRiseMin = 0.15f;
        private const float BaseRiseMax = 0.6f;
        private const float BaseSizeMin = 0.25f;
        private const float BaseSizeMax = 0.5f;

        //БАРЬЕР: языки пламени
        private const int TongueSpawnPerSec = 180;
        private const float TongueLifeMin = 0.8f;
        private const float TongueLifeMax = 1.5f;
        private const float TongueRiseMin = 1.8f;
        private const float TongueRiseMax = 3.2f;
        private const float TongueRiseDecel = 1.1f;
        private const float TongueSizeMin = 0.35f;
        private const float TongueSizeMax = 0.7f;
        private const float ShrinkAmount = 0.65f;

        //Турбулентность барьера
        private const float WobbleAmpMin = 0.15f;
        private const float WobbleAmpMax = 0.5f;
        private const float WobbleFreqMin = 2.5f;
        private const float WobbleFreqMax = 6f;

        //ЦЕПИ ИЗ ПИЛОНОВ (рождение + левитация)
        private const float PylonOffsetXZ = 4f;        // пилоны на (±4, ±4) от центра (синхронно с GaiaRitualSystem)
        private const float PylonFxHeight = 1.0f;      // высота истока над основанием пилона (подстрой под модель)
        private const int BeamPerSecPerPylon = 45;     // частиц цепи в секунду с каждого пилона
        private const float BeamTravelMin = 0.8f;      // время пути пилон->Гайа, сек
        private const float BeamTravelMax = 1.4f;
        private const float BeamSagFactor = 0.22f;     // провисание цепи = доля от длины пролёта
        private const float BeamSagVerticalWeight = 1.2f; // вклад вертикального подъёма в провисание (фаза левитации)
        private const float BeamSwirlAmp = 0.45f;      // дрожание поперёк цепи
        private const float BeamScatterSide = 0.9f;    // персональный боковой сдвиг дуги каждой частицы
        private const float BeamScatterVert = 0.6f;    // персональный вертикальный сдвиг дуги
        private const float BeamSize = 0.48f;
        private const float BeamTargetHeight = 1.3f;   // куда в теле Гайи приходит цепь

        //РОЖДЕНИЕ
        private const int RayPerSec = 90;              // лучи, бьющие из Гайи во все стороны
        private const float RaySpeedMin = 5f;
        private const float RaySpeedMax = 9f;
        private const int GatherPerSec = 90;           // энергия, стягивающаяся в тело
        private const float GatherRadius = 4.5f;       // с какого радиуса стягивается
        private const float GatherArcSide = 1.4f;      // боковая дуга траектории (разброс вместо прямых)
        private const float GatherArcVert = 0.9f;      // вертикальная дуга траектории
        private const float PillarAlpha = 0.22f;       // столб света
        private const float PillarHeight = 5.5f;
        private const float PillarWidth = 0.8f;

        //АУРА
        private const int WispPerSecNormal = 18;
        private const int WispPerSecCharged = 70;      // в левитации и рождении
        private const int OrbiterCount = 14;
        private const float OrbiterLife = 2.0f;
        private const float OrbiterRadius = 0.95f;
        private const float StreaksPerSecNormal = 5f;
        private const float StreaksPerSecCharged = 20f;
        private const float HazeBaseSize = 3.4f;
        private const float HazeBaseAlpha = 0.15f;     

        //Отталкивание собственного игрока (клиент-сайд)
        private const float PushStrength = 0.22f;
        private const float PushUp = 0.08f;
        private const float ConfineMargin = 5f;

        private static readonly Vec3f HotColor = new Vec3f(1.0f, 0.5f, 0.8f);
        private static readonly Vec3f CoolColor = new Vec3f(0.75f, 0.0f, 0.35f);
        private static readonly Vec3f StreakColor = new Vec3f(1.0f, 0.85f, 0.95f);

        private ICoreClientAPI capi;
        private MeshRef quadMeshRef = null;
        private LoadedTexture particleTexture = null;
        public Matrixf ModelMat = new Matrixf();
        private GaiaPlayerCountHud playerCountHud;

        private enum FxKind { BarrierBase, BarrierTongue, Wisp, Beam, Orbit, Streak, Flash, Ray, Gather }

        private class Fx
        {
            public FxKind Kind;
            public Vec3d Pos = new Vec3d();
            public double SrcX, SrcY, SrcZ;  // исток для цепей/стягивания
            public float Vx, Vy, Vz;
            public float Age;
            public float MaxAge;
            public float Size;
            public float SizeY;              // вертикальная растяжка (штрихи, лучи)
            public float P0, P1, P2, P3;
            public float A0, A1;
            public float RiseDecel;
            public float TangX, TangZ, RadX, RadZ;
        }

        private List<Fx> particles = new List<Fx>();

        private double centerX, centerY, centerZ;
        private bool hasBoss;
        private Entity bossEntity;
        private bool levitating;
        private bool birthing;
        private float fxTime;

        private float baseSpawnAccum, tongueSpawnAccum, wispAccum, beamAccum, orbitAccum, streakAccum, rayAccum, gatherAccum;
        private float scanAccum;

        public double RenderOrder => 0.5;
        public int RenderRange => 128;

        public GaiaBarrierRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "gaiabarrier");
            LoadTextureAndMesh();
            playerCountHud = new GaiaPlayerCountHud(api);
        }

        private void LoadTextureAndMesh()
        {
            AssetLocation texLocation = new AssetLocation("botaniastory", "textures/particle/mana_particle.png");
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLocation, ref particleTexture);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] {
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255,
                255, 255, 255, 255
            };
            quad.Flags = new int[] { 0, 0, 0, 0 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        // Раз в 0.5с ищем живого босса; каждый кадр читаем его живую позицию и фазу
        private void UpdateBoss(float dt)
        {
            scanAccum += dt;
            if (scanAccum >= 0.5f)
            {
                scanAccum = 0f;

                EntityPlayer plr = capi.World.Player?.Entity;
                bossEntity = plr == null ? null : capi.World.GetNearestEntity(plr.Pos.XYZ, RenderRange, RenderRange,
                    e => e.Alive && e is EntityGaiaGuardian);

                if (bossEntity != null)
                {
                    centerX = bossEntity.WatchedAttributes.GetDouble("gaiaSpawnPosX", bossEntity.Pos.X);
                    centerY = bossEntity.WatchedAttributes.GetDouble("gaiaSpawnPosY", bossEntity.Pos.Y);
                    centerZ = bossEntity.WatchedAttributes.GetDouble("gaiaSpawnPosZ", bossEntity.Pos.Z);
                }
            }

            if (bossEntity != null && !bossEntity.Alive) bossEntity = null;
            hasBoss = bossEntity != null;
            levitating = hasBoss && bossEntity.WatchedAttributes.GetBool("isLevitating", false);
            birthing = hasBoss && bossEntity.WatchedAttributes.GetFloat("gaiaBirthTimer", 0f) > 0f;

            // HUD: показываем число игроков при призыве, пока босс жив
            if (hasBoss)
            {
                playerCountHud.Show(bossEntity.WatchedAttributes.GetInt("gaiaPlayerCount", 1));
            }
            else
            {
                playerCountHud.Hide();
            }
        }

        // Барьер выталкивает собственного игрока обратно в арену (клиент-сайд: тут Motion авторитетен)
        private void ConfineOwnPlayer()
        {
            IClientPlayer plr = capi.World.Player;
            EntityPlayer pe = plr?.Entity;
            if (pe == null || !pe.Alive) return;

            EnumGameMode mode = plr.WorldData?.CurrentGameMode ?? EnumGameMode.Survival;
            if (mode == EnumGameMode.Creative || mode == EnumGameMode.Spectator) return;

            double dx = pe.Pos.X - centerX;
            double dz = pe.Pos.Z - centerZ;
            double distSq = dx * dx + dz * dz;
            if (distSq <= Radius * Radius) return;

            float outer = Radius + ConfineMargin;
            if (distSq > outer * outer) return;

            double dist = Math.Sqrt(distSq);

            pe.Pos.Motion.X = -dx / dist * PushStrength;
            pe.Pos.Motion.Z = -dz / dist * PushStrength;
            if (pe.Pos.Motion.Y < PushUp) pe.Pos.Motion.Y = PushUp;
        }

        // СПАВН

        private void SpawnBarrierParticle(Random rnd, bool tongue)
        {
            float ang = (float)(rnd.NextDouble() * GameMath.TWOPI);
            float cos = GameMath.Cos(ang);
            float sin = GameMath.Sin(ang);
            float r = Radius + (float)(rnd.NextDouble() - 0.5) * RingThickness;

            var p = new Fx
            {
                Kind = tongue ? FxKind.BarrierTongue : FxKind.BarrierBase,
                Age = 0f,
                RadX = cos,
                RadZ = sin,
                TangX = -sin,
                TangZ = cos,
                P0 = (float)(rnd.NextDouble() * GameMath.TWOPI),
                P1 = (float)(rnd.NextDouble() * GameMath.TWOPI),
                P2 = WobbleFreqMin + (float)rnd.NextDouble() * (WobbleFreqMax - WobbleFreqMin),
                P3 = WobbleFreqMin + (float)rnd.NextDouble() * (WobbleFreqMax - WobbleFreqMin),
                A0 = WobbleAmpMin + (float)rnd.NextDouble() * (WobbleAmpMax - WobbleAmpMin),
                A1 = (WobbleAmpMin + (float)rnd.NextDouble() * (WobbleAmpMax - WobbleAmpMin)) * 0.5f
            };
            p.Pos.Set(centerX + cos * r, centerY + FloorOffset, centerZ + sin * r);

            if (tongue)
            {
                p.MaxAge = TongueLifeMin + (float)rnd.NextDouble() * (TongueLifeMax - TongueLifeMin);
                p.Vy = TongueRiseMin + (float)rnd.NextDouble() * (TongueRiseMax - TongueRiseMin);
                p.RiseDecel = TongueRiseDecel;
                p.Size = TongueSizeMin + (float)rnd.NextDouble() * (TongueSizeMax - TongueSizeMin);
            }
            else
            {
                p.MaxAge = BaseLifeMin + (float)rnd.NextDouble() * (BaseLifeMax - BaseLifeMin);
                p.Vy = BaseRiseMin + (float)rnd.NextDouble() * (BaseRiseMax - BaseRiseMin);
                p.RiseDecel = 0f;
                p.Size = BaseSizeMin + (float)rnd.NextDouble() * (BaseSizeMax - BaseSizeMin);
            }

            particles.Add(p);
        }

        // Цепь: частица идёт по провисающей дуге от верхушки пилона к Гайе
        private void SpawnBeamParticles(Random rnd)
        {
            for (int sx = -1; sx <= 1; sx += 2)
            {
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    var p = new Fx
                    {
                        Kind = FxKind.Beam,
                        Age = 0f,
                        MaxAge = BeamTravelMin + (float)rnd.NextDouble() * (BeamTravelMax - BeamTravelMin),
                        Size = BeamSize * (0.75f + (float)rnd.NextDouble() * 0.5f),
                        P0 = (float)(rnd.NextDouble() * GameMath.TWOPI),        // фаза дрожания
                        P1 = 5f + (float)rnd.NextDouble() * 4f,                 // частота дрожания
                        A0 = BeamSwirlAmp * (0.5f + (float)rnd.NextDouble()),   // амплитуда дрожания
                        A1 = (float)(rnd.NextDouble() - 0.5) * 2f * BeamScatterSide, // свой боковой сдвиг дуги
                        P2 = (float)(rnd.NextDouble() - 0.5) * 2f * BeamScatterVert  // свой вертикальный сдвиг дуги
                    };
                    p.SrcX = centerX + sx * PylonOffsetXZ + (rnd.NextDouble() - 0.5) * 0.8;
                    p.SrcY = centerY + PylonFxHeight + (rnd.NextDouble() - 0.5) * 0.6;
                    p.SrcZ = centerZ + sz * PylonOffsetXZ + (rnd.NextDouble() - 0.5) * 0.8;
                    p.Pos.Set(p.SrcX, p.SrcY, p.SrcZ);
                    particles.Add(p);
                }
            }
        }

        // Луч рождения: вылетает из тела наружу, растянутый росчерк
        private void SpawnRay(Random rnd)
        {
            if (bossEntity == null) return;

            // случайное 3D-направление со смещением вверх
            double theta = rnd.NextDouble() * GameMath.TWOPI;
            double vert = rnd.NextDouble() * 1.2 - 0.3; // -0.3..0.9
            double horiz = Math.Sqrt(Math.Max(0.05, 1 - vert * vert));
            float speed = RaySpeedMin + (float)rnd.NextDouble() * (RaySpeedMax - RaySpeedMin);

            var p = new Fx
            {
                Kind = FxKind.Ray,
                Age = 0f,
                MaxAge = 0.25f + (float)rnd.NextDouble() * 0.25f,
                Size = 0.1f + (float)rnd.NextDouble() * 0.1f,
                SizeY = 0.9f + (float)rnd.NextDouble() * 0.9f,
                Vx = (float)(Math.Cos(theta) * horiz) * speed,
                Vy = (float)vert * speed,
                Vz = (float)(Math.Sin(theta) * horiz) * speed
            };
            p.Pos.Set(bossEntity.Pos.X, bossEntity.Pos.Y + 1.1, bossEntity.Pos.Z);
            particles.Add(p);
        }

        // Стягивание энергии: частица рождается вокруг и ускоряясь втягивается в тело
        private void SpawnGather(Random rnd)
        {
            float ang = (float)(rnd.NextDouble() * GameMath.TWOPI);
            float r = GatherRadius * (0.6f + (float)rnd.NextDouble() * 0.7f);

            var p = new Fx
            {
                Kind = FxKind.Gather,
                Age = 0f,
                MaxAge = 0.6f + (float)rnd.NextDouble() * 0.4f,
                Size = 0.28f + (float)rnd.NextDouble() * 0.24f,
                TangX = -GameMath.Sin(ang),
                TangZ = GameMath.Cos(ang),
                A0 = (float)(rnd.NextDouble() - 0.5) * 2f * GatherArcSide,  // боковая дуга (знак = сторона)
                A1 = (float)(rnd.NextDouble() - 0.5) * 2f * GatherArcVert   // вертикальная дуга
            };
            p.SrcX = centerX + GameMath.Cos(ang) * r;
            p.SrcY = centerY + 0.2 + rnd.NextDouble() * 2.4;
            p.SrcZ = centerZ + GameMath.Sin(ang) * r;
            p.Pos.Set(p.SrcX, p.SrcY, p.SrcZ);
            particles.Add(p);
        }

        private void SpawnWisp(Random rnd)
        {
            if (bossEntity == null) return;
            float ang = (float)(rnd.NextDouble() * GameMath.TWOPI);
            float r = 0.2f + (float)rnd.NextDouble() * 0.35f;

            var p = new Fx
            {
                Kind = FxKind.Wisp,
                Age = 0f,
                MaxAge = 0.5f + (float)rnd.NextDouble() * 0.5f,
                Size = 0.15f + (float)rnd.NextDouble() * 0.18f,
                Vy = 0.5f + (float)rnd.NextDouble() * 0.5f,
                Vx = (float)(rnd.NextDouble() - 0.5) * 0.2f,
                Vz = (float)(rnd.NextDouble() - 0.5) * 0.2f
            };
            p.Pos.Set(
                bossEntity.Pos.X + GameMath.Cos(ang) * r,
                bossEntity.Pos.Y + rnd.NextDouble() * 1.9,
                bossEntity.Pos.Z + GameMath.Sin(ang) * r);
            particles.Add(p);
        }

        private void SpawnOrbiter(Random rnd)
        {
            var p = new Fx
            {
                Kind = FxKind.Orbit,
                Age = 0f,
                MaxAge = OrbiterLife,
                Size = 0.16f + (float)rnd.NextDouble() * 0.12f,
                P0 = (float)(rnd.NextDouble() * GameMath.TWOPI),
                P1 = (2.2f + (float)rnd.NextDouble() * 1.6f) * (rnd.Next(2) == 0 ? 1f : -1f),
                P2 = OrbiterRadius * (0.85f + (float)rnd.NextDouble() * 0.3f),
                P3 = 0.5f + (float)rnd.NextDouble() * 1.2f,
                A0 = (float)(rnd.NextDouble() * GameMath.TWOPI)
            };
            particles.Add(p);
        }

        private void SpawnStreak(Random rnd)
        {
            if (bossEntity == null) return;
            var p = new Fx
            {
                Kind = FxKind.Streak,
                Age = 0f,
                MaxAge = 0.08f + (float)rnd.NextDouble() * 0.14f,
                Size = 0.16f + (float)rnd.NextDouble() * 0.2f,    // ширина
                SizeY = 1.6f + (float)rnd.NextDouble() * 1.6f     // высота (растянутый росчерк)
            };
            p.Pos.Set(
                bossEntity.Pos.X + (rnd.NextDouble() - 0.5) * 1.2,
                bossEntity.Pos.Y + 0.3 + rnd.NextDouble() * 1.5,
                bossEntity.Pos.Z + (rnd.NextDouble() - 0.5) * 1.2);
            particles.Add(p);
        }

        private void SpawnFlash(Random rnd, double x, double y, double z)
        {
            for (int i = 0; i < 2; i++)
            {
                var p = new Fx
                {
                    Kind = FxKind.Flash,
                    Age = 0f,
                    MaxAge = 0.15f + (float)rnd.NextDouble() * 0.1f,
                    Size = 0.3f + (float)rnd.NextDouble() * 0.25f,
                    Vx = (float)(rnd.NextDouble() - 0.5) * 1.5f,
                    Vy = (float)(rnd.NextDouble() - 0.5) * 1.5f,
                    Vz = (float)(rnd.NextDouble() - 0.5) * 1.5f
                };
                p.Pos.Set(x, y, z);
                particles.Add(p);
            }
        }

        // ТИК

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (particleTexture == null || particleTexture.Disposed || particleTexture.TextureId == 0) return;
            if (quadMeshRef == null) return;

            fxTime += deltaTime;
            UpdateBoss(deltaTime);

            var rnd = capi.World.Rand;
            bool charged = levitating || birthing;

            if (hasBoss)
            {
                ConfineOwnPlayer();

                bool room = particles.Count < MaxParticles;

                if (room)
                {
                    baseSpawnAccum += deltaTime * BaseSpawnPerSec;
                    while (baseSpawnAccum >= 1f) { baseSpawnAccum -= 1f; SpawnBarrierParticle(rnd, false); }

                    tongueSpawnAccum += deltaTime * TongueSpawnPerSec;
                    while (tongueSpawnAccum >= 1f) { tongueSpawnAccum -= 1f; SpawnBarrierParticle(rnd, true); }

                    wispAccum += deltaTime * (charged ? WispPerSecCharged : WispPerSecNormal);
                    while (wispAccum >= 1f) { wispAccum -= 1f; SpawnWisp(rnd); }

                    orbitAccum += deltaTime * (OrbiterCount / OrbiterLife);
                    while (orbitAccum >= 1f) { orbitAccum -= 1f; SpawnOrbiter(rnd); }

                    streakAccum += deltaTime * (charged ? StreaksPerSecCharged : StreaksPerSecNormal);
                    while (streakAccum >= 1f) { streakAccum -= 1f; SpawnStreak(rnd); }

                    // Цепи из пилонов: и при рождении, и при левитации
                    if (charged)
                    {
                        beamAccum += deltaTime * BeamPerSecPerPylon;
                        while (beamAccum >= 1f) { beamAccum -= 1f; SpawnBeamParticles(rnd); }
                    }

                    // Только рождение: лучи наружу + стягивание энергии
                    if (birthing)
                    {
                        rayAccum += deltaTime * RayPerSec;
                        while (rayAccum >= 1f) { rayAccum -= 1f; SpawnRay(rnd); }

                        gatherAccum += deltaTime * GatherPerSec;
                        while (gatherAccum >= 1f) { gatherAccum -= 1f; SpawnGather(rnd); }
                    }
                }
            }
            else
            {
                baseSpawnAccum = tongueSpawnAccum = wispAccum = beamAccum = orbitAccum = streakAccum = rayAccum = gatherAccum = 0f;
            }

            double bx = 0, by = 0, bz = 0;
            if (hasBoss) { bx = bossEntity.Pos.X; by = bossEntity.Pos.Y; bz = bossEntity.Pos.Z; }
            double beamTx = bx, beamTy = by + BeamTargetHeight, beamTz = bz;

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                var p = particles[i];
                p.Age += deltaTime;
                if (p.Age >= p.MaxAge)
                {
                    // Цепь дошла до Гайи - вспышка в точке прихода
                    if (p.Kind == FxKind.Beam && hasBoss) SpawnFlash(rnd, beamTx, beamTy, beamTz);
                    particles.RemoveAt(i);
                    continue;
                }

                switch (p.Kind)
                {
                    case FxKind.BarrierBase:
                    case FxKind.BarrierTongue:
                        if (p.RiseDecel > 0f)
                        {
                            p.Vy -= p.RiseDecel * deltaTime;
                            if (p.Vy < 0.25f) p.Vy = 0.25f;
                        }
                        p.Pos.Y += p.Vy * deltaTime;
                        float wobT = GameMath.Cos(p.P0 + p.Age * p.P2) * p.A0;
                        float wobR = GameMath.Cos(p.P1 + p.Age * p.P3) * p.A1;
                        p.Pos.X += (p.TangX * wobT + p.RadX * wobR) * deltaTime;
                        p.Pos.Z += (p.TangZ * wobT + p.RadZ * wobR) * deltaTime;
                        break;

                    case FxKind.Beam:
                        if (!hasBoss) { particles.RemoveAt(i); continue; }
                        // Провисающая цепь: линейная интерполяция исток - цель минус парабола провисания
                        float tb = p.Age / p.MaxAge;
                        double lx = p.SrcX + (beamTx - p.SrcX) * tb;
                        double ly = p.SrcY + (beamTy - p.SrcY) * tb;
                        double lz = p.SrcZ + (beamTz - p.SrcZ) * tb;

                        double hdx = beamTx - p.SrcX, hdz = beamTz - p.SrcZ;
                        double hdist = Math.Sqrt(hdx * hdx + hdz * hdz);
                        // База провиса = горизонталь + вертикаль пролёта: когда Гайа висит в небе,
                        // горизонталь та же (~4), но пролёт длиннее и круче - цепь должна провисать глубже
                        double sagBasis = hdist + Math.Abs(beamTy - p.SrcY) * BeamSagVerticalWeight;
                        double sag = sagBasis * BeamSagFactor * 4.0 * tb * (1.0 - tb); // максимум в середине пути

                        // Разброс: у каждой частицы своя смещенная дуга; колокол sin(pi*t) держит концы на месте
                        double pxn = 0, pzn = 0;
                        if (hdist > 1e-4) { pxn = -hdz / hdist; pzn = hdx / hdist; }
                        float bell = GameMath.Sin(GameMath.PI * tb);
                        float side = GameMath.Cos(p.P0 + p.Age * p.P1) * p.A0 + p.A1 * bell;

                        p.Pos.Set(lx + pxn * side, ly - sag + p.P2 * bell, lz + pzn * side);
                        break;

                    case FxKind.Gather:
                        if (!hasBoss) { particles.RemoveAt(i); continue; }
                        // ускоряющееся втягивание (ease-in по квадрату) + персональная дуга вместо прямой
                        float tg = p.Age / p.MaxAge;
                        float te = tg * tg;
                        float gbell = GameMath.Sin(GameMath.PI * te);
                        p.Pos.Set(
                            p.SrcX + (bx - p.SrcX) * te + p.TangX * p.A0 * gbell,
                            p.SrcY + (by + 1.1 - p.SrcY) * te + p.A1 * gbell,
                            p.SrcZ + (bz - p.SrcZ) * te + p.TangZ * p.A0 * gbell);
                        break;

                    case FxKind.Wisp:
                    case FxKind.Flash:
                    case FxKind.Ray:
                        p.Pos.X += p.Vx * deltaTime;
                        p.Pos.Y += p.Vy * deltaTime;
                        p.Pos.Z += p.Vz * deltaTime;
                        break;

                    case FxKind.Orbit:
                        if (!hasBoss) { particles.RemoveAt(i); continue; }
                        p.P0 += p.P1 * deltaTime;
                        p.Pos.Set(
                            bx + GameMath.Cos(p.P0) * p.P2,
                            by + p.P3 + GameMath.Sin(p.A0 + p.Age * 2.5f) * 0.15,
                            bz + GameMath.Sin(p.P0) * p.P2);
                        break;

                    case FxKind.Streak:
                        break; // штрих неподвижен, просто гаснет
                }
            }

            if (particles.Count == 0 && !hasBoss) return;

            IRenderAPI render = capi.Render;
            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            capi.Render.BindTexture2d(particleTexture.TextureId);

            prog.Uniform("alphaTest", 0.05f);
            prog.Uniform("extraGlow", 0);
            prog.NormalShaded = 0;

            render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            foreach (var p in particles)
            {
                float f = p.Age / p.MaxAge;
                Vec3f rgb;
                float alpha;
                float sx = p.Size, sy = p.Size;

                switch (p.Kind)
                {
                    case FxKind.Beam:
                        rgb = HotColor;
                        alpha = 0.75f;
                        break;
                    case FxKind.Gather:
                        rgb = HotColor;
                        alpha = 0.75f;
                        sx = sy = p.Size * (1f - f * 0.3f);
                        break;
                    case FxKind.Ray:
                        rgb = StreakColor;
                        alpha = 0.85f * (1f - f);
                        sy = p.SizeY;
                        break;
                    case FxKind.Streak:
                        rgb = StreakColor;
                        alpha = 0.9f * (1f - f);
                        sy = p.SizeY;
                        break;
                    case FxKind.Flash:
                        rgb = StreakColor;
                        alpha = 0.9f * (1f - f);
                        sx = sy = p.Size * (1f + f * 1.5f);
                        break;
                    case FxKind.Orbit:
                        rgb = CoolColor;
                        alpha = 0.65f * OrbitFade(f);
                        break;
                    default: // барьер и виспы - градиент пламени
                        rgb = new Vec3f(
                            HotColor.X + (CoolColor.X - HotColor.X) * f,
                            HotColor.Y + (CoolColor.Y - HotColor.Y) * f,
                            HotColor.Z + (CoolColor.Z - HotColor.Z) * f);
                        alpha = FadeAlpha(f);
                        if (p.Kind != FxKind.Wisp)
                            sx = sy = p.Size * (1f - f * ShrinkAmount);
                        break;
                }

                Vec4f col = new Vec4f(rgb.X, rgb.Y, rgb.Z, alpha);
                prog.RgbaAmbientIn = rgb;
                prog.RgbaLightIn = col;
                prog.RgbaGlowIn = col;
                prog.RgbaTint = col;

                DrawQuad(render, prog, player, camPos, p.Pos, sx, sy);
            }

            if (hasBoss)
            {
                // Дымка-искажение: 3 больших пульсирующих квада с разными фазами
                float hazeAlphaMul = charged ? 1.8f : 1f;
                float hazeSizeMul = charged ? 1.15f : 1f;
                for (int i = 0; i < 3; i++)
                {
                    float phase = fxTime * (1.1f + i * 0.37f) + i * 2.1f;
                    float s = (HazeBaseSize + GameMath.Sin(phase) * 0.6f) * hazeSizeMul;
                    float a = (HazeBaseAlpha + 0.05f * GameMath.Sin(phase * 1.7f)) * hazeAlphaMul;

                    Vec4f col = new Vec4f(CoolColor.X, CoolColor.Y, CoolColor.Z, a);
                    prog.RgbaAmbientIn = CoolColor;
                    prog.RgbaLightIn = col;
                    prog.RgbaGlowIn = col;
                    prog.RgbaTint = col;

                    DrawQuad(render, prog, player, camPos, new Vec3d(bx, by + 1.0, bz), s, s);
                }

                // Столб света при рождении
                if (birthing)
                {
                    for (int i = 0; i < 2; i++)
                    {
                        float phase = fxTime * (2.2f + i * 0.9f) + i * 1.4f;
                        float w = PillarWidth + GameMath.Sin(phase) * 0.15f;
                        float h = PillarHeight + GameMath.Sin(phase * 0.7f) * 0.8f;
                        float a = PillarAlpha + 0.06f * GameMath.Sin(phase * 1.3f);

                        Vec4f col = new Vec4f(HotColor.X, HotColor.Y, HotColor.Z, a);
                        prog.RgbaAmbientIn = HotColor;
                        prog.RgbaLightIn = col;
                        prog.RgbaGlowIn = col;
                        prog.RgbaTint = col;

                        DrawQuad(render, prog, player, camPos, new Vec3d(bx, by + 2.0, bz), w, h);
                    }
                }
            }

            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);

            prog.Stop();

            GL.DepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        private void DrawQuad(IRenderAPI render, IStandardShaderProgram prog, IClientPlayer player, Vec3d camPos, Vec3d pos, float sx, float sy)
        {
            ModelMat.Identity();
            ModelMat.Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z);
            ModelMat.RotateY(player.CameraYaw);
            ModelMat.RotateX(player.CameraPitch);
            ModelMat.Scale(sx, sy, sx);

            prog.ModelMatrix = ModelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            render.RenderMesh(quadMeshRef);
        }

        private static float FadeAlpha(float f)
        {
            const float fadeIn = 0.12f;
            const float fadeOut = 0.45f;
            float a;
            if (f < fadeIn) a = f / fadeIn;
            else if (f > 1f - fadeOut) a = (1f - f) / fadeOut;
            else a = 1f;
            return a * BaseAlpha;
        }

        private static float OrbitFade(float f)
        {
            if (f < 0.2f) return f / 0.2f;
            if (f > 0.8f) return (1f - f) / 0.2f;
            return 1f;
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadMeshRef?.Dispose();
            quadMeshRef = null;

            particleTexture = null;

            playerCountHud?.Dispose();
            playerCountHud = null;
        }
    }

    // HUD-значок справа: число игроков, присутствовавших при призыве Гайи
    public class GaiaPlayerCountHud : HudElement
    {
        private int lastCount = -1;

        public GaiaPlayerCountHud(ICoreClientAPI capi) : base(capi) { }

        public override EnumDialogType DialogType => EnumDialogType.HUD;
        public override bool Focusable => false;

        public void Show(int count)
        {
            if (count != lastCount)
            {
                lastCount = count;
                Compose(count);
            }
            if (!IsOpened()) TryOpen();
        }

        public void Hide()
        {
            lastCount = -1;
            if (IsOpened()) TryClose();
        }

        private void Compose(int count)
        {
            ElementBounds textBounds = ElementBounds.Fixed(0, 0, 260, 30);
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.RightTop)
                .WithFixedAlignmentOffset(-12, 180);

            SingleComposer?.Dispose();
            SingleComposer = capi.Gui.CreateCompo("gaiaplayercounthud", dialogBounds)
                .AddDynamicText("", CairoFont.WhiteSmallishText().WithOrientation(EnumTextOrientation.Right), textBounds, "cnt")
                .Compose();

            SingleComposer.GetDynamicText("cnt").SetNewText("Гайа · игроков при призыве: " + count);
        }
    }
}