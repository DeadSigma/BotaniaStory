using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;

namespace BotaniaStory
{
    public class BlockEntityElvenGatewayCore : BlockEntity
    {
        public bool IsActive { get; private set; }
        public override void Initialize(ICoreAPI api)
        {
            base.Initialize(api);

            // Запускаем проверку структуры только на сервере каждые 500 мс (полсекунды)
            if (api.Side == EnumAppSide.Server)
            {
                RegisterGameTickListener(CheckStructureTick, 500);
            }
        }
        private void CheckStructureTick(float dt)
        {
            // Если портал не активен — нам нечего проверять, просто выходим
            if (!IsActive) return;

            // Если GetPortalOrientation вернул "none", значит рамка сломана
            if (GetPortalOrientation() == "none")
            {
                // Выключаем портал (метод сам уберет анимацию и сменит стейт ядра)
                Deactivate();

                // Опционально: можно добавить звук "провала" или поломки магии здесь
            }
        }
        private string GetPortalOrientation()
        {
            // Сначала пытаемся найти портал, построенный вдоль оси X (Запад-Восток)
            if (CheckAxis(true)) return "we";

            // Если по X не совпало, проверяем вдоль оси Z (Север-Юг)
            if (CheckAxis(false)) return "ns";

            // Если ни одна ось не совпала — структура неполная
            return "none";
        }

        private bool CheckAxis(bool isXAxis)
        {
            // Относительные координаты для 8 блоков обычного жизнедерева (X/Z, Y)
            int[,] livingwoodOffsets = new int[,]
            {
        { -1, 0 }, { 1, 0 },   // Нижний ряд (слева и справа от ядра)
        { -2, 1 }, { 2, 1 },   // Нижняя часть боковых столбов
        { -2, 3 }, { 2, 3 },   // Верхняя часть боковых столбов
        { -1, 4 }, { 1, 4 }    // Верхний ряд (слева и справа от центра)
            };

            // Относительные координаты для 3 блоков светящегося жизнедерева (X/Z, Y)
            int[,] glimmeringOffsets = new int[,]
            {
        { -2, 2 }, { 2, 2 },   // Центр боковых столбов
        { 0, 4 }               // Центр верхнего ряда
            };

            // 1. Проверяем обычное жизнедерево (8 блоков)
            for (int i = 0; i < livingwoodOffsets.GetLength(0); i++)
            {
                int dx = isXAxis ? livingwoodOffsets[i, 0] : 0;
                int dy = livingwoodOffsets[i, 1];
                int dz = isXAxis ? 0 : livingwoodOffsets[i, 0];

                BlockPos checkPos = Pos.AddCopy(dx, dy, dz);
                Block block = Api.World.BlockAccessor.GetBlock(checkPos);

                // Блок должен быть, и его код должен содержать "livingwood", но НЕ быть светящимся
                if (block == null || !block.Code.Path.Contains("livingwood") || block.Code.Path.Contains("glimmering"))
                {
                    return false; // Не хватает блока, прерываем проверку
                }
            }

            // 2. Проверяем светящееся жизнедерево (3 блока)
            for (int i = 0; i < glimmeringOffsets.GetLength(0); i++)
            {
                int dx = isXAxis ? glimmeringOffsets[i, 0] : 0;
                int dy = glimmeringOffsets[i, 1];
                int dz = isXAxis ? 0 : glimmeringOffsets[i, 0];

                BlockPos checkPos = Pos.AddCopy(dx, dy, dz);
                Block block = Api.World.BlockAccessor.GetBlock(checkPos);

                if (block == null || !block.Code.Path.Contains("glimmering-livingwood"))
                {
                    return false; // Не хватает светящегося блока, прерываем проверку
                }
            }

            // Опционально: можно добавить проверку, чтобы внутри портала (3x3) был воздух,
            // но если портал при включении сам заменяет блоки на текстуру портала, то можно оставить так.

            return true; // Все блоки на своих местах!
        }

        public void OnInteract(IPlayer byPlayer)
        {
            // Взаимодействие обрабатываем только на сервере
            if (Api.Side == EnumAppSide.Client) return;

            if (IsActive)
                Deactivate();
            else
                Activate();
        }

        public void Activate()
        {
            if (IsActive) return;

            string orientation = GetPortalOrientation();
            if (orientation == "none")
            {
                return; // Структура не собрана
            }

            List<BlockEntityPylon> validPylons = new List<BlockEntityPylon>();
            int radius = 4;

            // ==========================================
            // 1. ИЩЕМ ТОЛЬКО ПОДХОДЯЩИЕ ПИЛОНЫ
            // ==========================================
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos pylonPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(pylonPos) is BlockEntityPylon pylon)
                        {
                            // Нас интересуют только природные пилоны
                            if (pylon.CurrentType == EnumPylonType.Natura)
                            {
                                BlockPos poolPos = pylonPos.DownCopy();
                                if (Api.World.BlockAccessor.GetBlockEntity(poolPos) is BlockEntityManaPool pool)
                                {
                                    // Если под пилоном есть бассейн и в нем есть мана — берем его!
                                    if (pool.CurrentMana >= 1)
                                    {
                                        validPylons.Add(pylon);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // ==========================================
            // 2. БЛОКИРОВКА ПРИ НЕХВАТКЕ ПИЛОНОВ
            // ==========================================
            // Если в радиусе не нашлось ни одного пилона с маной — ничего не делаем.
            if (validPylons.Count == 0) return;

            // ==========================================
            // 3. ВКЛЮЧАЕМ ЯДРО
            // ==========================================
            IsActive = true;
            UpdateBlockState("on"); // Меняем текстуру/модель самого ядра

            // ==========================================
            // 4. ПРИВЯЗЫВАЕМ ТОЛЬКО ВАЛИДНЫЕ ПИЛОНЫ
            // ==========================================
            foreach (var pylon in validPylons)
            {
                pylon.LinkedTarget = this.Pos;
                pylon.MarkDirty(true);
            }

            // ==========================================
            // 5. СОЗДАЕМ БЛОК ПОРТАЛА (АНИМАЦИЮ)
            // ==========================================
            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos portalPos = Pos.UpCopy();
                // Подставляем ориентацию (ns или we) в код блока
                Block portalBlock = Api.World.GetBlock(new AssetLocation("botaniastory", "alfheim_portal_dummy-" + orientation));

                if (portalBlock != null)
                {
                    Api.World.BlockAccessor.SetBlock(portalBlock.BlockId, portalPos);
                }
            }

            MarkDirty(true);
        }

        public void Deactivate(bool isBeingBroken = false)
        {
            if (!IsActive) return;

            IsActive = false;

            // Меняем стейт на "off" ТОЛЬКО если блок выключают вручную.
            if (!isBeingBroken)
            {
                UpdateBlockState("off");
            }

            // ==========================================
            // 1. ОТВЯЗЫВАЕМ ПИЛОНЫ
            // ==========================================
            int radius = 12;
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    for (int z = -radius; z <= radius; z++)
                    {
                        BlockPos pylonPos = Pos.AddCopy(x, y, z);
                        if (Api.World.BlockAccessor.GetBlockEntity(pylonPos) is BlockEntityPylon pylon)
                        {
                            // Отвязываем только если пилон был привязан именно к этому ядру
                            if (pylon.LinkedTarget != null && pylon.LinkedTarget.Equals(this.Pos))
                            {
                                pylon.LinkedTarget = null;
                                pylon.MarkDirty(true);
                            }
                        }
                    }
                }
            }

            // ==========================================
            // 2. УДАЛЯЕМ БЛОК ПОРТАЛА
            // ==========================================
            if (Api.Side == EnumAppSide.Server)
            {
                BlockPos portalPos = Pos.UpCopy();
                Block currentBlock = Api.World.BlockAccessor.GetBlock(portalPos);

                if (currentBlock != null && currentBlock.Code.Path.Contains("alfheim_portal_dummy"))
                {
                    Api.World.BlockAccessor.SetBlock(0, portalPos); // 0 - это воздух
                }
            }

            MarkDirty(true);
        }

        private void UpdateBlockState(string state)
        {
            AssetLocation newCode = Block.CodeWithVariant("state", state);
            Block nextBlock = Api.World.GetBlock(newCode);

            if (nextBlock != null)
            {
                // Заменяем блок (старый BlockEntity удаляется, создается новый)
                Api.World.BlockAccessor.ExchangeBlock(nextBlock.Id, Pos);

                // ВАЖНО: Восстанавливаем память новому ядру после замены!
                // Находим только что созданное новое ядро и жестко задаем ему статус.
                if (Api.World.BlockAccessor.GetBlockEntity(Pos) is BlockEntityElvenGatewayCore newCore)
                {
                    newCore.IsActive = (state == "on");
                }
            }
        }

        // --- СИНХРОНИЗАЦИЯ DAA ---

        public override void ToTreeAttributes(ITreeAttribute tree)
        {
            base.ToTreeAttributes(tree);
            tree.SetBool("isActive", IsActive);
        }

        public override void FromTreeAttributes(ITreeAttribute tree, IWorldAccessor worldForResolving)
        {
            base.FromTreeAttributes(tree, worldForResolving);
            IsActive = tree.GetBool("isActive");
        }

        // --- ОЧИСТКА ПРИ РАЗРУШЕНИИ ---

        public override void OnBlockRemoved()
        {
            // Передаем true, чтобы ядро поняло, что его ломают, и не пыталось заменить блок
            if (IsActive)
            {
                Deactivate(true);
            }
            base.OnBlockRemoved();
        }

        public override void OnBlockUnloaded()
        {
            base.OnBlockUnloaded();
        }
    }
}