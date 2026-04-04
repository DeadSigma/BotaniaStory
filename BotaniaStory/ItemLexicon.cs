using Vintagestory.API.Common;
using Vintagestory.API.Client;

namespace botaniastory
{
    public class ItemLexicon : Item
    {
        GuiDialogLexicon guideDialog;

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            // 1. Базовая логика игры
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (handling == EnumHandHandling.PreventDefault)
            {
                return;
            }

            // 2. Открываем интерфейс и меняем модельку (только на клиенте)
            if (byEntity.World.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = byEntity.Api as ICoreClientAPI;

                if (guideDialog == null)
                {
                    guideDialog = new GuiDialogLexicon(capi);

                }


                guideDialog.ActiveBookSlot = slot;

                string targetChapterId = null;

                if (byEntity.Controls.Sneak && blockSel != null)
                {
                    Vintagestory.API.Common.Block targetedBlock = capi.World.BlockAccessor.GetBlock(blockSel.Position);
                    if (targetedBlock != null)
                    {
                        // Берем полный код блока (например: "botaniastory:mysticalflower-orange-free")
                        string fullBlockCode = targetedBlock.Code.ToString();

                        // Обращаемся к нашему умному методу поиска
                        targetChapterId = BookDataManager.GetChapterForBlock(fullBlockCode);
                    }
                }

                if (!guideDialog.IsOpened())
                {
                    guideDialog.TryOpen();
                    // ВОЗВРАЩЕНО: Как только GUI открылся, даем в руки открытую книгу
                    SwapBookModel(capi, slot, "open");
                }

                if (targetChapterId != null)
                {
                    guideDialog.OpenSpecificChapter(targetChapterId);
                }

                handling = EnumHandHandling.PreventDefault;
            }
        }

        // ВОЗВРАЩЕНО: Твой вспомогательный метод для быстрой смены предмета
        private void SwapBookModel(ICoreClientAPI capi, ItemSlot slot, string targetState)
        {
            if (slot.Itemstack == null) return;

            string currentPath = slot.Itemstack.Item.Code.Path;

            // Если книга уже в нужном состоянии, ничего не делаем
            if (currentPath.EndsWith(targetState)) return;

            // Генерируем новый код
            string newPath = currentPath.EndsWith("closed")
                ? currentPath.Replace("closed", targetState)
                : currentPath.Replace("open", targetState);

            // Ищем этот предмет в реестре игры
            Item newBookItem = capi.World.GetItem(new AssetLocation("botaniastory", newPath));

            if (newBookItem != null)
            {
                // Кладем новую книгу в слот и говорим игре обновить визуал
                slot.Itemstack = new ItemStack(newBookItem);
                slot.MarkDirty();
            }
        }
    }
}