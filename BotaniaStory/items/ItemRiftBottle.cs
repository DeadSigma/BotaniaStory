using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.items
{
    public class ItemFlask : Item
    {
        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            if (!firstEvent) return;

            string content = Variant["content"];

            // === 1. ОПУСТОШЕНИЕ ЛЮБОГО ПУЗЫРЬКА (Ctrl + ПКМ) ===
            if (content != "empty" && byEntity.Controls.CtrlKey)
            {
                handling = EnumHandHandling.PreventDefaultAction;

                if (api.Side == EnumAppSide.Server)
                {
                    ReplaceItem(slot, byEntity, "flask-empty");

                    // Проверяем, что именно мы опустошаем, чтобы проиграть правильный звук
                    if (content == "water")
                    {
                        api.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), byEntity, null, true, 16f, 1f);
                    }
                    else if (content == "rustworldair")
                    {
                        // Звук шипения пара 
                        api.World.PlaySoundAt(new AssetLocation("game", "sounds/effect/extinguish"), byEntity, null, true, 16f, 1f);
                    }
                }
                return;
            }

            // === 2. НАПОЛНЕНИЕ ПУСТОГО ПУЗЫРЬКА ===
            if (content == "empty" && !byEntity.Controls.CtrlKey)
            {
                // 2.1. Сначала проверяем, есть ли рядом разлом
                bool riftFound = false;
                Vec3d playerPos = byEntity.Pos.XYZ;
                ModSystemRifts riftSystem = api.ModLoader.GetModSystem<ModSystemRifts>();

                if (riftSystem != null)
                {
                    if (api.Side == EnumAppSide.Server && riftSystem.ServerRifts != null)
                    {
                        foreach (Rift rift in riftSystem.ServerRifts)
                        {
                            if (rift.Position.DistanceTo(playerPos) < 2.0 && rift.Size > 0f) riftFound = true;
                        }
                    }
                    else if (api.Side == EnumAppSide.Client && riftSystem.riftsById != null)
                    {
                        foreach (var kvp in riftSystem.riftsById)
                        {
                            if (kvp.Value.Position.DistanceTo(playerPos) < 2.0 && kvp.Value.Size > 0f) riftFound = true;
                        }
                    }
                }

                if (riftFound)
                {
                    handling = EnumHandHandling.PreventDefaultAction;
                    if (api.Side == EnumAppSide.Server)
                    {
                        ReplaceItem(slot, byEntity, "flask-rustworldair");
                        api.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/transmute"), byEntity, null, true, 16f, 1f);
                    }
                    return;
                }

                // 2.2. Если разлома нет, проверяем клик по воде
                if (blockSel != null)
                {
                    BlockPos clickedPos = blockSel.Position;
                    BlockPos offsetPos = blockSel.Position.AddCopy(blockSel.Face);

                    Block[] blocksToCheck = new Block[] {
                        api.World.BlockAccessor.GetBlock(clickedPos),
                        api.World.BlockAccessor.GetBlock(offsetPos),
                        api.World.BlockAccessor.GetBlock(clickedPos, 1),
                        api.World.BlockAccessor.GetBlock(offsetPos, 1),
                        api.World.BlockAccessor.GetBlock(clickedPos, 2),
                        api.World.BlockAccessor.GetBlock(offsetPos, 2)
                    };

                    bool foundWater = false;

                    foreach (Block b in blocksToCheck)
                    {
                        if (b != null && b.Code != null && (b.LiquidCode == "water" || b.Code.Path.StartsWith("water")))
                        {
                            foundWater = true;
                            break;
                        }
                    }

                    if (foundWater)
                    {
                        handling = EnumHandHandling.PreventDefaultAction;
                        if (api.Side == EnumAppSide.Server)
                        {
                            ReplaceItem(slot, byEntity, "flask-water");
                            api.World.PlaySoundAt(new AssetLocation("game", "sounds/environment/smallsplash"), byEntity, null, true, 16f, 1f);
                        }
                        return;
                    }
                }
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }

        private void ReplaceItem(ItemSlot slot, EntityAgent byEntity, string newItemCode)
        {
            AssetLocation code = new AssetLocation("botaniastory", newItemCode);
            Item newItem = api.World.GetItem(code);

            if (newItem != null)
            {
                ItemStack newStack = new ItemStack(newItem);

                if (slot.StackSize == 1)
                {
                    slot.Itemstack = newStack;
                }
                else
                {
                    slot.TakeOut(1);
                    if (!byEntity.TryGiveItemStack(newStack))
                    {
                        api.World.SpawnItemEntity(newStack, byEntity.Pos.XYZ);
                    }
                }
                slot.MarkDirty();
            }
        }
    }
}