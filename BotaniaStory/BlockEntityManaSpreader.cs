using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityManaSpreader : BlockEntity
    {
        // Углы поворота
        public float Yaw = 0f;
        public float Pitch = 0f;

        // Внутренняя батарейка распространителя
        public int CurrentMana = 0;
        public int MaxMana = 1000; // Вмещает 1000 маны за раз

        // Координаты цели (Бассейна), к которому он привязан
        public BlockPos TargetPos = null;

        // Сохраняем все данные
        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetFloat("yaw", Yaw);
            tree.SetFloat("pitch", Pitch);
            tree.SetInt("mana", CurrentMana);

            if (TargetPos != null)
            {
                tree.SetInt("tgtX", TargetPos.X);
                tree.SetInt("tgtY", TargetPos.Y);
                tree.SetInt("tgtZ", TargetPos.Z);
                tree.SetBool("hasTarget", true);
            }
            else
            {
                tree.SetBool("hasTarget", false);
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            Yaw = tree.GetFloat("yaw", 0f);
            Pitch = tree.GetFloat("pitch", 0f);
            CurrentMana = tree.GetInt("mana", 0);

            if (tree.GetBool("hasTarget"))
            {
                TargetPos = new BlockPos(tree.GetInt("tgtX"), tree.GetInt("tgtY"), tree.GetInt("tgtZ"));
            }
            else
            {
                TargetPos = null;
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                MarkDirty(true);
            }
        }

        // ==========================================
        // МАГИЯ ДИНАМИЧЕСКОГО ВРАЩЕНИЯ 3D-МОДЕЛИ
        // ==========================================
        public override bool OnTesselation(ITerrainMeshPool mesher, ITesselatorAPI tesselator)
        {
            // Загружаем нашу 3D модель
            AssetLocation shapeLoc = new AssetLocation("botaniastory", "shapes/block/manaspreader.json");
            Shape shape = Api.Assets.TryGet(shapeLoc)?.ToObject<Shape>();

            if (shape != null)
            {
                MeshData mesh;
                tesselator.TesselateShape(Block, shape, out mesh);

                // Создаем правильную матрицу вращения
                Matrixf matrix = new Matrixf();

                // Цепочка трансформаций: сдвигаем в центр -> крутим -> возвращаем обратно
                matrix.Translate(0.5f, 0.5f, 0.5f)
                      .RotateY(Yaw)
                      .RotateX(Pitch)
                      .Translate(-0.5f, -0.5f, -0.5f);

                // Применяем вращение к сетке блока (используем внутренний массив .Values)
                mesh.MatrixTransform(matrix.Values);

                // Добавляем нашу повернутую модель в мир
                mesher.AddMeshData(mesh);
            }

            // Возвращаем false! Это скажет игре: "НЕ рисуй стандартный неподвижный блок из JSON, я нарисовал его сам!"
            return true;
        }
    }
}