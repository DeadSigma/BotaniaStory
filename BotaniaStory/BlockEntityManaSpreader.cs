using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
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
        public int MaxMana = 100000; // Вмещает 1000 маны за раз

        // Координаты цели (Бассейна), к которому он привязан
        public BlockPos TargetPos = null;

        // Сохраняем все данные

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);
            // Запускаем логику передачи ТОЛЬКО на сервере (10 раз в секунду)
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 100);
            }
        }

        private void OnServerTick(float dt)
        {
            // ==========================================
            // 0. ПРОВЕРКА СУЩЕСТВУЮЩЕЙ ЦЕЛИ
            // ==========================================
            if (TargetPos != null)
            {
                // Проверяем, стоит ли еще на месте цели Бассейн
                BlockEntity targetBlock = Api.World.BlockAccessor.GetBlockEntity(TargetPos);
                if (!(targetBlock is BlockEntityManaPool))
                {
                    TargetPos = null; // Бассейн сломали - сбрасываем цель!
                    MarkDirty(true);
                }
            }

            // ==========================================
            // 1. УМНЫЙ РАДАР (Работает, если нет цели)
            // ==========================================
            if (TargetPos == null)
            {
                // Восстанавливаем 3D-вектор направления дула
                double dy = Math.Sin(Pitch);
                double distanceXZ = Math.Cos(Pitch);
                double dx = -Math.Sin(Yaw) * distanceXZ;
                double dz = -Math.Cos(Yaw) * distanceXZ;

                // Пускаем невидимый луч
                for (float i = 1f; i <= 12f; i += 0.5f)
                {
                    int cx = (int)Math.Floor(Pos.X + 0.5 + dx * i);
                    int cy = (int)Math.Floor(Pos.Y + 0.5 + dy * i);
                    int cz = (int)Math.Floor(Pos.Z + 0.5 + dz * i);
                    BlockPos checkPos = new BlockPos(cx, cy, cz);

                    Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                    // Если луч наткнулся на Бассейн - привязываемся!
                    if (hitBlock is BlockManaPool)
                    {
                        TargetPos = checkPos.Copy();
                        MarkDirty(true);
                        break;
                    }
                    else if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                    {
                        break; // Врезались в стену
                    }
                }
            }

            // ==========================================
            // 2. ПЕРЕДАЧА МАНЫ И ВЫСТРЕЛ
            // ==========================================
            // Если цели все еще нет или маны мало - отменяем выстрел
            if (TargetPos == null || CurrentMana < 100) return;

            int burstMana = Math.Min(CurrentMana, 2000);

            // НАХОДИМ ТИП СУЩНОСТИ "manaburst"
            EntityProperties type = Api.World.GetEntityType(new AssetLocation("botaniastory", "manaburst"));
            if (type == null) return;

            // СОЗДАЕМ СГУСТОК И ЗАРЯЖАЕМ МАНОЙ
            EntityManaBurst burstEntity = (EntityManaBurst)Api.World.ClassRegistry.CreateEntity(type);
            burstEntity.ManaPayload = burstMana;
            burstEntity.SourcePos = Pos.Copy();

            // СТАВИМ В ЦЕНТР ДУЛА
            burstEntity.Pos.SetPos(new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5));

            // МАТЕМАТИКА ПРИЦЕЛИВАНИЯ И ВЫСТРЕЛ
            Vec3d targetCenter = new Vec3d(TargetPos.X + 0.5, TargetPos.Y + 0.5, TargetPos.Z + 0.5);
            Vec3d direction = (targetCenter - burstEntity.Pos.XYZ).Normalize();

            // Задаем скорость полета
            burstEntity.Pos.Motion.Set(direction.X * 0.3, direction.Y * 0.3, direction.Z * 0.3);

            // ВЫПУСКАЕМ В МИР!
            Api.World.SpawnEntity(burstEntity);

            this.CurrentMana -= burstMana;
            this.MarkDirty(true);
        }


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
                tree.SetBool("hasTarget", true); // Говорим: Связь есть!
            }
            else
            {
                tree.SetBool("hasTarget", false); // Говорим: Забудь координаты!
            }
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);

            // Запоминаем старые углы ПЕРЕД обновлением
            float oldYaw = Yaw;
            float oldPitch = Pitch;

            Yaw = tree.GetFloat("yaw", 0f);
            Pitch = tree.GetFloat("pitch", 0f);
            CurrentMana = tree.GetInt("mana", 0);

            // Клиент проверяет, есть ли связь
            if (tree.GetBool("hasTarget"))
            {
                TargetPos = new BlockPos(tree.GetInt("tgtX"), tree.GetInt("tgtY"), tree.GetInt("tgtZ"));
            }
            else
            {
                TargetPos = null; // Принудительно очищаем память от призраков!
            }

            if (Api?.Side == EnumAppSide.Client)
            {
                // ПЕРЕРИСОВЫВАЕМ 3D-МОДЕЛЬ ТОЛЬКО ЕСЛИ ИЗМЕНИЛСЯ УГОЛ ПОВОРОТА!
                // Если изменилась только мана - не трогаем блок, чтобы хитбокс не "моргал"
                if (Yaw != oldYaw || Pitch != oldPitch)
                {
                    MarkDirty(true);
                }
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