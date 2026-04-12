using Vintagestory.API.Client;
using Vintagestory.API.Common;

namespace BotaniaStory
{
    public class TextureAtlasFix : ModSystem
    {
        public override void StartClientSide(ICoreClientAPI api)
        {
            // Подключаемся к моменту, когда игра загрузила текстуры блоков
            api.Event.BlockTexturesLoaded += () =>
            {
                int textureSubId;
                TextureAtlasPosition texPos;

                // Насильно приказываем игре запечь текстуру манастали в атлас блоков!
                api.BlockTextureAtlas.GetOrInsertTexture(
                    new AssetLocation("game:block/metal/ingot/manasteel"),
                    out textureSubId,
                    out texPos
                );

                api.Logger.Warning("[BotaniaStory] Текстура манастали принудительно добавлена в BlockTextureAtlas!");
            };
        }
    }
}