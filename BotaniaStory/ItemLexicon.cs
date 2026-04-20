using BotaniaStory;
using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace botaniastory
{
    public class ItemLexicon : Item
    {
        GuiDialogLexicon guideDialog;

        // ДОБАВЛЕНО: Создаем "неубиваемую" анимацию с высоким приоритетом
        public static AnimationMetaData ReadAnimation = new AnimationMetaData()
        {
            Animation = "interactstatic",
            Code = "reading_lexicon",
            Weight = 10f,
            BlendMode = EnumAnimationBlendMode.Add, 
            EaseInSpeed = 2f,
            EaseOutSpeed = 2f
        };

        public override void OnHeldInteractStart(ItemSlot slot, EntityAgent byEntity, BlockSelection blockSel, EntitySelection entitySel, bool firstEvent, ref EnumHandHandling handling)
        {
            base.OnHeldInteractStart(slot, byEntity, blockSel, entitySel, firstEvent, ref handling);

            if (handling == EnumHandHandling.PreventDefault) return;

            if (byEntity.World.Side == EnumAppSide.Client)
            {
                ICoreClientAPI capi = byEntity.Api as ICoreClientAPI;

                if (guideDialog == null)
                {
                    guideDialog = new GuiDialogLexicon(capi);

                    // При закрытии книги — ОСТАНАВЛИВАЕМ по нашему уникальному Code
                    guideDialog.OnClosed += () => {
                        byEntity.AnimManager.StopAnimation("reading_lexicon");
                        capi.Network.GetChannel("botanianetwork").SendPacket(new LexiconStatePacket() { IsOpen = false });
                    };
                }

                guideDialog.ActiveBookSlot = slot;
                string targetChapterId = null;

                if (byEntity.Controls.Sneak && blockSel != null)
                {
                    Vintagestory.API.Common.Block targetedBlock = capi.World.BlockAccessor.GetBlock(blockSel.Position);
                    if (targetedBlock != null)
                    {
                        targetChapterId = BookDataManager.GetChapterForBlock(targetedBlock.Code.ToString());
                    }
                }

                if (!guideDialog.IsOpened())
                {
                    guideDialog.TryOpen();

                    // ЗАПУСКАЕМ неубиваемую анимацию
                    byEntity.AnimManager.StartAnimation(ReadAnimation);

                    capi.Network.GetChannel("botanianetwork").SendPacket(new LexiconStatePacket() { IsOpen = true });
                }

                if (targetChapterId != null)
                {
                    guideDialog.OpenSpecificChapter(targetChapterId);
                }

                handling = EnumHandHandling.PreventDefault;
            }
        }
    }
}