using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory.client.particles
{
    public class RunicLightningFlash : IDisposable
    {
        private MeshRef quadRef;
        private Vec4f color;
        private float linewidth;
        public List<Vec3d> points;
        public Vec3d origin;
        public float secondsAlive;
        public bool Alive = true;
        private ICoreClientAPI capi;
        private Random rand;

        public RunicLightningFlash(ICoreClientAPI capi, Vec3d startpoint)
        {
            this.capi = capi;
            this.rand = new Random();

            // ПОЛУПРОЗРАЧНОСТЬ: 4-е число (Альфа) теперь 0.3f (70% прозрачности)
            this.color = new Vec4f(0.2f, 1.0f, 0.9f, 0.3f);

            // ТОЛЩИНА
            this.linewidth = 0.02f;

            this.origin = startpoint.Clone();

            genPoints();
            genMesh(this.points);
        }

        protected void genPoints()
        {
            Vec3d pos = new Vec3d(0, 0, 0);
            this.points = new List<Vec3d>();

            // Маленькая, всего от 1.5 до 3 блоков вверх (как на скрине)
            float targetY = 1.5f + (float)rand.NextDouble() * 1.5f;

            while (pos.Y < targetY)
            {
                this.points.Add(pos.Clone());

                // Шаг по высоте (делаем изгибы более частыми)
                pos.Y += 0.3 + rand.NextDouble() * 0.4;

                // Делаем широкие изгибы в стороны для создания эффекта "зигзага"
                pos.X += (rand.NextDouble() - 0.5) * 1.2;
                pos.Z += (rand.NextDouble() - 0.5) * 1.2;
            }
            if (this.points.Count == 0) this.points.Add(pos.Clone());
        }

        protected void genMesh(List<Vec3d> points)
        {
            float[] data = new float[points.Count * 3];
            for (int i = 0; i < points.Count; i++)
            {
                data[i * 3] = (float)points[i].X;
                data[i * 3 + 1] = (float)points[i].Y;
                data[i * 3 + 2] = (float)points[i].Z;
            }
            quadRef?.Dispose();

            MeshData quadMesh = CubeMeshUtil.GetCube(0.5f, 0.5f, 0.5f, new Vec3f(0f, 0f, 0f));
            quadMesh.Flags = null;
            quadMesh.Rgba = null;

            // Передаем точки прямо в видеокарту
            quadMesh.CustomFloats = new CustomMeshDataPartFloat
            {
                Instanced = true,
                InterleaveOffsets = new int[] { 0, 12 },
                InterleaveSizes = new int[] { 3, 3 },
                InterleaveStride = 12,
                StaticDraw = false,
                Values = data,
                Count = data.Length
            };

            MeshData updateMesh = new MeshData(false);
            updateMesh.CustomFloats = quadMesh.CustomFloats;
            this.quadRef = capi.Render.UploadMesh(quadMesh);
            capi.Render.UpdateMesh(this.quadRef, updateMesh);
        }

        public void Render(float dt, IShaderProgram prog)
        {
            // Скорость "роста" молнии снизу вверх
            this.secondsAlive += dt * 1.0f;
            if (this.secondsAlive > 0.7f) this.Alive = false;

            prog.Uniform("color", this.color);
            prog.Uniform("lineWidth", this.linewidth);

            IClientPlayer plr = capi.World.Player;
            Vec3d camPos = plr.Entity.CameraPos;

            prog.Uniform("origin", (float)(this.origin.X - camPos.X), (float)(this.origin.Y - camPos.Y), (float)(this.origin.Z - camPos.Z));

            double cntRel = GameMath.Clamp(this.secondsAlive * 3f, 0f, 1f);
            int instanceCount = (int)(cntRel * this.points.Count) - 1;

            if (instanceCount > 0)
            {
                capi.Render.RenderMeshInstanced(this.quadRef, instanceCount);
            }
        }

        public void Dispose()
        {
            quadRef?.Dispose();
        }
    }
}