using System;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class ManaTrailParticle
    {
        public Vec3d Position;
        public Vec3f Velocity;
        public float Life;
        public float MaxLife;
        public float Size;
        public float R, G, B;
    }

    public class ManaBurstTrailRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private readonly List<ManaTrailParticle> particles = new List<ManaTrailParticle>();

        // шаг между частицами вдоль луча, из оригинала
        private const double Step = 0.095;

        // ширина квадки в блоках
        private const float BeamSize = 0.50f;
        private const float TailSize = 0.25f;

        private const float MinLife = 1.0f;
        private const float MaxLife = 2.5f;

        // дальше этого от камеры не рисуем
        private const float CullDistance = 48f;

        // жесткий потолок и мягкий, на мягком перестаем сыпать ореол
        private const int MaxParticles = 4000;
        private const int SoftCap = 2500;

        // предохранитель на случай телепорта или огромного разрыва
        private const int MaxStepsPerTick = 48;

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        private bool assetsLoaded = false;

        public Matrixf ModelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        public ManaBurstTrailRenderer(ICoreClientAPI api)
        {
            this.capi = api;
            api.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "manabursttrail");
        }

        private void LoadAssets()
        {
            assetsLoaded = true;

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

        // время жизни как у wisp в оригинале: 8 / (rand * 0.8 + 0.2) тиков
        private float RollLife()
        {
            double roll = 1.0 / (capi.World.Rand.NextDouble() * 0.8 + 0.2);
            float life = (float)(MinLife * roll);
            return life > MaxLife ? MaxLife : life;
        }

        private void Add(double x, double y, double z, float vx, float vy, float vz, float size, float r, float g, float b)
        {
            float life = RollLife();

            particles.Add(new ManaTrailParticle()
            {
                Position = new Vec3d(x, y, z),
                Velocity = new Vec3f(vx, vy, vz),
                Life = life,
                MaxLife = life,
                Size = size,
                R = r,
                G = g,
                B = b
            });
        }

        // сплошной отрезок между позицией прошлого тика и текущей
        // from - где искра была тик назад, to - где она сейчас
        public void SpawnBeam(Vec3d from, Vec3d to, Vec3d motion, float sizeRatio, float burstJitter, float cr, float cg, float cb)
        {
            if (particles.Count >= MaxParticles) return;

            double dx = from.X - to.X;
            double dy = from.Y - to.Y;
            double dz = from.Z - to.Z;
            double gap = Math.Sqrt(dx * dx + dy * dy + dz * dz);

            // частицы ядра сносит назад по ходу полета, в оригинале это -motion * 0.01 за тик
            float vx = (float)(-motion.X * 0.01);
            float vy = (float)(-motion.Y * 0.01);
            float vz = (float)(-motion.Z * 0.01);

            double luminance = 0.2126 * cr + 0.7152 * cg + 0.0722 * cb;

            double ix = to.X;
            double iy = to.Y;
            double iz = to.Z;

            double nx = 0, ny = 0, nz = 0;
            if (gap > 1e-6)
            {
                nx = dx / gap;
                ny = dy / gap;
                nz = dz / gap;
            }

            double walked = 0;
            int steps = 0;

            while (true)
            {
                float r = cr, g = cg, b = cb;

                // темный цвет подсвечиваем, иначе луч не читается
                if (luminance < 0.1)
                {
                    r += (float)capi.World.Rand.NextDouble() * 0.125f;
                    g += (float)capi.World.Rand.NextDouble() * 0.125f;
                    b += (float)capi.World.Rand.NextDouble() * 0.125f;
                }

                float jittered = sizeRatio + ((float)capi.World.Rand.NextDouble() - 0.5f) * 0.065f + burstJitter;
                if (jittered < 0.08f) jittered = 0.08f;

                Add(ix, iy, iz, vx, vy, vz, BeamSize * jittered, r, g, b);

                steps++;
                if (steps >= MaxStepsPerTick) break;
                if (particles.Count >= MaxParticles) return;

                ix += nx * Step;
                iy += ny * Step;
                iz += nz * Step;
                walked += Step;

                if (gap <= Step || walked > gap - Step) break;
            }

            // замыкающая искра, она же ореол - живет долго и разлетается в стороны
            if (particles.Count >= SoftCap) return;

            float tail = sizeRatio < 0.08f ? 0.08f : sizeRatio;

            Add(ix, iy, iz,
                (float)(capi.World.Rand.NextDouble() - 0.5) * 1.8f,
                (float)(capi.World.Rand.NextDouble() - 0.5) * 1.8f,
                (float)(capi.World.Rand.NextDouble() - 0.5) * 1.8f,
                TailSize * tail, cr, cg, cb);
        }

        // хлопок при попадании, 4 искры и одна вспышка
        public void SpawnImpact(Vec3d pos, float sizeRatio, float cr, float cg, float cb)
        {
            if (particles.Count >= MaxParticles) return;

            float s = sizeRatio < 0.15f ? 0.15f : sizeRatio;

            for (int i = 0; i < 4; i++)
            {
                Add(pos.X, pos.Y, pos.Z,
                    (float)(capi.World.Rand.NextDouble() - 0.5) * 1.2f,
                    (float)(capi.World.Rand.NextDouble() - 0.5) * 1.2f,
                    (float)(capi.World.Rand.NextDouble() - 0.5) * 1.2f,
                    0.35f * s, cr, cg, cb);
            }

            Add(pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 1.4f * s, cr, cg, cb);
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (!assetsLoaded) LoadAssets();

            if (particles.Count == 0) return;
            if (quadMeshRef == null) return;
            if (particleTexture == null || particleTexture.Disposed || particleTexture.TextureId == 0) return;

            for (int i = particles.Count - 1; i >= 0; i--)
            {
                ManaTrailParticle p = particles[i];
                p.Life -= deltaTime;

                if (p.Life <= 0f)
                {
                    particles.RemoveAt(i);
                    continue;
                }

                p.Position.X += p.Velocity.X * deltaTime;
                p.Position.Y += p.Velocity.Y * deltaTime;
                p.Position.Z += p.Velocity.Z * deltaTime;
            }

            if (particles.Count == 0) return;

            IClientPlayer player = capi.World.Player;
            if (player?.Entity == null) return;

            IRenderAPI render = capi.Render;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = render.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);

            render.BindTexture2d(particleTexture.TextureId);

            prog.Uniform("alphaTest", 0.02f);
            prog.Uniform("extraGlow", 0);
            prog.NormalShaded = 0;

            render.GlToggleBlend(true, EnumBlendMode.Glow);
            GL.DepthMask(false);

            float cullSq = CullDistance * CullDistance;

            foreach (ManaTrailParticle p in particles)
            {
                double rx = p.Position.X - camPos.X;
                double ry = p.Position.Y - camPos.Y;
                double rz = p.Position.Z - camPos.Z;

                if (rx * rx + ry * ry + rz * rz > cullSq) continue;

                float fade = p.Life / p.MaxLife;

                // яркость держится почти всю жизнь и гаснет только в конце
                float bright = fade > 0.45f ? 1f : fade / 0.45f;

                // гасим вклад каждой частицы, иначе десять слоев складываются в чистый канал
                const float Density = 0.45f;

                float k = bright * Density;
                Vec4f c = new Vec4f(p.R * k, p.G * k, p.B * k, bright);

                prog.RgbaAmbientIn = new Vec3f(c.X, c.Y, c.Z);
                prog.RgbaLightIn = c;
                prog.RgbaGlowIn = c;
                prog.RgbaTint = c;

                // ужимается мягко, иначе поток теряет объем
                float size = p.Size * (0.6f + fade * 0.4f);

                ModelMat.Identity();
                ModelMat.Translate(rx, ry, rz);
                ModelMat.RotateY(player.CameraYaw);
                ModelMat.RotateX(player.CameraPitch);
                ModelMat.Scale(size, size, size);

                prog.ModelMatrix = ModelMat.Values;
                prog.ViewMatrix = render.CameraMatrixOriginf;
                prog.ProjectionMatrix = render.CurrentProjectionMatrix;

                render.RenderMesh(quadMeshRef);
            }

            // возвращаем шейдеру чистый белый, иначе покрасит весь мир
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);

            prog.Stop();

            GL.DepthMask(true);
            render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);

            quadMeshRef?.Dispose();
            quadMeshRef = null;

            particles.Clear();
            particleTexture = null;
        }
    }
}