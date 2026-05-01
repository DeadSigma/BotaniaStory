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

            // Обновляем название варианта: теперь проверяем "content", а не "state"
            if (Variant["content"] != "empty")
            {
                base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
                return;
            }

            bool riftFound = false;
            Vec3d playerPos = byEntity.Pos.XYZ;

            ModSystemRifts riftSystem = api.ModLoader.GetModSystem<ModSystemRifts>();

            if (riftSystem != null)
            {
                if (api.Side == EnumAppSide.Server)
                {
                    if (riftSystem.ServerRifts != null)
                    {
                        foreach (Rift rift in riftSystem.ServerRifts)
                        {
                            if (rift.Position.DistanceTo(playerPos) < 2.0 && rift.Size > 0f) riftFound = true;
                        }
                    }
                }
                else
                {
                    if (riftSystem.riftsById != null)
                    {
                        foreach (var kvp in riftSystem.riftsById)
                        {
                            if (kvp.Value.Position.DistanceTo(playerPos) < 2.0 && kvp.Value.Size > 0f) riftFound = true;
                        }
                    }
                }
            }

            if (riftFound)
            {
                handling = EnumHandHandling.PreventDefaultAction;

                if (api.Side == EnumAppSide.Server)
                {
                    // Обновляем код выдаваемого предмета на новый универсальный формат "flask-rustworldair"
                    AssetLocation fullBottleCode = new AssetLocation("botaniastory", "flask-rustworldair");
                    Item fullBottleItem = api.World.GetItem(fullBottleCode);

                    if (fullBottleItem != null)
                    {
                        ItemStack fullStack = new ItemStack(fullBottleItem);

                        if (slot.StackSize == 1)
                        {
                            slot.Itemstack = fullStack;
                        }
                        else
                        {
                            slot.TakeOut(1);
                            if (!byEntity.TryGiveItemStack(fullStack))
                            {
                                api.World.SpawnItemEntity(fullStack, byEntity.Pos.XYZ);
                            }
                        }

                        slot.MarkDirty();
                        api.World.PlaySoundAt(new AssetLocation("botaniastory", "sounds/transmute"), byEntity, null, true, 16f, 1f);
                    }
                }
                return;
            }

            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);
        }
    }
}