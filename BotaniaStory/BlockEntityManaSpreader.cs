using botaniastory;
using System;
using System.Text;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

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

        private bool isDischarging = false; // Находимся ли мы в процессе отдачи маны
        private long lastFireMs = 0; // Время последнего выстрела
        private int fireCooldownMs = 500; // Пауза между выстрелами в миллисекундах (1.5 секунды)
        private int burstManaAmount = 120; // Маленькая порция маны для постепенной передачи


        public BlockEntityAnimationUtil animUtil;

        private SpreaderCoreRenderer coreRenderer;

        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(OnServerTick, 100);
            }

            // РЕГИСТРИРУЕМ РЕНДЕР ТОЛЬКО НА КЛИЕНТЕ
            if (api.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = (ICoreClientAPI)api;
                coreRenderer = new SpreaderCoreRenderer(capi, Pos, this);
                capi.Event.RegisterRenderer(coreRenderer, EnumRenderStage.Opaque, "botaniastory");
            }
        }

        // ВАЖНО: Не забываем удалять рендер, когда блок ломают!
        public override void OnBlockRemoved()
        {
            base.OnBlockRemoved();

            if (Api is ICoreClientAPI capi && coreRenderer != null)
            {
                capi.Event.UnregisterRenderer(coreRenderer, EnumRenderStage.Opaque);
                coreRenderer.Dispose();
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

            // 1. Проверяем порог в 30% (30 000 из 100 000)
            int threshold = (int)(MaxMana * 0.30f);

            // Если накопили 30% — начинаем разрядку
            if (CurrentMana >= threshold)
            {
                isDischarging = true;
            }
            // Если маны не хватает даже на один маленький сгусток — прекращаем стрелять
            if (CurrentMana < burstManaAmount)
            {
                isDischarging = false;
            }
            
            // Если мы не в режиме разрядки или цель потеряна — отменяем выстрел
            if (!isDischarging || TargetPos == null) return;

            // 2. Проверяем задержку (кулдаун), чтобы стрелял постепенно, а не пулеметом
            long currentMs = Api.World.ElapsedMilliseconds;
            if (currentMs - lastFireMs < fireCooldownMs) return;


            // ==========================================
            // 3. ПРОВЕРКА ПРЕПЯТСТВИЙ (Line of Sight)
            // ==========================================
            Vec3d startPos = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5);
            Vec3d targetCenter = new Vec3d(TargetPos.X + 0.5, TargetPos.Y + 0.5, TargetPos.Z + 0.5);

            double distance = startPos.DistanceTo(targetCenter);
            Vec3d direction = (targetCenter - startPos).Normalize();

            bool isBlocked = false;

            // Шагаем по невидимому лучу от дула к бассейну с шагом 0.5 блока
            for (float step = 0.5f; step < distance - 0.2f; step += 0.5f)
            {
                BlockPos checkPos = new BlockPos(
                    (int)Math.Floor(startPos.X + direction.X * step),
                    (int)Math.Floor(startPos.Y + direction.Y * step),
                    (int)Math.Floor(startPos.Z + direction.Z * step)
                );

                // Игнорируем сам распространитель, если луч задевает его край
                if (checkPos.Equals(Pos)) continue;

                Block hitBlock = Api.World.BlockAccessor.GetBlock(checkPos);

                // Если блок не пустой воздух и у него есть хитбоксы (игнорируем высокую траву, воду и т.д.)
                if (hitBlock.Id != 0 && hitBlock.CollisionBoxes != null && hitBlock.CollisionBoxes.Length > 0)
                {
                    // Если мы дошли до бассейна - значит путь чист, прекращаем проверку
                    if (checkPos.Equals(TargetPos) || hitBlock is BlockManaPool)
                    {
                        break;
                    }

                    // Если это любой другой твердый блок — путь заблокирован!
                    isBlocked = true;
                    break;
                }
            }

            // Если нашли преграду - откладываем выстрел (таймер не сбрасывается, ждем пока уберут блок)
            if (isBlocked) return;


            // ==========================================
            // 4. СОЗДАНИЕ И ЗАПУСК СГУСТКА МАНЫ
            // ==========================================
            // НАХОДИМ ТИП СУЩНОСТИ "manaburst"
            EntityProperties type = Api.World.GetEntityType(new AssetLocation("botaniastory", "manaburst"));
            if (type == null) return;

            // СОЗДАЕМ СГУСТОК И ЗАРЯЖАЕМ МАНОЙ
            EntityManaBurst burstEntity = (EntityManaBurst)Api.World.ClassRegistry.CreateEntity(type);
            burstEntity.ManaPayload = burstManaAmount; // Передаем только 150 маны!
            burstEntity.SourcePos = Pos.Copy();

            // === НАСТРОЙКА ДАЛЬНОСТИ ПОЛЕТА ===
            burstEntity.WatchedAttributes.SetDouble("maxDist", 8.0);

            // 1. СТАВИМ В ЦЕНТР ДУЛА (Обязательно обновляем Pos, иначе движок удалит сущность!)
            // Переменную startPos мы уже создали выше, используем её
            burstEntity.Pos.SetPos(startPos);
            burstEntity.Pos.SetFrom(burstEntity.Pos);

            // Передаем клиенту точные стартовые координаты для правильного расчета дистанции
            burstEntity.WatchedAttributes.SetDouble("startX", startPos.X);
            burstEntity.WatchedAttributes.SetDouble("startY", startPos.Y);
            burstEntity.WatchedAttributes.SetDouble("startZ", startPos.Z);

            // 2. ЗАДАЕМ СКОРОСТЬ (direction тоже уже просчитан выше, экономим ресурсы)
            double motionX = direction.X * 0.15;
            double motionY = direction.Y * 0.15;
            double motionZ = direction.Z * 0.15;

            // Обновляем и локальную, и серверную скорость
            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);
            burstEntity.Pos.Motion.Set(motionX, motionY, motionZ);

            // 4. ПЕРЕДАЕМ СКОРОСТЬ КЛИЕНТУ (Чтобы он сам плавно двигал искру между тиками сервера)
            burstEntity.WatchedAttributes.SetDouble("motionX", motionX);
            burstEntity.WatchedAttributes.SetDouble("motionY", motionY);
            burstEntity.WatchedAttributes.SetDouble("motionZ", motionZ);

            // ВЫПУСКАЕМ В МИР!
            Api.World.SpawnEntity(burstEntity);

            // ... (звук и списание маны)

            // ==========================================
            // ОТПРАВКА СЕТЕВОГО ПАКЕТА (ЗВУК ВЫСТРЕЛА)
            // ==========================================
            ICoreServerAPI sapi = Api as ICoreServerAPI;
            IServerNetworkChannel channel = sapi.Network.GetChannel("botanianetwork");

            PlayManaSoundPacket soundMessage = new PlayManaSoundPacket()
            {
                Position = new Vec3d(Pos.X + 0.5, Pos.Y + 0.5, Pos.Z + 0.5),
                SoundName = "manaspreaderfire" // Точное имя аудиофайла выстрела
            };

            channel.BroadcastPacket(soundMessage);

            // Обновляем таймер и списываем ману
            lastFireMs = currentMs;
            this.CurrentMana -= burstManaAmount;
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
        // ==========================================
        // ИНТЕРФЕЙС ПРИ НАВЕДЕНИИ (HUD)
        // ==========================================
        public override void GetBlockInfo(IPlayer forPlayer, StringBuilder dsc)
        {
            base.GetBlockInfo(forPlayer, dsc);

            // Получаем предмет, который игрок держит в руке
            Item activeItem = forPlayer.InventoryManager.ActiveHotbarSlot?.Itemstack?.Item;

            // Проверяем, является ли предмет Посохом Леса и зажат ли Shift
            bool holdsWand = activeItem is ItemWandOfTheForest;
            bool isSneaking = forPlayer.Entity.Controls.Sneak;

            // Показываем ману ТОЛЬКО если условия выполнены
            if (holdsWand && isSneaking)
            {
                // Заодно делаем проверку: привязан ли распространитель к чему-то
                string linkStatus = TargetPos != null ? "Привязан" : "Не привязан";

                // Добавляем строчку в интерфейс
                dsc.AppendLine($"{CurrentMana} / {MaxMana} [{linkStatus}]");
            }
        }
    }
}