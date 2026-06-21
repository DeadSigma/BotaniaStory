using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.renderers
{
    public class GaiaLightningRenderer : IRenderer, IDisposable
    {
        private ICoreClientAPI capi;
        private List<GaiaLightningBolt> bolts = new List<GaiaLightningBolt>();

        // Ресурсы для отрисовки (один кубик, который масштабируется в сегменты молнии)
        private MeshRef cubeMeshRef;
        private LoadedTexture effectTexture;
        private Matrixf modelMat = new Matrixf();

        public double RenderOrder => 0.5;
        public int RenderRange => 64; // Молнию должно быть видно издалека

        public GaiaLightningRenderer(ICoreClientAPI capi)
        {
            this.capi = capi;

            // Переиспользуем уже существующую текстуру алтаря, чтобы не плодить ассеты
            AssetLocation texLoc = new AssetLocation("botaniastory", "textures/block/runic_altar_cube.png");
            effectTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(texLoc, ref effectTexture);

            // Базовый кубик (-1..1), из которого строятся все сегменты
            MeshData cubeData = CubeMeshUtil.GetCube();
            cubeData.Flags = new int[cubeData.GetVerticesCount()]; // Отключаем ветер
            byte[] rgba = new byte[cubeData.GetVerticesCount() * 4];
            for (int i = 0; i < rgba.Length; i++) rgba[i] = 255; // Белый, чтобы работал RgbaTint
            cubeData.Rgba = rgba;
            cubeMeshRef = capi.Render.UploadMesh(cubeData);

            // Свечение рисуем на стадии AfterOIT (как кубы/звезда у алтаря)
            capi.Event.RegisterRenderer(this, EnumRenderStage.AfterOIT);
        }

        // Вызывается из обработчика пакета: бьём из центра Гайи (start) в центр игрока (end)
        public void AddLightning(Vec3d start, Vec3d end)
        {
            int seed = (int)(capi.World.ElapsedMilliseconds & 0x7FFFFFFF);
            bolts.Add(new GaiaLightningBolt(start, end, seed));
        }

        public void OnRenderFrame(float dt, EnumRenderStage stage)
        {
            if (bolts.Count == 0) return;

            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // Настройка шейдера один раз на кадр
            IStandardShaderProgram prog = rpi.PreparedStandardShader((int)camPos.X, (int)camPos.Y, (int)camPos.Z);
            prog.Tex2D = effectTexture.TextureId;
            prog.NormalShaded = 0;
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.ViewMatrix = rpi.CameraMatrixOriginf;
            prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

            rpi.GlToggleBlend(true, EnumBlendMode.Glow);
            rpi.GLDepthMask(false);

            for (int i = bolts.Count - 1; i >= 0; i--)
            {
                GaiaLightningBolt bolt = bolts[i];
                bolt.Render(dt, prog, rpi, modelMat, cubeMeshRef, camPos);
                if (!bolt.Alive) bolts.RemoveAt(i);
            }

            prog.Stop();
            rpi.GLDepthMask(true);
            rpi.GlToggleBlend(false);
        }

        public void Dispose()
        {
            capi?.Event.UnregisterRenderer(this, EnumRenderStage.AfterOIT);
            cubeMeshRef?.Dispose();
            effectTexture?.Dispose();
        }
    }

    public class GaiaLightningBolt
    {
        private List<Vec3d[]> strands = new List<Vec3d[]>();
        private float age = 0;
        private float maxAge = 0.45f; // Время жизни молнии
        public bool Alive => age < maxAge;

        public GaiaLightningBolt(Vec3d start, Vec3d end, int seed)
        {
            Random rnd = new Random(seed);

            double dx = end.X - start.X, dy = end.Y - start.Y, dz = end.Z - start.Z;
            double length = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (length < 1e-4) length = 1e-4;
            double jitter = length * 0.07; // Насколько сильно "пляшет" молния

            // Основная ветка
            strands.Add(BuildStrand(start.X, start.Y, start.Z, end.X, end.Y, end.Z, jitter, rnd));

            // 1-2 коротких ответвления для красоты
            Vec3d[] main = strands[0];
            int branches = 1 + rnd.Next(2);
            for (int b = 0; b < branches && main.Length > 3; b++)
            {
                int k = 1 + rnd.Next(main.Length - 2); // внутренняя точка основной ветки
                Vec3d bp = main[k];
                double bl = length * (0.15 + rnd.NextDouble() * 0.25);

                double ex = bp.X + (dx / length) * bl * 0.4 + (rnd.NextDouble() * 2 - 1) * bl;
                double ey = bp.Y + (dy / length) * bl * 0.4 + (rnd.NextDouble() * 2 - 1) * bl;
                double ez = bp.Z + (dz / length) * bl * 0.4 + (rnd.NextDouble() * 2 - 1) * bl;

                strands.Add(BuildStrand(bp.X, bp.Y, bp.Z, ex, ey, ez, bl * 0.18, rnd));
            }
        }

        // Строит ломаную с дрожанием от точки A к точке B
        private Vec3d[] BuildStrand(double sx, double sy, double sz, double ex, double ey, double ez, double jitter, Random rnd)
        {
            double dx = ex - sx, dy = ey - sy, dz = ez - sz;
            double len = Math.Sqrt(dx * dx + dy * dy + dz * dz);
            if (len < 1e-4) len = 1e-4;
            double nx = dx / len, ny = dy / len, nz = dz / len;

            // Два перпендикуляра к направлению
            double ux, uy, uz;
            if (Math.Abs(ny) < 0.99) { ux = 0; uy = 1; uz = 0; } else { ux = 1; uy = 0; uz = 0; }

            double p1x = ny * uz - nz * uy;
            double p1y = nz * ux - nx * uz;
            double p1z = nx * uy - ny * ux;
            double p1l = Math.Sqrt(p1x * p1x + p1y * p1y + p1z * p1z);
            if (p1l < 1e-6) p1l = 1e-6;
            p1x /= p1l; p1y /= p1l; p1z /= p1l;

            double p2x = ny * p1z - nz * p1y;
            double p2y = nz * p1x - nx * p1z;
            double p2z = nx * p1y - ny * p1x;

            int seg = (int)GameMath.Clamp((float)(len * 2.5f), 5, 40);
            Vec3d[] pts = new Vec3d[seg + 1];
            pts[0] = new Vec3d(sx, sy, sz);
            pts[seg] = new Vec3d(ex, ey, ez);

            for (int i = 1; i < seg; i++)
            {
                double t = (double)i / seg;
                double taper = Math.Sin(t * Math.PI); // 0 на концах, 1 в середине — концы точно сходятся
                double bx = sx + dx * t, by = sy + dy * t, bz = sz + dz * t;
                double o1 = (rnd.NextDouble() * 2 - 1) * jitter * taper;
                double o2 = (rnd.NextDouble() * 2 - 1) * jitter * taper;
                pts[i] = new Vec3d(bx + p1x * o1 + p2x * o2, by + p1y * o1 + p2y * o2, bz + p1z * o1 + p2z * o2);
            }
            return pts;
        }

        public void Render(float dt, IStandardShaderProgram prog, IRenderAPI render, Matrixf modelMat, MeshRef cube, Vec3d camPos)
        {
            age += dt;

            float lifeT = age / maxAge;
            float alpha = lifeT < 0.6f ? 1f : Math.Max(0f, 1f - (lifeT - 0.6f) / 0.4f); // плавное затухание
            float flicker = 0.75f + 0.25f * (float)Math.Abs(Math.Sin(age * 55f));        // мерцание
            alpha *= flicker;

            // Внешнее свечение (толще, бирюзовое — цвет Гайи) + яркое белое ядро
            DrawStrands(prog, render, modelMat, cube, camPos, 0.06f, new Vec4f(0f, 0.85f, 0.80f, alpha * 0.5f), 180);
            DrawStrands(prog, render, modelMat, cube, camPos, 0.02f, new Vec4f(0.8f, 1f, 0.97f, alpha), 255);
        }

        private void DrawStrands(IStandardShaderProgram prog, IRenderAPI render, Matrixf modelMat, MeshRef cube, Vec3d camPos, float thick, Vec4f color, int glow)
        {
            prog.RgbaTint = color;
            prog.ExtraGlow = glow;

            foreach (Vec3d[] pts in strands)
            {
                for (int i = 0; i < pts.Length - 1; i++)
                {
                    Vec3d a = pts[i];
                    Vec3d b = pts[i + 1];
                    double sx = b.X - a.X, sy = b.Y - a.Y, sz = b.Z - a.Z;
                    double segLen = Math.Sqrt(sx * sx + sy * sy + sz * sz);
                    if (segLen < 1e-5) continue;
                    double dirx = sx / segLen, diry = sy / segLen, dirz = sz / segLen;

                    modelMat.Identity();
                    modelMat.Translate(
                        (a.X + b.X) * 0.5 - camPos.X,
                        (a.Y + b.Y) * 0.5 - camPos.Y,
                        (a.Z + b.Z) * 0.5 - camPos.Z);
                    AlignX(modelMat, dirx, diry, dirz);
                    modelMat.Scale((float)(segLen * 0.5), thick, thick); // куб -1..1 -> длина segLen по X

                    prog.ModelMatrix = modelMat.Values;
                    render.RenderMesh(cube);
                }
            }
        }

        // Поворачивает локальную ось +X так, чтобы она смотрела вдоль (tx,ty,tz)
        private void AlignX(Matrixf m, double tx, double ty, double tz)
        {
            // ось = (1,0,0) x dir = (0, -tz, ty)
            double axisX = 0, axisY = -tz, axisZ = ty;
            double s = Math.Sqrt(axisX * axisX + axisY * axisY + axisZ * axisZ);
            double c = tx; // dot((1,0,0), dir)

            if (s < 1e-6)
            {
                if (c < 0) Mat4f.Rotate(m.Values, m.Values, (float)Math.PI, new float[] { 0, 1, 0 });
                return;
            }

            axisX /= s; axisY /= s; axisZ /= s;
            float ang = (float)Math.Atan2(s, c);
            Mat4f.Rotate(m.Values, m.Values, ang, new float[] { (float)axisX, (float)axisY, (float)axisZ });
        }
    }
}