using System;
using System.Collections.Generic;
using ProtoBuf;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.systems.lenses
{
    [ProtoContract]
    public class MagnetLootModePacket
    {
        [ProtoMember(1)]
        public int Mode { get; set; }
    }

    public class ModSystemMagnetismRings : ModSystem
    {
        private const string NetworkChannel = "botaniastory-magnetism";

        private const long PlayerDropCooldownMs = 4000;
        private const long LootModeHudDurationMs = 10000;

        private const double SettleDistance = 0.85;
        private const double SettleDistanceSq = SettleDistance * SettleDistance;

        private ICoreServerAPI sapi;
        private ICoreClientAPI capi;
        private IClientNetworkChannel clientChannel;

        private readonly Dictionary<string, PlayerMagnetState> playerStates = new();
        private readonly Dictionary<long, MagnetTarget> targets = new();
        private readonly Dictionary<string, bool> animalTypeCache = new();

        private GuiDialogMagnetLootMode lootModeDialog;
        private HudMagnetLootMode lootModeHud;

        private ItemStack lastClientArmStack;
        private long lootModeHudHideAt;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.Network
                .RegisterChannel(NetworkChannel)
                .RegisterMessageType<MagnetLootModePacket>();
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            capi = api;
            clientChannel = api.Network.GetChannel(NetworkChannel);

            lootModeHud = new HudMagnetLootMode(api);

            api.Input.AddHotkeyListener(OnHotKey);
            api.Event.RegisterGameTickListener(OnClientTick, 250);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;

            api.Network
                .GetChannel(NetworkChannel)
                .SetMessageHandler<MagnetLootModePacket>(OnLootModePacket);

            api.Event.RegisterGameTickListener(OnScanTick, 250);
            api.Event.RegisterGameTickListener(OnPullTick, 50);

            api.Event.PlayerRespawn += OnPlayerRespawn;
            api.Event.PlayerDisconnect += OnPlayerDisconnect;
        }

        private void OnHotKey(string hotkeyCode, KeyCombination keyComb)
        {
            if (hotkeyCode != "toolmodeselect")
            {
                return;
            }

            IClientPlayer player = capi.World.Player;

            if (player?.Entity == null)
            {
                return;
            }

            if (HeldItemHasToolModes(player))
            {
                return;
            }

            ItemSlotCharacter armSlot = FindArmSlot(player);

            if (armSlot?.Itemstack?.Collectible is not ItemMagnetismRing ring)
            {
                return;
            }

            if (!ItemMagnetismRing.IsLootingRing(armSlot.Itemstack))
            {
                return;
            }

            SkillItem[] modes = ring.GetToolModes(
                armSlot,
                player,
                player.CurrentBlockSelection
            );

            if (modes == null || modes.Length == 0)
            {
                return;
            }

            int currentMode = ring.GetToolMode(
                armSlot,
                player,
                player.CurrentBlockSelection
            );

            lootModeDialog?.TryClose();
            lootModeDialog?.Dispose();

            lootModeDialog = new GuiDialogMagnetLootMode(
                capi,
                modes,
                currentMode,
                SetClientLootMode
            );

            lootModeDialog.TryOpen();
        }

        private static bool HeldItemHasToolModes(IClientPlayer player)
        {
            ItemSlot slot = player.InventoryManager.ActiveHotbarSlot;

            if (slot?.Itemstack == null)
            {
                return false;
            }

            SkillItem[] modes = slot.Itemstack.Collectible.GetToolModes(
                slot,
                player,
                player.CurrentBlockSelection
            );

            return modes != null && modes.Length > 0;
        }

        private void SetClientLootMode(int mode)
        {
            if (mode < 0 || mode > 2)
            {
                return;
            }

            IClientPlayer player = capi.World.Player;
            ItemSlotCharacter armSlot = FindArmSlot(player);

            if (armSlot?.Itemstack?.Collectible is not ItemMagnetismRing ring)
            {
                return;
            }

            if (!ItemMagnetismRing.IsLootingRing(armSlot.Itemstack))
            {
                return;
            }

            ring.SetToolMode(
                armSlot,
                player,
                player.CurrentBlockSelection,
                mode
            );

            clientChannel.SendPacket(
                new MagnetLootModePacket
                {
                    Mode = mode
                }
            );

            ShowLootModeHud(armSlot);
        }

        private void OnLootModePacket(
            IServerPlayer player,
            MagnetLootModePacket packet
        )
        {
            if (packet == null || packet.Mode < 0 || packet.Mode > 2)
            {
                return;
            }

            ItemSlotCharacter armSlot = FindArmSlot(player);

            if (armSlot?.Itemstack?.Collectible is not ItemMagnetismRing ring)
            {
                return;
            }

            if (!ItemMagnetismRing.IsLootingRing(armSlot.Itemstack))
            {
                return;
            }

            ring.SetToolMode(
                armSlot,
                player,
                null,
                packet.Mode
            );

            if (playerStates.TryGetValue(
                player.PlayerUID,
                out PlayerMagnetState state
            ))
            {
                state.LootMode = (MagnetLootMode)packet.Mode;
            }
        }

        private void OnClientTick(float dt)
        {
            IClientPlayer player = capi.World.Player;

            if (player?.Entity == null)
            {
                return;
            }

            ItemSlotCharacter armSlot = FindArmSlot(player);
            ItemStack stack = armSlot?.Itemstack;

            bool hasLootingRing = ItemMagnetismRing.IsLootingRing(stack);

            if (!hasLootingRing)
            {
                lastClientArmStack = stack;

                if (lootModeHud?.IsOpened() == true)
                {
                    lootModeHud.TryClose();
                }

                return;
            }

            if (!ReferenceEquals(stack, lastClientArmStack))
            {
                ShowLootModeHud(armSlot);
            }

            lastClientArmStack = stack;

            if (
                lootModeHud?.IsOpened() == true
                && capi.World.ElapsedMilliseconds >= lootModeHudHideAt
            )
            {
                lootModeHud.TryClose();
            }
        }

        private void ShowLootModeHud(ItemSlotCharacter armSlot)
        {
            if (armSlot?.Itemstack?.Collectible is not ItemMagnetismRing ring)
            {
                return;
            }

            if (!ItemMagnetismRing.IsLootingRing(armSlot.Itemstack))
            {
                return;
            }

            SkillItem[] modes = ring.GetToolModes(
                armSlot,
                capi.World.Player,
                capi.World.Player.CurrentBlockSelection
            );

            if (modes == null || modes.Length == 0)
            {
                return;
            }

            int mode = ring.GetToolMode(
                armSlot,
                capi.World.Player,
                capi.World.Player.CurrentBlockSelection
            );

            if (mode < 0 || mode >= modes.Length)
            {
                return;
            }

            lootModeHud.ShowMode(modes[mode]);

            lootModeHudHideAt =
                capi.World.ElapsedMilliseconds
                + LootModeHudDurationMs;
        }

        private void OnScanTick(float dt)
        {
            targets.Clear();

            IPlayer[] players = sapi.World.AllOnlinePlayers;
            long nowMs = sapi.World.ElapsedMilliseconds;

            for (int i = 0; i < players.Length; i++)
            {
                IPlayer player = players[i];

                if (player?.Entity == null || !player.Entity.Alive)
                {
                    continue;
                }

                PlayerMagnetState state = GetPlayerState(player);

                RefreshRingState(state);

                if (!state.Active || IsSneaking(player))
                {
                    continue;
                }

                bool canLootCorpses =
                    state.LootCorpses
                    && state.LootMode != MagnetLootMode.Disabled
                    && state.CorpseLootRadius > 0;

                float searchRadius = canLootCorpses
                    ? Math.Max(state.Radius, state.CorpseLootRadius)
                    : state.Radius;

                Vec3d playerPos = GetTargetPosition(player);

                Entity[] entities = sapi.World.GetEntitiesAround(
                    playerPos,
                    searchRadius,
                    searchRadius,
                    entity =>
                    {
                        if (entity is EntityItem)
                        {
                            return true;
                        }

                        if (!canLootCorpses || entity.Alive)
                        {
                            return false;
                        }

                        return entity.GetBehavior<EntityBehaviorHarvestable>() != null;
                    }
                );

                for (int j = 0; j < entities.Length; j++)
                {
                    Entity entity = entities[j];

                    if (entity is EntityItem entityItem)
                    {
                        AddTarget(
                            entityItem,
                            state,
                            playerPos,
                            nowMs
                        );

                        continue;
                    }

                    if (!canLootCorpses)
                    {
                        continue;
                    }

                    if (!ShouldLootCorpse(entity, state.LootMode))
                    {
                        continue;
                    }

                    TryLootCorpse(
                        player,
                        entity,
                        playerPos,
                        state.CorpseLootRadius
                    );
                }
            }
        }

        private void OnPullTick(float dt)
        {
            foreach (MagnetTarget target in targets.Values)
            {
                PlayerMagnetState state = target.State;

                if (
                    !state.Active
                    || state.Player?.Entity == null
                    || !state.Player.Entity.Alive
                    || target.Item == null
                    || !target.Item.Alive
                )
                {
                    continue;
                }

                if (IsSneaking(state.Player))
                {
                    continue;
                }

                if (
                    state.ArmSlot?.Itemstack?.Collectible
                    is not ItemMagnetismRing
                )
                {
                    continue;
                }

                PullItem(
                    target.Item,
                    GetTargetPosition(state.Player),
                    state.Radius,
                    state.Speed
                );
            }
        }

        private PlayerMagnetState GetPlayerState(IPlayer player)
        {
            if (playerStates.TryGetValue(
                player.PlayerUID,
                out PlayerMagnetState state
            ))
            {
                state.Player = player;
                return state;
            }

            state = new PlayerMagnetState
            {
                Player = player,
                ArmSlot = FindArmSlot(player)
            };

            playerStates[player.PlayerUID] = state;

            return state;
        }

        private void RefreshRingState(PlayerMagnetState state)
        {
            if (state.ArmSlot == null)
            {
                state.ArmSlot = FindArmSlot(state.Player);

                if (state.ArmSlot == null)
                {
                    state.Active = false;
                    state.LootMode = MagnetLootMode.Disabled;
                    return;
                }
            }

            ItemStack stack = state.ArmSlot.Itemstack;

            state.LootMode = ItemMagnetismRing.IsLootingRing(stack)
                ? ItemMagnetismRing.GetLootMode(stack)
                : MagnetLootMode.Disabled;

            if (ReferenceEquals(stack, state.LastStack))
            {
                return;
            }

            state.LastStack = stack;

            state.Active = false;
            state.Radius = 0;
            state.Speed = 0;
            state.LootCorpses = false;
            state.CorpseLootRadius = 0;

            if (stack?.Collectible is not ItemMagnetismRing)
            {
                return;
            }

            JsonObject attributes = stack.Collectible.Attributes;

            if (attributes == null)
            {
                return;
            }

            float radius = attributes["magnetismRadius"].AsFloat(0);
            float speed = attributes["magnetismSpeed"].AsFloat(0);

            if (radius <= 0 || speed <= 0)
            {
                return;
            }

            state.Radius = radius;
            state.Speed = speed;
            state.LootCorpses = attributes["lootCorpses"].AsBool(false);
            state.CorpseLootRadius = attributes["corpseLootRadius"].AsFloat(0);
            state.Active = true;
        }

        private void AddTarget(
            EntityItem item,
            PlayerMagnetState state,
            Vec3d playerPos,
            long nowMs
        )
        {
            if (!item.Alive)
            {
                return;
            }

            if (IsPlayerDropOnCooldown(
                item,
                state.Player.PlayerUID,
                nowMs
            ))
            {
                return;
            }

            double dx = playerPos.X - item.Pos.X;
            double dy = playerPos.Y - item.Pos.Y;
            double dz = playerPos.Z - item.Pos.Z;

            double distanceSq =
                dx * dx
                + dy * dy
                + dz * dz;

            if (distanceSq > state.Radius * state.Radius)
            {
                return;
            }

            if (targets.TryGetValue(
                item.EntityId,
                out MagnetTarget current
            ))
            {
                if (distanceSq >= current.DistanceSq)
                {
                    return;
                }

                current.Item = item;
                current.State = state;
                current.DistanceSq = distanceSq;
                return;
            }

            targets[item.EntityId] = new MagnetTarget
            {
                Item = item,
                State = state,
                DistanceSq = distanceSq
            };
        }

        private static bool IsPlayerDropOnCooldown(
            EntityItem item,
            string playerUid,
            long nowMs
        )
        {
            if (string.IsNullOrEmpty(item.ByPlayerUid))
            {
                return false;
            }

            if (item.ByPlayerUid != playerUid)
            {
                return false;
            }

            long age = nowMs - item.itemSpawnedMilliseconds;

            return age >= 0 && age < PlayerDropCooldownMs;
        }

        private static void PullItem(
            EntityItem item,
            Vec3d target,
            float radius,
            float speed
        )
        {
            double dx = target.X - item.Pos.X;
            double dy = target.Y - item.Pos.Y;
            double dz = target.Z - item.Pos.Z;

            double distanceSq =
                dx * dx
                + dy * dy
                + dz * dz;

            if (distanceSq > radius * radius)
            {
                return;
            }

            if (distanceSq <= SettleDistanceSq)
            {
                SettleItem(item);
                return;
            }

            double distance = Math.Sqrt(distanceSq);
            double multiplier = speed / distance;

            item.Pos.Motion.X = dx * multiplier;
            item.Pos.Motion.Y = dy * multiplier;
            item.Pos.Motion.Z = dz * multiplier;
        }

        private static void SettleItem(EntityItem item)
        {
            item.Pos.Motion.X = 0;
            item.Pos.Motion.Z = 0;

            if (item.Pos.Motion.Y > 0)
            {
                item.Pos.Motion.Y = 0;
            }
        }

        private bool ShouldLootCorpse(
            Entity entity,
            MagnetLootMode mode
        )
        {
            if (mode == MagnetLootMode.Disabled)
            {
                return false;
            }

            if (mode == MagnetLootMode.AllCreatures)
            {
                return true;
            }

            return !IsAnimalCorpse(entity);
        }

        private bool IsAnimalCorpse(Entity entity)
        {
            string key =
                entity.Code?.ToString()
                ?? entity.GetType().FullName
                ?? entity.EntityId.ToString();

            if (animalTypeCache.TryGetValue(
                key,
                out bool isAnimal
            ))
            {
                return isAnimal;
            }

            isAnimal = DetectAnimalCorpse(entity);
            animalTypeCache[key] = isAnimal;

            return isAnimal;
        }

        private static bool DetectAnimalCorpse(Entity entity)
        {
            JsonObject entityAttributes = entity.Properties?.Attributes;

            string forcedClass =
                entityAttributes?["botaniastoryMagnetLootClass"]
                    .AsString(null);

            if (string.Equals(
                forcedClass,
                "monster",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return false;
            }

            if (string.Equals(
                forcedClass,
                "animal",
                StringComparison.OrdinalIgnoreCase
            ))
            {
                return true;
            }

            JsonObject[] behaviors =
                entity.SidedProperties?.BehaviorsAsJsonObj;

            if (behaviors == null)
            {
                return true;
            }

            JsonObject harvestable = null;

            for (int i = 0; i < behaviors.Length; i++)
            {
                JsonObject behavior = behaviors[i];
                string code = behavior["code"].AsString() ?? "";

                if (code.IndexOf(
                    "butcher",
                    StringComparison.OrdinalIgnoreCase
                ) >= 0)
                {
                    return true;
                }

                if (string.Equals(
                    code,
                    "harvestable",
                    StringComparison.OrdinalIgnoreCase
                ))
                {
                    harvestable = behavior;
                }
            }

            if (harvestable == null)
            {
                return true;
            }

            bool fixedWeight = harvestable["fixedweight"].AsBool(false);

            return !fixedWeight;
        }

        private static bool IsSneaking(IPlayer player)
        {
            return player?.Entity?.ServerControls?.Sneak == true;
        }

        private static Vec3d GetTargetPosition(IPlayer player)
        {
            return new Vec3d(
                player.Entity.Pos.X,
                player.Entity.Pos.Y + 0.6,
                player.Entity.Pos.Z
            );
        }

        private static ItemSlotCharacter FindArmSlot(IPlayer player)
        {
            if (player == null)
            {
                return null;
            }

            IInventory inventory =
                player.InventoryManager.GetOwnInventory(
                    GlobalConstants.characterInvClassName
                );

            if (inventory == null)
            {
                return null;
            }

            for (int i = 0; i < inventory.Count; i++)
            {
                if (
                    inventory[i] is ItemSlotCharacter slot
                    && slot.Type == EnumCharacterDressType.Arm
                )
                {
                    return slot;
                }
            }

            return null;
        }

        private static void TryLootCorpse(
            IPlayer player,
            Entity entity,
            Vec3d playerPos,
            float radius
        )
        {
            double dx = playerPos.X - entity.Pos.X;
            double dy = playerPos.Y - entity.Pos.Y;
            double dz = playerPos.Z - entity.Pos.Z;

            double distanceSq =
                dx * dx
                + dy * dy
                + dz * dz;

            if (distanceSq > radius * radius)
            {
                return;
            }

            EntityBehaviorHarvestable harvestable =
                entity.GetBehavior<EntityBehaviorHarvestable>();

            if (harvestable == null)
            {
                return;
            }

            if (!harvestable.IsHarvested)
            {
                harvestable.SetHarvested(player);
            }

            if (!harvestable.Inventory.Empty)
            {
                harvestable.Inventory.DropAll(entity.Pos.XYZ);
            }

            if (harvestable.Inventory.Empty)
            {
                entity
                    .GetBehavior<EntityBehaviorDeadDecay>()
                    ?.DecayNow();
            }
        }

        private void OnPlayerRespawn(IServerPlayer player)
        {
            RemovePlayerState(player);
        }

        private void OnPlayerDisconnect(IServerPlayer player)
        {
            RemovePlayerState(player);
        }

        private void RemovePlayerState(IServerPlayer player)
        {
            if (player == null)
            {
                return;
            }

            if (playerStates.TryGetValue(
                player.PlayerUID,
                out PlayerMagnetState state
            ))
            {
                state.Active = false;
            }

            playerStates.Remove(player.PlayerUID);
        }

        public override void Dispose()
        {
            lootModeDialog?.Dispose();
            lootModeDialog = null;

            lootModeHud?.Dispose();
            lootModeHud = null;

            playerStates.Clear();
            targets.Clear();
            animalTypeCache.Clear();

            base.Dispose();
        }

        private sealed class PlayerMagnetState
        {
            public IPlayer Player;
            public ItemSlotCharacter ArmSlot;
            public ItemStack LastStack;

            public bool Active;
            public float Radius;
            public float Speed;
            public bool LootCorpses;
            public float CorpseLootRadius;
            public MagnetLootMode LootMode;
        }

        private sealed class MagnetTarget
        {
            public EntityItem Item;
            public PlayerMagnetState State;
            public double DistanceSq;
        }
    }

    public sealed class GuiDialogMagnetLootMode : GuiDialog
    {
        private readonly SkillItem[] modes;
        private readonly Action<int> onSelected;
        private readonly int selectedMode;

        public override string ToggleKeyCombinationCode => null;

        public GuiDialogMagnetLootMode(
            ICoreClientAPI capi,
            SkillItem[] modes,
            int selectedMode,
            Action<int> onSelected
        ) : base(capi)
        {
            this.modes = modes;
            this.selectedMode = selectedMode;
            this.onSelected = onSelected;
        }

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            ComposeDialog();
        }

        private void ComposeDialog()
        {
            ClearComposers();

            int cols = modes.Length;
            int rows = 1;

            double size =
                GuiElementPassiveItemSlot.unscaledSlotSize
                + GuiElementItemSlotGrid.unscaledSlotPadding;

            double innerWidth = cols * size;

            string title = Lang.Get(
                "botaniastory:magnet-lootmode-title"
            );

            for (int i = 0; i < modes.Length; i++)
            {
                innerWidth = Math.Max(
                    innerWidth,
                    CairoFont
                        .WhiteSmallishText()
                        .GetTextExtents(modes[i].Name)
                        .Width
                    / RuntimeEnv.GUIScale
                    + 1
                );
            }

            innerWidth = Math.Max(
                innerWidth,
                CairoFont
                    .WhiteSmallishText()
                    .GetTextExtents(title)
                    .Width
                / RuntimeEnv.GUIScale
                + 1
            );

            ElementBounds gridBounds = ElementBounds.Fixed(
                0,
                30,
                innerWidth,
                rows * size
            );

            ElementBounds textBounds = ElementBounds.Fixed(
                0,
                rows * (size + 2) + 5,
                innerWidth,
                25
            );

            ElementBounds titleBounds = ElementBounds.Fixed(
                0,
                0,
                innerWidth,
                25
            );

            List<SkillItem> modeList = new List<SkillItem>(modes);

            SingleComposer = capi.Gui
                .CreateCompo(
                    "magnetlootmodeselect",
                    ElementStdBounds.AutosizedMainDialog
                )
                .AddShadedDialogBG(
                    ElementStdBounds
                        .DialogBackground()
                        .WithFixedPadding(
                            GuiStyle.ElementToDialogPadding / 2
                        ),
                    false
                )
                .BeginChildElements()
                .AddStaticText(
                    title,
                    CairoFont.WhiteSmallishText(),
                    titleBounds
                )
                .AddSkillItemGrid(
                    modeList,
                    cols,
                    rows,
                    OnSlotClick,
                    gridBounds,
                    "skillitemgrid"
                )
                .AddDynamicText(
                    "",
                    CairoFont.WhiteSmallishText(),
                    textBounds,
                    "name"
                )
                .EndChildElements()
                .Compose();

            GuiElementSkillItemGrid grid =
                SingleComposer.GetSkillItemGrid("skillitemgrid");

            grid.OnSlotOver = OnSlotOver;

            if (selectedMode >= 0 && selectedMode < modes.Length)
            {
                grid.selectedIndex = selectedMode;

                SingleComposer
                    .GetDynamicText("name")
                    .SetNewText(modes[selectedMode].Name);
            }
        }

        private void OnSlotOver(int num)
        {
            if (num < 0 || num >= modes.Length)
            {
                return;
            }

            SingleComposer
                .GetDynamicText("name")
                .SetNewText(modes[num].Name);
        }

        private void OnSlotClick(int num)
        {
            if (num < 0 || num >= modes.Length)
            {
                return;
            }

            onSelected(num);
            TryClose();
        }
    }

    public sealed class HudMagnetLootMode : HudElement
    {
        public HudMagnetLootMode(ICoreClientAPI capi)
            : base(capi)
        {
        }

        public void ShowMode(SkillItem mode)
        {
            if (mode == null)
            {
                return;
            }

            if (IsOpened())
            {
                TryClose();
            }

            Compose(mode);
            TryOpen();
        }

        private void Compose(SkillItem mode)
        {
            ClearComposers();

            double size =
                GuiElementPassiveItemSlot.unscaledSlotSize
                + GuiElementItemSlotGrid.unscaledSlotPadding;

            ElementBounds dialogBounds = ElementBounds
                .Fixed(
                    0,
                    0,
                    size + 12,
                    size + 12
                )
                .WithAlignment(EnumDialogArea.RightBottom)
                .WithFixedAlignmentOffset(-20, -115);

            ElementBounds gridBounds = ElementBounds.Fixed(
                6,
                6,
                size,
                size
            );

            List<SkillItem> modeList = new List<SkillItem>
            {
                mode
            };

            SingleComposer = capi.Gui
                .CreateCompo(
                    "magnetloothud",
                    dialogBounds
                )
                .AddShadedDialogBG(
                    ElementBounds.Fill,
                    false,
                    3,
                    0.65f
                )
                .AddSkillItemGrid(
                    modeList,
                    1,
                    1,
                    num => { },
                    gridBounds,
                    "modeicon"
                )
                .Compose();

            SingleComposer
                .GetSkillItemGrid("modeicon")
                .selectedIndex = 0;
        }
    }
}
