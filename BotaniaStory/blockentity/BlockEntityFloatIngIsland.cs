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
            // Важно: Строка "BotaniaFloatingIsland" должна строго совпадать с "entityClass" в JSON!
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

        // Очередь отрисовки: Opaque идеально подходит для наших 1-битных прозрачных текстур
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

                // Берем стандартную статичную модель твоего блока (с уже натянутыми текстурами)
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

        // ВАЖНО: Отключаем стандартную отрисовку блока движком чанка.
        // Если вернуть false, у тебя будет два островка: один статичный, а из него будет "выплывать" анимированный.
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

            // --- МАТЕМАТИКА АНИМАЦИИ ---
            // 1. Вращение замедлено в 2 раза (было 0.5f, стало 0.25f)
            float rotationY = time * 0.25f;

            // 2. Вверх-вниз слегка увеличено (было 0.05f, стало 0.08f)
            float bobbingY = (float)Math.Sin(time * 1.5f) * 0.08f;

            // 3. Колыхание на воде увеличено в 2 раза (было 0.03f, стало 0.06f)
            float swayX = (float)Math.Sin(time * 0.8f) * 0.06f;
            float swayZ = (float)Math.Cos(time * 0.9f) * 0.06f;

            // --- КОРРЕКТИРОВКА ЦЕНТРА ---
            // Если островок не по центру, меняй эти значения (например, 0.1f или -0.15f), 
            // пока он не встанет идеально. Это сдвинет саму модель в мире.
            float offsetX = 0.0f; // Сдвиг по оси X
            float offsetY = 0.0f; // Сдвиг по высоте (Y)
            float offsetZ = 0.0f; // Сдвиг по оси Z

            // Готовим шейдер
            IStandardShaderProgram prog = render.PreparedStandardShader(Pos.X, Pos.Y, Pos.Z);

            // Биндим правильную страницу атласа
            render.BindTexture2d(this.atlasPageId);

            modelMat.Identity();

            // 1. Смещаем координатную сетку к нашему блоку в мире + применяем твой ручной оффсет
            modelMat.Translate(Pos.X - camPos.X + offsetX, Pos.Y - camPos.Y + offsetY, Pos.Z - camPos.Z + offsetZ);

            // 2. Смещаемся в центр вращения (0.5, 0.5, 0.5 - это середина блока 16x16x16)
            modelMat.Translate(0.5f, 0.5f + bobbingY, 0.5f);

            // 3. Применяем вращения
            modelMat.RotateX(swayX);
            modelMat.RotateZ(swayZ);
            modelMat.RotateY(rotationY);

            // 4. Возвращаемся из центра обратно, чтобы отрисовать меш
            modelMat.Translate(-0.5f, -0.5f, -0.5f);

            // Передаем итоговую матрицу в шейдер
            prog.ModelMatrix = modelMat.Values;
            prog.ViewMatrix = render.CameraMatrixOriginf;
            prog.ProjectionMatrix = render.CurrentProjectionMatrix;

            // Рисуем!
            render.RenderMesh(meshRef);

            prog.Stop();
        }

        // Обязательная очистка памяти при разрушении блока
        // Обязательная очистка памяти при разрушении блока
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();
            Dispose(); // Вызываем новый стандартный метод
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
            Dispose(); // Вызываем новый стандартный метод
        }

        // Теперь это обязательный публичный метод, который требует интерфейс IDisposable
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