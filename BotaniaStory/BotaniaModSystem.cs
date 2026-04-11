using botaniastory;
using BotaniaStory.Flora.GeneratingFlora;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BotaniaStoryModSystem : ModSystem
    {
        private BotaniaWandHud wandHud;
        public static ManaStreamRenderer ManaRenderer;

        public IServerNetworkChannel serverChannel;
        public IClientNetworkChannel clientChannel;
        public static botaniastory.LexiconConfig ClientConfig;
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            // 2. Загружаем конфиг при старте игры
            ClientConfig = api.LoadModConfig<botaniastory.LexiconConfig>("lexicon_client.json") ?? new botaniastory.LexiconConfig();
            
            // 1. РЕГИСТРИРУЕМ СЕТЬ В ПЕРВУЮ ОЧЕРЕДЬ!
            clientChannel = api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType(typeof(ManaStreamPacket))
                .SetMessageHandler<ManaStreamPacket>(OnManaStreamPacketReceived);

            // 2. А уже потом загружаем HUD и Рендерер
            wandHud = new BotaniaWandHud(api);
            ManaRenderer = new ManaStreamRenderer(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            // Регистрируем сеть на стороне сервера
            serverChannel = api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType(typeof(ManaStreamPacket));
        }
        public override void Start(ICoreAPI api)
        {
            base.Start(api);
            api.RegisterItemClass("ItemLexicon", typeof(ItemLexicon));
            api.RegisterItemClass("ItemWandOfTheForest", typeof(ItemWandOfTheForest));
            api.RegisterBlockClass("BlockMysticalFlower", typeof(BlockMysticalFlower));
            api.RegisterBlockClass("BlockApothecary", typeof(BlockApothecary));
            api.RegisterBlockEntityClass("ApothecaryEntity", typeof(BlockEntityApothecary));
            api.RegisterBlockClass("BlockPureDaisy", typeof(BlockPureDaisy));
            api.RegisterBlockEntityClass("PureDaisyEntity", typeof(BlockEntityPureDaisy));
            api.RegisterBlockClass("BlockManaPool", typeof(BlockManaPool));
            api.RegisterBlockEntityClass("ManaPoolEntity", typeof(BlockEntityManaPool));
            api.RegisterBlockClass("ManaSpreader", typeof(ManaSpreader));
            api.RegisterBlockEntityClass("ManaSpreaderEntity", typeof(BlockEntityManaSpreader));
            api.RegisterBlockClass("BlockDaybloom", typeof(BlockDaybloom));
            api.RegisterBlockEntityClass("DaybloomEntity", typeof(BlockEntityDaybloom));
            api.RegisterEntity("EntityManaBurst", typeof(EntityManaBurst));
            api.RegisterBlockClass("BlockEndoflame", typeof(BlockEndoflame));
            api.RegisterBlockEntityClass("EndoflameEntity", typeof(BlockEntityEndoflame));
            api.RegisterItemClass("ItemWandOfBinding", typeof(ItemWandOfBinding));
            api.RegisterItemClass("ItemSpark", typeof(ItemSpark));
            api.RegisterEntity("EntitySpark", typeof(EntitySpark));
            api.RegisterItemClass("ItemSparkAugment", typeof(ItemSparkAugment));

            api.Logger.Notification("Мод Botania Story успешно загружен! Магия начинается...");
        }
        private void OnManaStreamPacketReceived(ManaStreamPacket packet)
        {
            ManaRenderer?.AddParticle(
                new Vintagestory.API.MathTools.Vec3d(packet.StartX, packet.StartY, packet.StartZ),
                new Vintagestory.API.MathTools.Vec3d(packet.EndX, packet.EndY, packet.EndZ)
            );
        }


        // --- ДОБАВЛЕНО: Обязательная очистка памяти при закрытии мира/игры ---
        public override void Dispose()
        {
            ManaRenderer?.Dispose();
            base.Dispose();
        }
    }

    // Теперь мы наследуемся от BlockPlant, а не от Block!
    public class BlockMysticalFlower : BlockPlant
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
    }
}