using BotaniaStory.client.renderers;
using BotaniaStory.items;
using HarmonyLib;
using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Reflection;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace BotaniaStory.systems
{
    public class TerraShattererCaveBarrierSystem : ModSystem
    {
        private const string HarmonyId = "botaniastory.terrashatterer.cavebarrier";
        private const string ChannelId = "terrashattererbarrier";

        private static readonly Dictionary<BarrierKey, HashSet<string>> serverBarrierOwners = new Dictionary<BarrierKey, HashSet<string>>();
        private static readonly Dictionary<string, HashSet<BarrierKey>> serverOwnerBarriers = new Dictionary<string, HashSet<BarrierKey>>();
        private static readonly HashSet<BarrierKey> clientBarriers = new HashSet<BarrierKey>();

        private static ICoreServerAPI sapi;
        private static IServerNetworkChannel serverChannel;

        private IClientNetworkChannel clientChannel;
        private Harmony harmony;
        private TerraShattererCaveBarrierRenderer renderer;
        private long ownerCleanupListenerId;
        private long validityCleanupListenerId;

        public override void Start(ICoreAPI api)
        {
            base.Start(api);

            api.Network.RegisterChannel(ChannelId)
                .RegisterMessageType<TerraBarrierUpdatePacket>()
                .RegisterMessageType<TerraBarrierSyncPacket>();

            harmony = new Harmony(HarmonyId);
            PatchCaveStability(api);
        }

        public override void StartServerSide(ICoreServerAPI api)
        {
            base.StartServerSide(api);

            sapi = api;
            serverChannel = api.Network.GetChannel(ChannelId) as IServerNetworkChannel;

            serverBarrierOwners.Clear();
            serverOwnerBarriers.Clear();

            PatchDroppedItemRemoval(api);

            api.Event.PlayerNowPlaying += OnPlayerNowPlaying;
            ownerCleanupListenerId = api.Event.RegisterGameTickListener(CleanupInactiveOwners, 100);
            validityCleanupListenerId = api.Event.RegisterGameTickListener(CleanupInvalidBarriers, 5000);
        }

        public override void StartClientSide(ICoreClientAPI api)
        {
            base.StartClientSide(api);

            clientBarriers.Clear();
            clientChannel = api.Network.GetChannel(ChannelId) as IClientNetworkChannel;
            clientChannel?.SetMessageHandler<TerraBarrierUpdatePacket>(OnBarrierUpdate);
            clientChannel?.SetMessageHandler<TerraBarrierSyncPacket>(OnBarrierSync);

            renderer = new TerraShattererCaveBarrierRenderer(api);
        }

        private void PatchDroppedItemRemoval(ICoreServerAPI api)
        {
            MethodInfo dieMethod = AccessTools.Method(
                typeof(EntityItem),
                nameof(EntityItem.Die),
                new[] { typeof(EnumDespawnReason), typeof(DamageSource) }
            );

            if (dieMethod != null)
            {
                harmony.Patch(
                    dieMethod,
                    prefix: new HarmonyMethod(typeof(TerraShattererCaveBarrierSystem), nameof(EntityItemDiePrefix))
                );
            }

            MethodInfo despawnMethod = AccessTools.Method(
                api.World.GetType(),
                "DespawnEntity",
                new[] { typeof(Entity), typeof(EntityDespawnData) }
            );

            if (despawnMethod != null)
            {
                harmony.Patch(
                    despawnMethod,
                    prefix: new HarmonyMethod(typeof(TerraShattererCaveBarrierSystem), nameof(DespawnEntityPrefix))
                );
            }
        }

        private void PatchCaveStability(ICoreAPI api)
        {
            MethodInfo supportMethod = AccessTools.Method(
                typeof(BlockBehaviorUnstableRock),
                nameof(BlockBehaviorUnstableRock.getVerticalSupportStrength),
                new[] { typeof(IWorldAccessor), typeof(BlockPos) }
            );

            if (supportMethod == null)
            {
                api.Logger.Warning("[BotaniaStory] Не найден BlockBehaviorUnstableRock.getVerticalSupportStrength");
                return;
            }

            harmony.Patch(
                supportMethod,
                postfix: new HarmonyMethod(typeof(TerraShattererCaveBarrierSystem), nameof(VerticalSupportPostfix))
            );
        }

        // Барьер считается обычной вертикальной опорой
        private static void VerticalSupportPostfix(IWorldAccessor world, BlockPos npos, ref int __result)
        {
            if (__result > 0 || world == null || npos == null) return;
            if (!CaveInsEnabled(world)) return;

            BarrierKey key = new BarrierKey(npos.X, npos.Y - 1, npos.Z, npos.dimension);
            if (!HasBarrier(world.Side, key)) return;

            __result = 1;
        }

        // Барьер создаётся только после копки активным Землекрушителем
        public static void RegisterBrokenBlock(IWorldAccessor world, BlockPos brokenPos, IPlayer player)
        {
            if (world?.Side != EnumAppSide.Server || brokenPos == null || player == null || sapi == null) return;
            if (!CaveInsEnabled(world)) return;
            if (!IsHoldingActiveTerraShatterer(player)) return;

            string playerUid = player.PlayerUID;
            if (string.IsNullOrEmpty(playerUid)) return;

            BarrierKey oldKey = new BarrierKey(brokenPos.X, brokenPos.Y - 1, brokenPos.Z, brokenPos.dimension);
            RemoveOwnerFromBarrier(oldKey, playerUid);

            BlockPos abovePos = brokenPos.UpCopy();
            Block aboveBlock = world.BlockAccessor.GetBlock(abovePos, BlockLayersAccess.Solid);
            if (aboveBlock?.GetBehavior<BlockBehaviorUnstableRock>() == null) return;

            AddOwnerToBarrier(
                new BarrierKey(brokenPos.X, brokenPos.Y, brokenPos.Z, brokenPos.dimension),
                playerUid
            );
        }

        private static bool IsHoldingActiveTerraShatterer(IPlayer player)
        {
            ItemStack stack = player?.InventoryManager?.ActiveHotbarSlot?.Itemstack;
            return ItemTerraShatterer.IsActive(stack);
        }

        private static void AddOwnerToBarrier(BarrierKey key, string playerUid)
        {
            if (!serverBarrierOwners.TryGetValue(key, out HashSet<string> owners))
            {
                owners = new HashSet<string>(StringComparer.Ordinal);
                serverBarrierOwners[key] = owners;
            }

            if (!owners.Add(playerUid)) return;

            if (!serverOwnerBarriers.TryGetValue(playerUid, out HashSet<BarrierKey> barriers))
            {
                barriers = new HashSet<BarrierKey>();
                serverOwnerBarriers[playerUid] = barriers;
            }

            barriers.Add(key);

            if (owners.Count != 1) return;

            serverChannel?.BroadcastPacket(new TerraBarrierUpdatePacket
            {
                X = key.X,
                Y = key.Y,
                Z = key.Z,
                Dimension = key.Dimension,
                Active = true
            });
        }

        private static void RemoveOwnerFromBarrier(BarrierKey key, string playerUid)
        {
            if (!serverBarrierOwners.TryGetValue(key, out HashSet<string> owners)) return;
            if (!owners.Remove(playerUid)) return;

            if (serverOwnerBarriers.TryGetValue(playerUid, out HashSet<BarrierKey> barriers))
            {
                barriers.Remove(key);
                if (barriers.Count == 0) serverOwnerBarriers.Remove(playerUid);
            }

            if (owners.Count > 0) return;

            serverBarrierOwners.Remove(key);
            BroadcastBarrierRemoved(key);
        }

        private static void RemoveBarrier(BarrierKey key)
        {
            if (!serverBarrierOwners.TryGetValue(key, out HashSet<string> owners)) return;

            List<string> ownerCopy = new List<string>(owners);
            foreach (string playerUid in ownerCopy)
            {
                if (!serverOwnerBarriers.TryGetValue(playerUid, out HashSet<BarrierKey> barriers)) continue;

                barriers.Remove(key);
                if (barriers.Count == 0) serverOwnerBarriers.Remove(playerUid);
            }

            serverBarrierOwners.Remove(key);
            BroadcastBarrierRemoved(key);
        }

        private static void RemoveAllOwnerBarriers(string playerUid)
        {
            if (!serverOwnerBarriers.TryGetValue(playerUid, out HashSet<BarrierKey> barriers)) return;

            List<BarrierKey> barrierCopy = new List<BarrierKey>(barriers);
            foreach (BarrierKey key in barrierCopy)
            {
                RemoveOwnerFromBarrier(key, playerUid);
            }
        }

        private static void BroadcastBarrierRemoved(BarrierKey key)
        {
            serverChannel?.BroadcastPacket(new TerraBarrierUpdatePacket
            {
                X = key.X,
                Y = key.Y,
                Z = key.Z,
                Dimension = key.Dimension,
                Active = false
            });
        }

        // Барьеры снимаются сразу после смены предмета в руке
        private void CleanupInactiveOwners(float dt)
        {
            if (sapi?.World == null || serverOwnerBarriers.Count == 0) return;

            HashSet<string> activeOwners = new HashSet<string>(StringComparer.Ordinal);

            foreach (IServerPlayer player in sapi.World.AllOnlinePlayers)
            {
                if (player?.ConnectionState != EnumClientState.Playing) continue;
                if (!IsHoldingActiveTerraShatterer(player)) continue;
                if (string.IsNullOrEmpty(player.PlayerUID)) continue;

                activeOwners.Add(player.PlayerUID);
            }

            List<string> staleOwners = null;

            foreach (string playerUid in serverOwnerBarriers.Keys)
            {
                if (activeOwners.Contains(playerUid)) continue;

                if (staleOwners == null) staleOwners = new List<string>();
                staleOwners.Add(playerUid);
            }

            if (staleOwners == null) return;

            foreach (string playerUid in staleOwners)
            {
                RemoveAllOwnerBarriers(playerUid);
            }
        }

        private void CleanupInvalidBarriers(float dt)
        {
            if (sapi?.World == null || serverBarrierOwners.Count == 0) return;

            List<BarrierKey> stale = null;

            foreach (BarrierKey key in serverBarrierOwners.Keys)
            {
                if (IsBarrierValid(sapi.World, key)) continue;

                if (stale == null) stale = new List<BarrierKey>();
                stale.Add(key);
            }

            if (stale == null) return;

            foreach (BarrierKey key in stale)
            {
                RemoveBarrier(key);
            }
        }

        public static bool IsBarrierValid(IWorldAccessor world, int x, int y, int z, int dimension)
        {
            return IsBarrierValid(world, new BarrierKey(x, y, z, dimension));
        }

        private static bool IsBarrierValid(IWorldAccessor world, BarrierKey key)
        {
            if (world == null) return false;

            BlockPos cellPos = new BlockPos(key.X, key.Y, key.Z, key.Dimension);
            Block cellBlock = world.BlockAccessor.GetBlock(cellPos, BlockLayersAccess.Solid);
            if (cellBlock.Id != 0) return false;

            BlockPos abovePos = cellPos.UpCopy();
            Block aboveBlock = world.BlockAccessor.GetBlock(abovePos, BlockLayersAccess.Solid);
            return aboveBlock?.GetBehavior<BlockBehaviorUnstableRock>() != null;
        }

        public static List<BlockPos> GetClientBarrierSnapshot()
        {
            List<BlockPos> result = new List<BlockPos>(clientBarriers.Count);

            foreach (BarrierKey key in clientBarriers)
            {
                result.Add(new BlockPos(key.X, key.Y, key.Z, key.Dimension));
            }

            return result;
        }

        private static bool HasBarrier(EnumAppSide side, BarrierKey key)
        {
            return side == EnumAppSide.Server
                ? serverBarrierOwners.ContainsKey(key)
                : clientBarriers.Contains(key);
        }

        private void OnBarrierUpdate(TerraBarrierUpdatePacket packet)
        {
            BarrierKey key = new BarrierKey(packet.X, packet.Y, packet.Z, packet.Dimension);

            if (packet.Active) clientBarriers.Add(key);
            else clientBarriers.Remove(key);
        }

        private void OnBarrierSync(TerraBarrierSyncPacket packet)
        {
            clientBarriers.Clear();
            if (packet?.Cells == null) return;

            foreach (TerraBarrierCellData cell in packet.Cells)
            {
                clientBarriers.Add(new BarrierKey(cell.X, cell.Y, cell.Z, cell.Dimension));
            }
        }

        private void OnPlayerNowPlaying(IServerPlayer player)
        {
            TerraBarrierSyncPacket packet = new TerraBarrierSyncPacket
            {
                Cells = new List<TerraBarrierCellData>(serverBarrierOwners.Count)
            };

            foreach (BarrierKey key in serverBarrierOwners.Keys)
            {
                packet.Cells.Add(new TerraBarrierCellData
                {
                    X = key.X,
                    Y = key.Y,
                    Z = key.Z,
                    Dimension = key.Dimension
                });
            }

            serverChannel?.SendPacket(packet, player);
        }

        // Истечение времени жизни Землекрушителя блокируется
        private static bool EntityItemDiePrefix(EntityItem __instance, EnumDespawnReason reason)
        {
            return !ShouldProtectDroppedItem(__instance, reason);
        }

        // Истечение времени жизни через сервер также блокируется
        private static bool DespawnEntityPrefix(Entity __0, EntityDespawnData __1)
        {
            EntityItem entityItem = __0 as EntityItem;
            if (entityItem == null || __1 == null) return true;

            return !ShouldProtectDroppedItem(entityItem, __1.Reason);
        }

        private static bool ShouldProtectDroppedItem(EntityItem entityItem, EnumDespawnReason reason)
        {
            if (entityItem == null || !ItemTerraShatterer.IsTerraShatterer(entityItem.Itemstack)) return false;

            return reason == EnumDespawnReason.Expire;
        }

        public static bool CaveInsEnabled(IWorldAccessor world)
        {
            if (world?.Config == null) return false;

            return world.Config.GetString("caveIns") == "on"
                && world.Config.GetBool("allowFallingBlocks");
        }

        public override void Dispose()
        {
            renderer?.Dispose();
            renderer = null;

            if (sapi != null)
            {
                sapi.Event.PlayerNowPlaying -= OnPlayerNowPlaying;

                if (ownerCleanupListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(ownerCleanupListenerId);
                    ownerCleanupListenerId = 0;
                }

                if (validityCleanupListenerId != 0)
                {
                    sapi.Event.UnregisterGameTickListener(validityCleanupListenerId);
                    validityCleanupListenerId = 0;
                }
            }

            if (harmony != null)
            {
                harmony.UnpatchAll(HarmonyId);
                harmony = null;
            }

            sapi = null;
            serverChannel = null;
            clientChannel = null;
            serverBarrierOwners.Clear();
            serverOwnerBarriers.Clear();
            clientBarriers.Clear();

            base.Dispose();
        }

        private struct BarrierKey : IEquatable<BarrierKey>
        {
            public readonly int X;
            public readonly int Y;
            public readonly int Z;
            public readonly int Dimension;

            public BarrierKey(int x, int y, int z, int dimension)
            {
                X = x;
                Y = y;
                Z = z;
                Dimension = dimension;
            }

            public bool Equals(BarrierKey other)
            {
                return X == other.X && Y == other.Y && Z == other.Z && Dimension == other.Dimension;
            }

            public override bool Equals(object obj)
            {
                return obj is BarrierKey other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hash = 17;
                    hash = hash * 31 + X;
                    hash = hash * 31 + Y;
                    hash = hash * 31 + Z;
                    hash = hash * 31 + Dimension;
                    return hash;
                }
            }
        }
    }

    [ProtoContract]
    public class TerraBarrierCellData
    {
        [ProtoMember(1)] public int X;
        [ProtoMember(2)] public int Y;
        [ProtoMember(3)] public int Z;
        [ProtoMember(4)] public int Dimension;
    }

    [ProtoContract]
    public class TerraBarrierUpdatePacket
    {
        [ProtoMember(1)] public int X;
        [ProtoMember(2)] public int Y;
        [ProtoMember(3)] public int Z;
        [ProtoMember(4)] public int Dimension;
        [ProtoMember(5)] public bool Active;
    }

    [ProtoContract]
    public class TerraBarrierSyncPacket
    {
        [ProtoMember(1)] public List<TerraBarrierCellData> Cells;
    }
}
