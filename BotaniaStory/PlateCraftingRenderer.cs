using OpenTK.Graphics.OpenGL;
using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class PlateCraftingRenderer : IRenderer
    {
        private ICoreClientAPI capi;
        private BlockPos pos;
        private BlockEntityTerrestrialPlate plate;

        private MeshRef quadMeshRef;
        private LoadedTexture particleTexture;
        public Matrixf ModelMat = new Matrixf();

        private MeshRef[] itemMeshRefs = new MeshRef[3];
        private ItemStack[] lastRenderedStacks = new ItemStack[3];

        private float currentProgress = 0f;
        private float rotationAngle = 0f;

        public double RenderOrder => 0.5;
        public int RenderRange => 64;

        // Внутренний класс для хранения разлетающихся частиц
        private class ExplosionParticle
        {
            public Vec3d Position;
            public Vec3d Velocity;
            public float Size;
            public float Life;
            public float MaxLife;
        }
        private System.Collections.Generic.List<ExplosionParticle> explosionParticles = new System.Collections.Generic.List<ExplosionParticle>();

        // Метод генерации искр
        public void TriggerExplosion()
        {
            Random rand = capi.World.Rand;
            for (int i = 0; i < 40; i++) // 40 частиц маны
            {
                ExplosionParticle p = new ExplosionParticle();
                p.Position = new Vec3d(pos.X + 0.5, pos.Y + 0.3, pos.Z + 0.5); // Старт из центра

                // Формула сферического разлета во все стороны
                double theta = rand.NextDouble() * Math.PI * 2;
                double phi = Math.Acos(2 * rand.NextDouble() - 1);
                double speed = 1.0 + rand.NextDouble() * 3.0; // Скорость вылета

                p.Velocity = new Vec3d(
                    Math.Sin(phi) * Math.Cos(theta) * speed,
                    Math.Abs(Math.Cos(phi)) * speed + 2.0, // Всегда летят немного вверх
                    Math.Sin(phi) * Math.Sin(theta) * speed
                );

                p.Size = 0.3f + (float)rand.NextDouble() * 0.5f; // Случайный размер
                p.MaxLife = 0.5f + (float)rand.NextDouble() * 0.8f; // Живут от 0.5 до 1.3 секунд
                p.Life = p.MaxLife;

                explosionParticles.Add(p);
            }
        }

        public PlateCraftingRenderer(ICoreClientAPI api, BlockPos pos, BlockEntityTerrestrialPlate plate)
        {
            this.capi = api;
            this.pos = pos;
            this.plate = plate;
            LoadGraphics();
        }

        private void LoadGraphics()
        {
            particleTexture = new LoadedTexture(capi);
            capi.Render.GetOrLoadTexture(new AssetLocation("botaniastory", "textures/particle/mana_particle.png"), ref particleTexture);

            MeshData quad = QuadMeshUtil.GetCustomQuadModelData(-0.5f, -0.5f, 0, 1f, 1f);
            quad.Rgba = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255 };
            quadMeshRef = capi.Render.UploadMesh(quad);
        }

        public void UpdateProgress(float progress)
        {
            currentProgress = progress;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            // Проверяем, есть ли хотя бы один предмет на плите
            bool hasItems = !plate.inventory[0].Empty || !plate.inventory[1].Empty || !plate.inventory[2].Empty;

            // Если плита пустая, крафт не идет и взрыва нет — вообще не напрягаем видеокарту
            if (!hasItems && currentProgress <= 0 && explosionParticles.Count == 0) return;

            rotationAngle += deltaTime * 3f;

            IClientPlayer player = capi.World.Player;
            Vec3d camPos = player.Entity.CameraPos;

            IStandardShaderProgram prog = capi.Render.PreparedStandardShader((int)pos.X, (int)pos.Y, (int)pos.Z);

            // ==========================================================
            // 1. РЕНДЕР ПРЕДМЕТОВ (ВСЕГДА, ЕСЛИ ОНИ ЕСТЬ)
            // ==========================================================
            if (hasItems)
            {
                // Настройки для плотных физических предметов
                GL.DepthMask(true);
                capi.Render.GlToggleBlend(true, EnumBlendMode.Standard);

                for (int i = 0; i < 3; i++)
                {
                    ItemSlot slot = plate.inventory[i];
                    if (slot.Empty)
                    {
                        itemMeshRefs[i]?.Dispose();
                        itemMeshRefs[i] = null;
                        lastRenderedStacks[i] = null;
                        continue;
                    }

                    if (lastRenderedStacks[i] != slot.Itemstack)
                    {
                        itemMeshRefs[i]?.Dispose();
                        // Тесселируем модель предмета
                        capi.Tesselator.TesselateItem(slot.Itemstack.Item, out MeshData mesh);
                        itemMeshRefs[i] = capi.Render.UploadMesh(mesh);
                        lastRenderedStacks[i] = slot.Itemstack;
                    }

                    if (itemMeshRefs[i] != null)
                    {
                        // Умный выбор атласа: если предмет это блок, берем атлас блоков. Иначе - атлас предметов.
                        int atlasId = slot.Itemstack.Class == EnumItemClass.Block
                            ? capi.BlockTextureAtlas.AtlasTextures[0].TextureId
                            : capi.ItemTextureAtlas.AtlasTextures[0].TextureId;

                        // ЖЕСТКО сбрасываем активный слот текстуры на 0 перед применением
                        GL.ActiveTexture(TextureUnit.Texture0);
                        capi.Render.BindTexture2d(atlasId);

                        // Замедлили полет по кругу (умножение на 0.3f)
                        float offsetAngle = (rotationAngle * 0.15f) + (i * (float)(Math.PI * 2 / 3));
                        float itemRadius = 0.4f;

                        double ix = pos.X + 0.5 + Math.Sin(offsetAngle) * itemRadius;
                        // Плавное покачивание вверх-вниз (асинхронно за счет + i * 2f)
                        double iy = pos.Y + 0.65 + Math.Sin(rotationAngle * 0.8f + (i * 2f)) * 0.04;
                        double iz = pos.Z + 0.5 + Math.Cos(offsetAngle) * itemRadius;

                        // Обычный, плотный цвет без свечения
                        prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
                        prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
                        prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
                        prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
                        prog.ExtraGlow = 0;

                        ModelMat.Identity();
                        ModelMat.Translate(ix - camPos.X, iy - camPos.Y, iz - camPos.Z);

                        // Вращаем предмет вокруг своей оси
                        ModelMat.RotateY(rotationAngle * 0.5f + i);
                        ModelMat.Scale(0.75f, 0.75f, 0.75f);

                        // ВОТ ЭТА СТРОЧКА ИСПРАВЛЯЕТ ТРАЕКТОРИЮ: 
                        // Сдвигаем меш предмета так, чтобы его физический центр совпадал с орбитой
                        ModelMat.Translate(-0.5f, -0.5f, -0.5f);

                        prog.ModelMatrix = ModelMat.Values;
                        prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
                        prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

                        capi.Render.RenderMesh(itemMeshRefs[i]);
                    }
                }
            }

            // ==========================================================
            // 2. РЕНДЕР МАГИИ (ПУЧКИ И ВЗРЫВ)
            // ==========================================================
            if (currentProgress > 0 || explosionParticles.Count > 0)
            {
                // Настройки для светящейся, полупрозрачной маны
                // Жестко сбрасываем слот на 0 для магических частиц
                GL.ActiveTexture(TextureUnit.Texture0);
                capi.Render.BindTexture2d(particleTexture.TextureId);
                prog.Uniform("alphaTest", 0f);
                capi.Render.GlToggleBlend(true, EnumBlendMode.Glow);
                GL.DepthMask(false); // Отключаем глубину, чтобы шарики сливались друг с другом

                // --- ОТРИСОВКА КЛАСТЕРОВ МАНЫ ---
                if (currentProgress > 0)
                {
                    float maxRadius = 2.2f;
                    float minRadius = 0.2f;
                    float currentRadius = maxRadius - ((maxRadius - minRadius) * currentProgress);

                    for (int i = 0; i < 3; i++)
                    {
                        float baseAngle = rotationAngle + (i * (float)(Math.PI * 2 / 3));

                        for (int j = 0; j < 5; j++)
                        {
                            float heightOffset = j == 0 ? 0 : (float)(Math.Cos(rotationAngle * 4 + j) * 0.2f);
                            float trailAngle = j == 0 ? 0 : -0.15f * j;
                            float wobbleRadius = j == 0 ? currentRadius : currentRadius + (float)(Math.Sin(rotationAngle * 3 + j * 2) * 0.2f);

                            double px = pos.X + 0.5 + Math.Sin(baseAngle + trailAngle) * wobbleRadius;
                            double py = pos.Y + 0.3 + heightOffset + (Math.Sin(rotationAngle * 2 + i) * 0.1);
                            double pz = pos.Z + 0.5 + Math.Cos(baseAngle + trailAngle) * wobbleRadius;

                            float alpha = j == 0 ? 0.9f : 0.4f;
                            Vec4f color = new Vec4f(0.1f, 1.0f, 0.2f, alpha);

                            prog.RgbaAmbientIn = new Vec3f(color.X, color.Y, color.Z);
                            prog.RgbaLightIn = color;
                            prog.RgbaGlowIn = color;
                            prog.RgbaTint = color;

                            ModelMat.Identity();
                            ModelMat.Translate(px - camPos.X, py - camPos.Y, pz - camPos.Z);
                            ModelMat.RotateY(player.CameraYaw);
                            ModelMat.RotateX(player.CameraPitch);

                            float baseSize = j == 0 ? 0.8f : 0.25f + (0.05f * j);
                            float size = baseSize + (float)(Math.Sin(rotationAngle * 5 + i + j) * 0.05);
                            ModelMat.Scale(size, size, size);

                            prog.ModelMatrix = ModelMat.Values;
                            prog.ViewMatrix = capi.Render.CameraMatrixOriginf;
                            prog.ProjectionMatrix = capi.Render.CurrentProjectionMatrix;

                            capi.Render.RenderMesh(quadMeshRef);
                        }
                    }
                }

                // --- ОТРИСОВКА ВЗРЫВА ---
                for (int i = explosionParticles.Count - 1; i >= 0; i--)
                {
                    var p = explosionParticles[i];
                    p.Life -= deltaTime;
                    if (p.Life <= 0)
                    {
                        explosionParticles.RemoveAt(i);
                        continue;
                    }

                    p.Position.X += p.Velocity.X * deltaTime;
                    p.Position.Y += p.Velocity.Y * deltaTime;
                    p.Position.Z += p.Velocity.Z * deltaTime;

                    p.Velocity.Y -= 3.0 * deltaTime;
                    p.Velocity.X *= 1.0 - (2.0 * deltaTime);
                    p.Velocity.Z *= 1.0 - (2.0 * deltaTime);

                    float lifeRatio = p.Life / p.MaxLife;
                    Vec4f color = new Vec4f(0.1f, 1.0f, 0.2f, lifeRatio * 0.8f);

                    prog.RgbaAmbientIn = new Vec3f(color.X, color.Y, color.Z);
                    prog.RgbaLightIn = color;
                    prog.RgbaGlowIn = color;
                    prog.RgbaTint = color;

                    ModelMat.Identity();
                    ModelMat.Translate(p.Position.X - camPos.X, p.Position.Y - camPos.Y, p.Position.Z - camPos.Z);
                    ModelMat.RotateY(player.CameraYaw);
                    ModelMat.RotateX(player.CameraPitch);

                    float currentSize = p.Size * lifeRatio;
                    ModelMat.Scale(currentSize, currentSize, currentSize);

                    prog.ModelMatrix = ModelMat.Values;
                    capi.Render.RenderMesh(quadMeshRef);
                }
            }

            // ==========================================================
            // 3. ЗАВЕРШЕНИЕ РЕНДЕРА (СБРОС НАСТРОЕК)
            // ==========================================================
            prog.RgbaAmbientIn = new Vec3f(1f, 1f, 1f);
            prog.RgbaLightIn = new Vec4f(1f, 1f, 1f, 1f);
            prog.RgbaGlowIn = new Vec4f(0f, 0f, 0f, 0f);
            prog.RgbaTint = new Vec4f(1f, 1f, 1f, 1f);
            prog.Stop();

            GL.DepthMask(true); // Обязательно возвращаем глубину для остального мира
            capi.Render.GlToggleBlend(false, EnumBlendMode.Standard);
        }

        public void Dispose()
        {
            quadMeshRef?.Dispose();

        }
    }
}