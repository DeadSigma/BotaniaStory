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

        public IServerNetworkChannel serverChannel;
        public IClientNetworkChannel clientChannel;
        public static botaniastory.LexiconConfig ClientConfig;

        // --- ПЕРЕМЕННАЯ РЕНДЕРЕРА ЧАСТИЦ ---
        public static ManaStreamRenderer ManaRenderer;

        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;

        // --- 1. ОБЩАЯ РЕГИСТРАЦИЯ ДЛЯ КЛИЕНТА И СЕРВЕРА ---
        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            // Регистрируем канал и ВСЕ пакеты один раз здесь
            api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType<PlayManaSoundPacket>()
                .RegisterMessageType<LexiconStatePacket>()
                .RegisterMessageType<ManaStreamPacket>();

            // Регистрация контента мода
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
            api.RegisterBlockClass("BlockRunicAltar", typeof(BlockRunicAltar));
            api.RegisterBlockEntityClass("RunicAltar", typeof(BlockEntityRunicAltar));
            api.RegisterBlockClass("BlockTerrestrialPlate", typeof(BlockTerrestrialPlate));
            api.RegisterBlockEntityClass("BlockEntityTerrestrialPlate", typeof(BlockEntityTerrestrialPlate));
            api.RegisterBlockClass("BlockPylon", typeof(BlockPylon));
            api.RegisterBlockEntityClass("BEPylon", typeof(BlockEntityPylon));
            api.RegisterBlockClass("BlockElvenGatewayCore", typeof(BlockElvenGatewayCore));
            api.RegisterBlockEntityClass("BEElvenGatewayCore", typeof(BlockEntityElvenGatewayCore));



        api.Logger.Notification("Мод Botania Story успешно загружен! Магия начинается...");
        }

        // --- 2. КЛИЕНТСКАЯ ЧАСТЬ (Звуки и Искры) ---
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;

            ClientConfig = api.LoadModConfig<botaniastory.LexiconConfig>("lexicon_client.json") ?? new botaniastory.LexiconConfig();

            // Получаем канал и вешаем слушателей
            clientChannel = api.Network.GetChannel("botanianetwork") as IClientNetworkChannel;
            clientChannel
                .SetMessageHandler<PlayManaSoundPacket>(OnSoundPacketReceived)
                .SetMessageHandler<ManaStreamPacket>(OnManaStreamPacketReceived); 

            wandHud = new BotaniaWandHud(api);

            // Инициализируем рендерер частиц
            ManaRenderer = new ManaStreamRenderer(api);
        }

        // --- 3. СЕРВЕРНАЯ ЧАСТЬ (Лексикон) ---
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            // Получаем канал и вешаем слушателя пакетов книги
            serverChannel = api.Network.GetChannel("botanianetwork") as IServerNetworkChannel;
            serverChannel.SetMessageHandler<LexiconStatePacket>(OnLexiconStateMessage);
        }

        // --- ОБРАБОТЧИК ДЛЯ ЧАСТИЦ (КЛИЕНТ) ---
        private void OnManaStreamPacketReceived(ManaStreamPacket packet)
        {
            if (ManaRenderer != null)
            {
                ManaRenderer.AddParticle(
                    new Vec3d(packet.StartX, packet.StartY, packet.StartZ),
                    new Vec3d(packet.EndX, packet.EndY, packet.EndZ)
                );
            }
        }

        // --- ОБРАБОТЧИК ДЛЯ ЗВУКОВ (КЛИЕНТ) ---
        private void OnSoundPacketReceived(PlayManaSoundPacket packet)
        {
            if (capi.World?.Player?.Entity == null) return;
            if (packet == null || packet.SoundName == null || packet.Position == null) return;

            float volume = 0f;

            if (packet.SoundName == "transmute")
            {
                volume = ClientConfig.PoolVolume / 100f;
            }
            else if (packet.SoundName == "runic_altar_craft" || packet.SoundName == "runic_altar_full")
            {
                volume = ClientConfig.AltarVolume / 100f;
            }
            else if (packet.SoundName == "apothecary_splash" || packet.SoundName == "apothecary_craft")
            {
                volume = ClientConfig.ApothecaryVolume / 100f;
            }
            else if (packet.SoundName == "manaspreaderfire")
            {
                volume = ClientConfig.SpreaderVolume / 100f;
            }
            else if (packet.SoundName == "terrasteel_craft")
            {
                volume = ClientConfig.PlateVolume / 100f;
            }
            else if (packet.SoundName == "ignite")
            {
                volume = ClientConfig.FlowerVolume / 100f;
            }
            else if (packet.SoundName == "wand_bind")
            {
                volume = ClientConfig.WandVolume / 100f;
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

        // --- ОБРАБОТЧИК ДЛЯ ЛЕКСИКОНА (СЕРВЕР) ---
        private void OnLexiconStateMessage(IServerPlayer fromPlayer, LexiconStatePacket packet)
        {
            ItemSlot activeSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;
            if (activeSlot.Itemstack?.Item is ItemLexicon)
            {
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
            base.Dispose();
        }
    }

    // --- ПАКЕТЫ И КЛАССЫ ---

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

    public class BlockMysticalFlower : BlockPlant
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
    }
}