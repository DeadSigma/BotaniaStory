using BotaniaStory.blockentity;
using BotaniaStory.blocks;
using BotaniaStory.Blocks;
using BotaniaStory.client;
using BotaniaStory.client.renderers;
using BotaniaStory.client.ui;
using BotaniaStory.entities;
using BotaniaStory.entities.ai;
using BotaniaStory.Flora.GeneratingFlora;
using BotaniaStory.items;
using BotaniaStory.lexicon;
using BotaniaStory.network;
using BotaniaStory.util;
using ProtoBuf;
using System;
using System.Numerics;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory
{
    public class BotaniaStoryModSystem : ModSystem
    {
        private BotaniaWandHud wandHud;

        public IServerNetworkChannel serverChannel;
        public IClientNetworkChannel clientChannel;
        public static LexiconConfig ClientConfig;
        public static BotaniaConfig ServerConfig;
        public static ManaStreamRenderer ManaRenderer;
        public static GaiaLightningRenderer GaiaLightningVisuals;

        private ICoreClientAPI capi;
        private ICoreServerAPI sapi;

        public override void StartPre(ICoreAPI api)
        {
            base.StartPre(api);

            try
            {
                // Загружаем конфиг механик мода
                ServerConfig = api.LoadModConfig<BotaniaConfig>("botaniastory_server.json");

                // Если файла еще нет, создаем дефолтный и сохраняем
                if (ServerConfig == null)
                {
                    ServerConfig = new BotaniaConfig();
                    api.StoreModConfig(ServerConfig, "botaniastory_server.json");
                }
            }
            catch (Exception e)
            {
                ServerConfig = new BotaniaConfig();
                api.Logger.Error("Failed to load botaniastory_server.json. Using default settings. Error: " + e.Message);
            }
        }
        public override void Start(ICoreAPI api)
        {
            base.Start(api);


            api.Network.RegisterChannel("botanianetwork")
                .RegisterMessageType<PlayManaSoundPacket>()
                .RegisterMessageType<LexiconStatePacket>()
                .RegisterMessageType<ManaStreamPacket>()
                .RegisterMessageType<GaiaLightningPacket>()
                .RegisterMessageType<FilterUpdatePacket>();


            api.RegisterItemClass("ItemLexicon", typeof(ItemLexicon));
            api.RegisterItemClass("ItemWandOfTheForest", typeof(ItemWandOfTheForest));
            api.RegisterBlockClass("BlockMysticalFlower", typeof(BlockMysticalFlower));
            api.RegisterBlockClass("BlockApothecary", typeof(BlockApothecary));
            api.RegisterBlockEntityClass("ApothecaryEntity", typeof(BlockEntityApothecary));
            api.RegisterBlockClass("BlockManaPool", typeof(BlockManaPool));
            api.RegisterBlockEntityClass("ManaPoolEntity", typeof(BlockEntityManaPool));
            api.RegisterBlockClass("ManaSpreader", typeof(ManaSpreader));
            api.RegisterBlockEntityClass("ManaSpreaderEntity", typeof(BlockEntityManaSpreader));
            api.RegisterEntity("EntityManaBurst", typeof(EntityManaBurst));

            //Функциональные цветы
            api.RegisterBlockClass("BlockPureDaisy", typeof(BlockPureDaisy));
            api.RegisterBlockEntityBehaviorClass("puredaisylogic", typeof(BEBehaviorPureDaisy));
            api.RegisterBlockClass("BlockJadedAmaranthus", typeof(BlockJadedAmaranthus));
            api.RegisterBlockEntityBehaviorClass("jadedamaranthuslogic", typeof(BEBehaviorJadedAmaranthus));
            api.RegisterBlockClass("BlockWitheredAmaranthus", typeof(BlockWitheredAmaranthus));
            api.RegisterBlockEntityBehaviorClass("witheredamaranthuslogic", typeof(BEBehaviorWitheredAmaranthus));
            api.RegisterBlockClass("BlockHopperhock", typeof(BlockHopperhock));
            api.RegisterBlockEntityBehaviorClass("hopperhocklogic", typeof(BEBehaviorHopperhock));
            api.RegisterBlockClass("BlockAgricarnation", typeof(BlockAgricarnation));
            api.RegisterBlockEntityBehaviorClass("agricarnationlogic", typeof(BEBehaviorAgricarnation));


            //Генерирующие цветы
            api.RegisterBlockEntityBehaviorClass("endoflamelogic", typeof(BEBehaviorEndoflame));
            api.RegisterBlockClass("BlockEndoflame", typeof(BlockEndoflame));
            api.RegisterBlockClass("BlockRosaArcana", typeof(BlockRosaArcana));
            api.RegisterBlockEntityBehaviorClass("rosaarcanalogic", typeof(BEBehaviorRosaArcana));
            api.RegisterBlockClass("BlockDaybloom", typeof(BlockDaybloom));
            api.RegisterBlockEntityBehaviorClass("daybloomlogic", typeof(BEBehaviorDaybloom));

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
            api.RegisterItemClass("ItemTerraTool", typeof(ItemTerraTool));
            api.RegisterItemClass("ItemTerraAxe", typeof(ItemTerraAxe));
            api.RegisterItemClass("ItemTerraScythe", typeof(ItemTerraScythe));
            api.RegisterItemClass("ItemTerraChisel", typeof(ItemTerraChisel));
            api.RegisterItemClass("ItemTerraKnife", typeof(ItemTerraKnife));
            api.RegisterItemClass("ItemTerraCleaver", typeof(ItemTerraCleaver));
            api.RegisterItemClass("ItemTerraProspectingPick", typeof(ItemTerraProspectingPick));
            api.RegisterItemClass("ItemTerraTongs", typeof(ItemTerraTongs));
            api.RegisterItemClass("ItemTerraShears", typeof(ItemTerraShears));
            api.RegisterItemClass("ItemTerraHammer", typeof(ItemTerraHammer));
            api.RegisterItemClass("ItemTerraHoe", typeof(ItemTerraHoe));
            api.RegisterItemClass("ItemTerraSpear", typeof(ItemTerraSpear));
            api.RegisterItemClass("ItemTerraWrench", typeof(ItemTerraWrench));
            api.RegisterItemClass("ItemTerraCrowbar", typeof(ItemTerraCrowbar));
            api.RegisterBlockClass("BlockRunicAltar", typeof(BlockRunicAltar));
            api.RegisterBlockEntityClass("RunicAltar", typeof(BlockEntityRunicAltar));
            api.RegisterBlockClass("BlockTerrestrialPlate", typeof(BlockTerrestrialPlate));
            api.RegisterBlockEntityClass("BlockEntityTerrestrialPlate", typeof(BlockEntityTerrestrialPlate));
            api.RegisterBlockClass("BlockPylon", typeof(BlockPylon));
            api.RegisterBlockEntityClass("BEPylon", typeof(BlockEntityPylon));
            api.RegisterBlockClass("BlockElvenGatewayCore", typeof(BlockElvenGatewayCore));
            api.RegisterBlockEntityClass("BEElvenGatewayCore", typeof(BlockEntityElvenGatewayCore));
            api.RegisterItemClass("ItemFlightTiara", typeof(ItemFlightTiara));
            api.RegisterItemClass("ItemBlackHoleTalisman", typeof(ItemBlackHoleTalisman));
            api.RegisterItemClass("ItemFlask", typeof(ItemFlask));
            api.RegisterItemClass("ItemRodOfTheSeas", typeof(ItemRodOfTheSeas));
            api.RegisterCollectibleBehaviorClass("AnimatedItem", typeof(BotaniaStory.systems.BehaviorAnimatedItem));
            api.RegisterBlockClass("BlockBotaniaFlower", typeof(BlockBotaniaFlower));
            api.RegisterEntity("EntityGaiaGuardian", typeof(EntityGaiaGuardian));
            AiTaskRegistry.Register<AiTaskGaiaTeleport>("gaiateleport");
            AiTaskRegistry.Register<AiTaskGaiaLightning>("gaialightning");
            AiTaskRegistry.Register<AiTaskGaiaSpawnMobs>("gaiaspawnmobs");
            api.RegisterItemClass("ItemTerraShatterer", typeof(ItemTerraShatterer));
            api.RegisterBlockClass("BlockMechanicalDropper", typeof(BlockMechanicalDropper));
            api.RegisterBlockEntityClass("BlockEntityMechanicalDropper", typeof(BlockEntityMechanicalDropper));
            api.RegisterBlockClass("BlockHourglass", typeof(BlockHourglass));
            api.RegisterBlockEntityClass("BlockEntityHourglass", typeof(BlockEntityHourglass));
            api.RegisterBlockEntityClass("FloatingIslandEntity", typeof(BlockEntityFloatingIsland));
            api.RegisterBlockClass("BlockBotaniaFloatingIsland", typeof(BlockBotaniaFloatingIsland));
            api.RegisterItemClass("ItemMeadowSeed", typeof(ItemMeadowSeed));
            api.RegisterItemClass("ItemFlowerBag", typeof(ItemFlowerBag));
            api.RegisterItemClass("ItemFilterScroll", typeof(ItemFilterScroll));
            api.RegisterItemClass("ItemFloralFertilizer", typeof(ItemFloralFertilizer));
            api.RegisterItemClass("ItemMysticalPowder", typeof(ItemMysticalPowder));


            api.Logger.Notification("Mod BotaniaStory wurde erfolgreich geladen! Die Magie beginnt...");

        }                      



        // КЛИЕНТСКАЯ ЧАСТЬ (Звуки и Искры)
        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);
            this.capi = api;

            ClientConfig = api.LoadModConfig<LexiconConfig>("lexicon_client.json") ?? new LexiconConfig();


            // Получаем канал и вешаем слушателей
            clientChannel = api.Network.GetChannel("botanianetwork") as IClientNetworkChannel;
            clientChannel
                .SetMessageHandler<PlayManaSoundPacket>(OnSoundPacketReceived)
                .SetMessageHandler<ManaStreamPacket>(OnManaStreamPacketReceived);
            wandHud = new BotaniaWandHud(api);

            // Инициализируем рендерер частиц
            ManaRenderer = new ManaStreamRenderer(api);

            capi.Event.RegisterRenderer(new TerraShattererHud(capi), EnumRenderStage.Ortho);

            clientChannel.SetMessageHandler<GaiaLightningPacket>(OnGaiaLightningPacketReceived);
            GaiaLightningVisuals = new GaiaLightningRenderer(api);
        }


        // СЕРВЕРНАЯ ЧАСТЬ
        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);
            this.sapi = api;

            // Получаем канал и вешаем слушателя пакетов книги
            serverChannel = api.Network.GetChannel("botanianetwork") as IServerNetworkChannel;
            serverChannel.SetMessageHandler<LexiconStatePacket>(OnLexiconStateMessage);
            serverChannel.SetMessageHandler<FilterUpdatePacket>(OnClientUpdateFilter);

            api.Event.RegisterGameTickListener(OnTalismanTick, 500);

            sapi.ChatCommands.GetOrCreate("b")
             .WithDescription("Botania Admin Commands")
             .RequiresPrivilege(Privilege.controlserver) // Только для админов
             .BeginSubCommand("sg")
             .WithDescription("Spawns the Gaia Guardian")
             .HandleWith(OnSpawnGaiaCommand)
             .EndSubCommand();
        }

        private TextCommandResult OnSpawnGaiaCommand(TextCommandCallingArgs args)
        {
            IServerPlayer player = args.Caller.Player as IServerPlayer;
            if (player == null) return TextCommandResult.Error("Команду может использовать только игрок.");

            // Ищем тип сущности 
            EntityProperties type = sapi.World.GetEntityType(new AssetLocation("botaniastory", "gaiaguardian"));
            if (type == null) return TextCommandResult.Error("Сущность gaiaguardian не найдена в JSON.");

            Entity entity = sapi.World.ClassRegistry.CreateEntity(type);

            // Спавним немного впереди игрока
            entity.Pos.SetPos(player.Entity.Pos.AsBlockPos.ToVec3d().Add(0, 1, 2));
            entity.Pos.SetFrom(entity.Pos);

            sapi.World.SpawnEntity(entity);

            return TextCommandResult.Success("Страж Гайи призван! Да начнется битва!");
        }

        private void OnTalismanTick(float dt)
        {
            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player.ConnectionState != EnumClientState.Playing) continue;

                ItemBlackHoleTalisman.AbsorbBlocksFromPlayer(player, sapi);
            }
        }

        // ОБРАБОТЧИК ДЛЯ ЧАСТИЦ (КЛИЕНТ)
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

        private void OnGaiaLightningPacketReceived(GaiaLightningPacket packet)
        {
            if (GaiaLightningVisuals != null)
            {
                GaiaLightningVisuals.AddLightning(packet.StartPos, packet.EndPos);
            }
        }

        // ОБРАБОТЧИК ДЛЯ ЗВУКОВ (КЛИЕНТ)
        private void OnSoundPacketReceived(PlayManaSoundPacket packet)
        {
            if (capi.World?.Player?.Entity == null) return;
            if (packet == null || packet.SoundName == null || packet.Position == null) return;

            float volume = 0f;

            if (packet.SoundName == "transmute" || packet.SoundName == "terrashatterer_evolve" || packet.SoundName == "terrashatterer_fill")
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
            else if (packet.SoundName == "whish")
            {
                volume = ClientConfig.TiaraVolume / 100f;
            }
            else if (packet.SoundName == "terrasteel_craft")
            {
                volume = ClientConfig.PlateVolume / 100f;
            }
            else if (packet.SoundName == "alfheim_exchange")
            {
                volume = ClientConfig.PortalVolume / 100f;
            }
            else if (packet.SoundName == "ignite")
            {
                volume = ClientConfig.FlowerVolume / 100f;
            }
            else if (packet.SoundName == "wand_bind")
            {
                volume = ClientConfig.WandVolume / 100f;
            }
            else if (packet.SoundName == "talisman_insert" || packet.SoundName == "talisman_extract" || packet.SoundName == "talisman_absorb")
            {
                volume = ClientConfig.TalismanVolume / 100f;
            }
            else if (packet.SoundName == "mechanical_dropper")
            {
                volume = ClientConfig.MechanicsVolume / 100f;
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

        // ОБРАБОТЧИК ДЛЯ ЛЕКСИКОНА (СЕРВЕР)
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

        private void OnClientUpdateFilter(IServerPlayer fromPlayer, FilterUpdatePacket packet)
        {
            ItemSlot activeSlot = fromPlayer.InventoryManager.ActiveHotbarSlot;

            // Проверяем, что игрок всё ещё держит бумагу в руке
            if (activeSlot.Itemstack?.Item is ItemFilterScroll)
            {
                // Сохранение кликнутых предметов
                if (packet.FilteredItemCodes == null || packet.FilteredItemCodes.Length == 0)
                {
                    activeSlot.Itemstack.Attributes.RemoveAttribute("filterList");
                }
                else
                {
                    activeSlot.Itemstack.Attributes["filterList"] = new Vintagestory.API.Datastructures.StringArrayAttribute(packet.FilteredItemCodes);
                }

                // Сохранение текстовых масок
                if (packet.FilterPatterns == null || packet.FilterPatterns.Length == 0)
                {
                    activeSlot.Itemstack.Attributes.RemoveAttribute("filterPatterns");
                }
                else
                {
                    activeSlot.Itemstack.Attributes["filterPatterns"] = new Vintagestory.API.Datastructures.StringArrayAttribute(packet.FilterPatterns);
                }

                activeSlot.MarkDirty();
            }
        }

        public override void Dispose()
        {
            base.Dispose();
        }

    }

    // ПАКЕТЫ И КЛАССЫ

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

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class FilterUpdatePacket
    {
        [ProtoMember(1)]
        public string[] FilteredItemCodes;

        [ProtoMember(2)]
        public string[] FilterPatterns { get; set; }
    }

    public class BlockMysticalFlower : BlockBotaniaFlower
    {
        public override void OnLoaded(ICoreAPI api)
        {
            base.OnLoaded(api);
        }
    }

    [ProtoContract(ImplicitFields = ImplicitFields.AllPublic)]
    public class GaiaLightningPacket
    {
        public Vec3d StartPos;
        public Vec3d EndPos;
    }

}