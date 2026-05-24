using System;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BotaniaStoryMod : ModSystem
    {
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterBlockEntityClass("BotaniaFloatingIsland", typeof(BlockEntityFloatingIsland));
        }
    }

    // 2. Сущность блока и Рендерер
    public class BlockEntityFloatingIsland : BlockEntity, IRenderer
    {
        private ICoreClientAPI capi;
        private MeshRef meshRef;
        private int atlasPageId;
        private Matrixf modelMat = new Matrixf();

        // Очередь отрисовки: Opaque 
        public double RenderOrder => 0.5;
        public int RenderRange => 24; // Дальность прорисовки анимации в блоках

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Анимация имеет смысл только на стороне клиента
            if (api is ICoreClientAPI clientApi)
            {
                this.capi = clientApi;
                // Заставляем игру вызывать OnRenderFrame каждый кадр
                capi.Event.RegisterRenderer(this, EnumRenderStage.Opaque, "botaniafloatingisland");

                MeshData mesh = capi.TesselatorManager.GetDefaultBlockMesh(Block);
                if (mesh != null)
                {
                    // Загружаем её в видеопамять
                    meshRef = capi.Render.UploadMesh(mesh);
                }
                // По умолчанию ставим нулевую страницу
                this.atlasPageId = capi.BlockTextureAtlas.AtlasTextures[0].TextureId;

                // Пытаемся найти фактическую страницу атласа, на которую попала текстура этого блока
                if (Block.Textures != null && Block.Textures.Count > 0)
                {
                    var firstTexture = Block.Textures.Values.First();
                    if (firstTexture?.Baked != null)
                    {
                        int texSubId = firstTexture.Baked.TextureSubId;
                        // Получаем реальный OpenGL ID текстуры нужной страницы атласа
                        this.atlasPageId = capi.BlockTextureAtlas.Positions[texSubId].atlasTextureId;
                    }
                }
            }
        }

        // Отключаем стандартную отрисовку блока движком чанка.
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            return true;
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (meshRef == null || capi == null) return;

            IRenderAPI render = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            // Время в секундах
            float time = capi.World.ElapsedMilliseconds / 1000f;

            // Скорость вращения
            float rotationY = time * 0.25f;

            // Вверх-вниз
            float bobbingY = (float)Math.Sin(time * 1.5f) * 0.08f;

            // Колыхание на "воде"
            float swayX = (float)Math.Sin(time * 0.8f) * 0.06f;
            float swayZ = (float)Math.Cos(time * 0.9f) * 0.06f;

            // КОРРЕКТИРОВКА ЦЕНТРА
            float offsetX = 0.0f; 
            float offsetY = 0.0f; 
            float offsetZ = 0.0f; 

            IStandardShaderProgram prog = render.PreparedStandardShader(Pos.X, Pos.Y, Pos.Z);

            // Биндим правильную страницу атласа
            render.BindTexture2d(this.atlasPageId);

            modelMat.Identity();

            modelMat.Translate(Pos.X - camPos.X + offsetX, Pos.Y - camPos.Y + offsetY, Pos.Z - camPos.Z + offsetZ);

            // Смещаемся в центр вращения (0.5, 0.5, 0.5 - это середина блока 16x16x16)
            modelMat.Translate(0.5f, 0.5f + bobbingY, 0.5f);

            //  Применяем вращения
            modelMat.RotateX(swayX);
            modelMat.RotateZ(swayZ);
            modelMat.RotateY(rotationY);

            // Возвращаемся из центра обратно, чтобы отрисовать меш
            modelMat.Translate(-0.5f, -0.5f, -0.5f);

            // Передаем итоговую матрицу в шейдер
            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            // Рисуем!
            render.RenderMesh(meshRef);

            prog.Stop();
        }

        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Dispose(); 
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            Dispose(); 
        }

        public void Dispose()
        {
            if (capi != null)
            {
                capi.Event.UnregisterRenderer(this, EnumRenderStage.Opaque);
                meshRef?.Dispose();
                meshRef = null;
            }
        }
    }
}