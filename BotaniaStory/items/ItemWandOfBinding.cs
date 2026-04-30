using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using BotaniaStory.Flora.GeneratingFlora;
using BotaniaStory.blocks;

namespace BotaniaStory.items
{
    public class ItemWandOfBinding : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {


            if (!firstEvent || blockSel == null) return;

            IPlayer byPlayer = (byEntity as EntityPlayer)?.Player;
            if (byPlayer == null) return;

            IWorldAccessor world = byEntity.World;
            BlockPos pos = blockSel.Position;
            Block block = world.BlockAccessor.GetBlock(pos);
            BlockEntity clickedBe = world.BlockAccessor.GetBlockEntity(pos);



            AssetLocation bindSound = new AssetLocation("botaniastory", "sounds/effect/translocate");

            // --- ДОБАВЛЯЕМ ЧТЕНИЕ ГРОМКОСТИ ---
            float wandVolume = 1f;
            if (world.Api is ICoreClientAPI)
            {
                wandVolume = (BotaniaStoryModSystem.ClientConfig?.WandVolume ?? 50) / 100f;
            }

            // ==========================================
            // 1. КЛИК ПО РАСПРОСТРАНИТЕЛЮ (Массовая привязка)
            // ==========================================
            if (block is ManaSpreader)
            {
                int radius = 6;
                int boundCount = 0;



                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        for (int z = -radius; z <= radius; z++)
                        {
                            BlockPos checkPos = pos.AddCopy(x, y, z);
                            BlockEntity be = world.BlockAccessor.GetBlockEntity(checkPos);

                            if (be is BlockEntityGeneratingFlower flower)
                            {
                                flower.LinkedSpreader = pos.Copy();
                                flower.MarkDirty(true);
                                boundCount++;

                                // Создаем луч частиц от распространителя к цветку
                                SpawnBindingParticles(world, pos, checkPos);
                            }
                        }
                    }
                }

                if (world.Side == EnumAppSide.Client)
                {
                    var clientApi = world.Api as ICoreClientAPI;
                    if (boundCount > 0)
                    {
                        clientApi?.ShowChatMessage($"Посох связывания подключил цветы: {boundCount} шт. к этому Распространителю.");
                    }
                    else
                    {
                        clientApi?.ShowChatMessage("Поблизости нет генерирующих цветов для привязки.");
                    }
                }

                if (boundCount > 0)
                {
                    world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            // ==========================================
            // 2. КЛИК ПО ЦВЕТКУ (Копирование привязки на соседей)
            // ==========================================
            else if (clickedBe is BlockEntityGeneratingFlower clickedFlower)
            {
                if (clickedFlower.LinkedSpreader != null)
                {
                    int radius = 6;
                    int boundCount = 0;
                    BlockPos spreaderPos = clickedFlower.LinkedSpreader;

                    SpawnBindingParticles(world, spreaderPos, pos);

                    for (int x = -radius; x <= radius; x++)
                    {
                        for (int y = -radius; y <= radius; y++)
                        {
                            for (int z = -radius; z <= radius; z++)
                            {
                                BlockPos checkPos = pos.AddCopy(x, y, z);
                                BlockEntity be = world.BlockAccessor.GetBlockEntity(checkPos);

                                if (be is BlockEntityGeneratingFlower flower && !checkPos.Equals(pos))
                                {
                                    flower.LinkedSpreader = spreaderPos.Copy();
                                    flower.MarkDirty(true);
                                    boundCount++;

                                    // Создаем луч частиц от распространителя к этому соседнему цветку
                                    SpawnBindingParticles(world, spreaderPos, checkPos);
                                }
                            }
                        }
                    }

                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        if (boundCount > 0)
                        {
                            clientApi?.ShowChatMessage($"Связь скопирована! Подключено соседних цветов: {boundCount} шт. к тому же Распространителю.");
                        }
                        else
                        {
                            clientApi?.ShowChatMessage("Поблизости нет других генерирующих цветов для привязки.");
                        }
                    }

                    if (boundCount > 0)
                    {
                        world.PlaySoundAt(bindSound, pos.X + 0.5, pos.Y + 0.5, pos.Z + 0.5, byPlayer, true, 16, wandVolume);
                    }
                }
                else
                {
                    if (world.Side == EnumAppSide.Client)
                    {
                        var clientApi = world.Api as ICoreClientAPI;
                        clientApi?.ShowChatMessage("Этот цветок ещё не привязан к Распространителю!");
                    }
                }

                handling = EnumHandHandling.Handled;
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        // ==========================================
        // МЕТОД ДЛЯ РИСОВАНИЯ ЛУЧЕЙ ИЗ ЧАСТИЦ
        // ==========================================
        private void SpawnBindingParticles(IWorldAccessor world, BlockPos spreaderPos, BlockPos flowerPos)
        {
            // Смещаем координаты в центр блоков
            Vec3d start = new Vec3d(spreaderPos.X + 0.5, spreaderPos.Y + 0.5, spreaderPos.Z + 0.5);
            Vec3d end = new Vec3d(flowerPos.X + 0.5, flowerPos.Y + 0.2, flowerPos.Z + 0.5);

            double distance = start.DistanceTo(end);
            Vec3d direction = (end - start).Normalize();

            // Настраиваем красивые светящиеся частицы (цвет маны - бирюзово-зеленый)
            SimpleParticleProperties beamParticles = new SimpleParticleProperties(
                1, 1, // Количество частиц в одной точке
                ColorUtil.ToRgba(255, 100, 255, 200), // Цвет (A, R, G, B)
                new Vec3d(), new Vec3d(),
                new Vec3f(-0.05f, -0.05f, -0.05f), // Минимальная скорость разлета
                new Vec3f(0.05f, 0.05f, 0.05f),    // Максимальная скорость разлета
                1.0f, // Время жизни (секунды)
                0f,
                0.2f, // Начальный размер
                0.4f, // Максимальный размер
                EnumParticleModel.Quad
            );

            // Делаем частицы светящимися в темноте!
            beamParticles.VertexFlags = 128;
            // Заставляем их плавно уменьшаться и исчезать
            beamParticles.SizeEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEAR, -0.5f);

            // Рисуем пунктир от старта до конца с шагом 0.3 блока
            for (float i = 0; i < distance; i += 0.3f)
            {
                beamParticles.MinPos.Set(start.X + direction.X * i, start.Y + direction.Y * i, start.Z + direction.Z * i);
                world.SpawnParticles(beamParticles);
            }
        }
    }
}