using botaniastory;
using BotaniaStory.Flora.GeneratingFlora;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;
using ProtoBuf;

namespace BotaniaStory
{
    public class BotaniaStoryModSystem : ModSystem
    {
        private BotaniaWandHud wandHud;
        public static ManaStreamRenderer ManaRenderer;

        public IServerNetworkChannel serverChannel;
        public IClientNetworkChannel clientChannel;
        public static botaniastory.LexiconConfig ClientConfig;

        // Переменная, чтобы мы могли обращаться к миру для воспроизведения звука
        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api; // Сохраняем ссылку на Клиент

            // 2. Загружаем конфиг при старте игры
            ClientConfig = api.LoadModConfig<botaniastory.LexiconConfig>("lexicon_client.json") ?? new botaniastory.LexiconConfig();

            // 1. РЕГИСТРИРУЕМ СЕТЬ В ПЕРВУЮ ОЧЕРЕДЬ!
            clientChannel = api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType(typeof(ManaStreamPacket))
                .RegisterMessageType(typeof(PlayManaSoundPacket)) //  пакет звука
                .RegisterMessageType(typeof(LexiconStatePacket))
                .SetMessageHandler<ManaStreamPacket>(OnManaStreamPacketReceived)
                .SetMessageHandler<PlayManaSoundPacket>(OnSoundPacketReceived); // чтение звука

            // 2. А уже потом загружаем HUD и Рендерер
            wandHud = new BotaniaWandHud(api);
            ManaRenderer = new ManaStreamRenderer(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api; // <--- Сохраняем ссылку на сервер

            serverChannel = api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType(typeof(ManaStreamPacket))
                .RegisterMessageType(typeof(PlayManaSoundPacket))
                .RegisterMessageType(typeof(LexiconStatePacket)) // <--- Добавили пакет книги
                .SetMessageHandler<LexiconStatePacket>(OnLexiconStateMessage); // <--- Сервер слушает этот пакет
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
            api.RegisterItemClass("ItemManaTablet", typeof(ItemManaTablet));
            api.RegisterItemClass("ItemManaArmor", typeof(ItemManaArmor));
            api.RegisterItemClass("ItemManaTool", typeof(ItemManaTool));
            api.RegisterItemClass("ItemManaAxe", typeof(ItemManaAxe));
            api.RegisterItemClass("ItemManaScythe", typeof(ItemManaScythe));
            api.RegisterItemClass("ItemManaChisel", typeof(ItemManaChisel));
            api.RegisterItemClass("ItemManaKnife", typeof(ItemManaKnife));
            api.RegisterItemClass("ItemManaCleaver", typeof(ItemManaCleaver));
            api.RegisterItemClass("ItemManaProspectingPick", typeof(ItemManaProspectingPick));
            api.RegisterItemClass("ItemManaTongs", typeof(ItemManaTongs));
            api.RegisterItemClass("ItemManaShears", typeof(ItemManaShears));
            api.RegisterItemClass("ItemManaHammer", typeof(ItemManaHammer));
            api.RegisterItemClass("ItemManaHoe", typeof(ItemManaHoe));
            api.RegisterItemClass("ItemManaSpear", typeof(ItemManaSpear));
            api.RegisterItemClass("ItemManaWrench", typeof(ItemManaWrench));
            api.RegisterItemClass("ItemManaCrowbar", typeof(ItemManaCrowbar));


            api.Logger.Notification("Мод Botania Story успешно загружен! Магия начинается...");
        }

        private void OnManaStreamPacketReceived(ManaStreamPacket packet)
        {
            ManaRenderer?.AddParticle(
                new Vintagestory.API.MathTools.Vec3d(packet.StartX, packet.StartY, packet.StartZ),
                new Vintagestory.API.MathTools.Vec3d(packet.EndX, packet.EndY, packet.EndZ)
            );
        }

        // --- ДОБАВЛЕНО: Что делает Клиент, когда получает письмо о звуке
        private void OnSoundPacketReceived(PlayManaSoundPacket packet)
        {
            // ЗАЩИТА ОТ КРАША: Если пакет или его данные пустые — просто игнорируем
            if (packet == null || packet.SoundName == null || packet.Position == null) return;

            float volume = 0f;

            if (packet.SoundName == "transmute" || packet.SoundName == "apothecary_splash" || packet.SoundName == "apothecary_craft")
            {
                volume = ClientConfig.PoolVolume / 100f;
            }
            else if (packet.SoundName == "manaspreaderfire")
            {
                volume = ClientConfig.SpreaderVolume / 100f;
            }
            else if (packet.SoundName == "ignite")
            {
                volume = ClientConfig.FlowerVolume / 100f;
            }
            else
            {
                volume = 0.5f;
            }

            if (volume <= 0) return;

            capi.World.PlaySoundAt(
                new AssetLocation("botaniastory", "sounds/" + packet.SoundName),
                packet.Position.X, packet.Position.Y, packet.Position.Z,
                null, true, 16f, volume
            );
        }

        private void OnLexiconStateMessage(IServerPlayer fromPlayer, LexiconStatePacket packet)
        {
            ItemSlot activeSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot.Itemstack?.Item is ItemLexicon)
            {
                // ИСПОЛЬЗУЕМ ту же мета-дату из ItemLexicon!
                if (packet.IsOpen)
                {
                    fromPlayer.Entity.AnimManager.StartAnimation(ItemLexicon.ReadAnimation);
                }
                else
                {
                    fromPlayer.Entity.AnimManager.StopAnimation("reading_lexicon");
                }

                string targetState = packet.IsOpen ? "open" : "closed";
                string currentPath = activeSlot.Itemstack.Item.Code.Path;

                if (!currentPath.EndsWith(targetState))
                {
                    string newPath = packet.IsOpen
                        ? currentPath.Replace("closed", "open")
                        : currentPath.Replace("open", "closed");

                    Item newBookItem = sapi.World.GetItem(new AssetLocation("botaniastory", newPath));
                    if (newBookItem != null)
                    {
                        activeSlot.Itemstack = new ItemStack(newBookItem);
                        activeSlot.MarkDirty();
                    }
                }
            }
        }
        public override void Dispose()
        {
            ManaRenderer?.Dispose();
            base.Dispose();
        }
    }

    // пакеты звука для мультиплеера
    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class LexiconStatePacket
    {
        public bool IsOpen;
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class PlayManaSoundPacket
    {
        public Vec3d Position;
        public string SoundName;
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