using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;

namespace BotaniaStory.systems
{
    public class BehaviorAnimatedItem : CollectibleBehavior
    {
        private MultiTextureMeshRef[] frames;
        private int totalFrames = 1;
        private ICoreClientAPI capi;
        private int animationSpeedMs = 200;

        // Словарь для хранения максимального количества кадров каждой группы деталей
        private Dictionary<string, int> maxFramesPerGroup = new Dictionary<string, int>();

        public BehaviorAnimatedItem(CollectibleObject collObj) : base(collObj) { }

        public override void Initialize(JsonObject properties)
        {
            base.Initialize(properties);
            animationSpeedMs = properties["speed"].AsInt(200);
        }

        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
            if (api.Side == EnumAppSide.Client)
            {
                capi = api as ICoreClientAPI;
                GenerateMeshes();
            }
        }

        private void GenerateMeshes()
        {
            CompositeShape compShape = (collObj as Item)?.Shape ?? (collObj as Block)?.Shape;
            if (compShape == null) return;

            AssetLocation shapePath = compShape.Base.Clone().WithPathPrefixOnce("shapes/").WithPathAppendixOnce(".json");
            Shape baseShape = capi.Assets.TryGet(shapePath)?.ToObject<Shape>();

            if (baseShape == null) return;

            // 1. Высчитываем идеальную длину общего цикла (НОК всех групп)
            totalFrames = CalculateTotalFrames(baseShape.Elements);

            // Предохранитель от слишком тяжелых моделей
            if (totalFrames > 60)
            {
                capi.Logger.Warning("BehaviorAnimatedItem: Слишком много кадров ({0}) сгенерировано для {1}. Осторожно с памятью!", totalFrames, collObj.Code);
            }

            frames = new MultiTextureMeshRef[totalFrames];

            ITexPositionSource texSource;
            if (collObj is Item item) texSource = capi.Tesselator.GetTextureSource(item);
            else if (collObj is Block block) texSource = capi.Tesselator.GetTextureSource(block);
            else return;

            // 2. Генерируем меши, смешивая кадры разных групп
            for (int i = 1; i <= totalFrames; i++)
            {
                Shape shapeClone = baseShape.Clone();
                shapeClone.Elements = FilterElementsForFrame(shapeClone.Elements, i);

                capi.Tesselator.TesselateShape(collObj.Code.ToString(), shapeClone, out MeshData meshData, texSource);
                frames[i - 1] = capi.Render.UploadMultiTextureMesh(meshData);
            }
        }

        // --- НОВАЯ ЛОГИКА ГРУПП И КАДРОВ ---

        private string GetBaseName(string name)
        {
            if (string.IsNullOrEmpty(name)) return "";
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                return name.Substring(0, lastUnderscore); // Возвращает "ball" из "ball_4"
            }
            return name;
        }

        private int ExtractFrameNumber(string name)
        {
            if (string.IsNullOrEmpty(name)) return 0;
            int lastUnderscore = name.LastIndexOf('_');
            if (lastUnderscore >= 0 && lastUnderscore < name.Length - 1)
            {
                string numPart = name.Substring(lastUnderscore + 1);
                if (int.TryParse(numPart, out int frameNum)) return frameNum;
            }
            return 0;
        }

        private void PopulateMaxFrames(ShapeElement[] elements)
        {
            if (elements == null) return;
            foreach (var el in elements)
            {
                string baseName = GetBaseName(el.Name);
                int frame = ExtractFrameNumber(el.Name);

                if (frame > 0)
                {
                    if (!maxFramesPerGroup.ContainsKey(baseName) || frame > maxFramesPerGroup[baseName])
                    {
                        maxFramesPerGroup[baseName] = frame;
                    }
                }

                if (el.Children != null) PopulateMaxFrames(el.Children);
            }
        }

        private int CalculateTotalFrames(ShapeElement[] elements)
        {
            maxFramesPerGroup.Clear();
            PopulateMaxFrames(elements);

            int lcm = 1;
            foreach (var maxF in maxFramesPerGroup.Values)
            {
                if (maxF > 0) lcm = LCM(lcm, maxF);
            }
            return lcm > 0 ? lcm : 1;
        }

        // Математика Наименьшего общего кратного (НОК)
        private int GCD(int a, int b) { while (b != 0) { int temp = b; b = a % b; a = temp; } return a; }
        private int LCM(int a, int b) { return (a / GCD(a, b)) * b; }

        // --- УМНАЯ ФИЛЬТРАЦИЯ ---

        private ShapeElement[] FilterElementsForFrame(ShapeElement[] elements, int targetFrame)
        {
            if (elements == null) return null;
            List<ShapeElement> filtered = new List<ShapeElement>();
            foreach (var el in elements)
            {
                int elFrame = ExtractFrameNumber(el.Name);
                if (elFrame > 0)
                {
                    string baseName = GetBaseName(el.Name);
                    int maxGroupFrames = maxFramesPerGroup.ContainsKey(baseName) ? maxFramesPerGroup[baseName] : 1;

                    // Магия здесь: вычисляем ЛОКАЛЬНЫЙ кадр для этой конкретной детали
                    int expectedLocalFrame = ((targetFrame - 1) % maxGroupFrames) + 1;

                    // Если это не нужный локальный кадр - скрываем деталь
                    if (elFrame != expectedLocalFrame) continue;
                }

                if (el.Children != null) el.Children = FilterElementsForFrame(el.Children, targetFrame);
                filtered.Add(el);
            }
            return filtered.ToArray();
        }

        public override void OnBeforeRender(ICoreClientAPI capi, ItemStack itemstack, EnumItemRenderTarget target, ref ItemRenderInfo renderinfo)
        {
            base.OnBeforeRender(capi, itemstack, target, ref renderinfo);

            if (frames == null || frames[0] == null) return;

            int currentFrame = (int)((capi.World.ElapsedMilliseconds / animationSpeedMs) % totalFrames);
            renderinfo.ModelRef = frames[currentFrame];
        }
    }
}