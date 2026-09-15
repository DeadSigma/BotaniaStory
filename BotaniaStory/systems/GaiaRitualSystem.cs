using BotaniaStory.blocks;
using BotaniaStory.entities;
using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;

namespace BotaniaStory.ritual
{
    public class GaiaRitualSystem : ModSystem
    {
        private const string GaiaEntityCode = "botaniastory:gaiaguardian";

        private const string TerrasteelIngotCode = "game:ingot-terrasteel";
        private const string EmpoweredTerrasteelIngotCode = "botaniastory:ingot-terrasteel-empowered";

        private const string PlatformCode = "game:metalblock-new-riveted-elementium";
        private const string BeaconCode = "botaniastory:beacon";
        private const string PylonCode = "botaniastory:pylon-gaia";

        private static readonly (int dx, int dz)[] PylonOffsets =
        {
            ( 4,  4),
            ( 4, -4),
            (-4,  4),
            (-4, -4)
        };

        private const int PylonYOffset = 1;
        private const int BeaconSearchH = 4;
        private const int BeaconSearchV = 4;

        public static GaiaRitualSystem ServerInstance { get; private set; }

        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;
            ServerInstance = this;


            api.ChatCommands.Create("spawngaia")
                .WithDescription("Призвать Гайю для тестирования")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(OnSpawnGaia);
        }

        private int GetGaiaLevel(string itemCode)
        {
            if (itemCode == TerrasteelIngotCode) return 1;
            if (itemCode == EmpoweredTerrasteelIngotCode) return 2;
            return 0;
        }

        private TextCommandResult OnSpawnGaia(TextCommandCallingArgs args)
        {
            IServerPlayer sp = args.Caller.Player as IServerPlayer;
            if (sp?.Entity == null) return TextCommandResult.Success();

            BlockPos beacon = FindBeaconNear(sp.Entity.Pos.AsBlockPos);
            if (beacon == null) return TextCommandResult.Success();
            if (!IsStructureValid(beacon)) return TextCommandResult.Success();
            if (HasActiveGaia(beacon)) return TextCommandResult.Success();

            SpawnGaia(beacon, 1);
            return TextCommandResult.Success();
        }

        private BlockPos FindBeaconNear(BlockPos origin)
        {
            BlockPos best = null;
            int bestSq = int.MaxValue;

            for (int dy = -BeaconSearchV; dy <= BeaconSearchV; dy++)
            {
                for (int dx = -BeaconSearchH; dx <= BeaconSearchH; dx++)
                {
                    for (int dz = -BeaconSearchH; dz <= BeaconSearchH; dz++)
                    {
                        BlockPos p = origin.AddCopy(dx, dy, dz);
                        if (!MatchBlock(p, BeaconCode)) continue;

                        int distSq = dx * dx + dy * dy + dz * dz;
                        if (distSq >= bestSq) continue;

                        bestSq = distSq;
                        best = p;
                    }
                }
            }

            return best;
        }

        private bool IsStructureValid(BlockPos beacon)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!MatchBlock(beacon.AddCopy(dx, -1, dz), PlatformCode))
                        return false;
                }
            }

            foreach (var off in PylonOffsets)
            {
                if (!MatchBlock(
                    beacon.AddCopy(off.dx, PylonYOffset, off.dz),
                    PylonCode))
                {
                    return false;
                }
            }

            return true;
        }

        private bool IsBeaconActive(BlockPos beacon)
        {
            return sapi.World.BlockAccessor.GetBlockEntity(beacon)
                is BlockEntityBeacon blockEntity && blockEntity.IsActive;
        }

        private bool HasActiveGaia(BlockPos beacon)
        {
            Vec3d center = new(
                beacon.X + 0.5,
                beacon.Y + 1.0,
                beacon.Z + 0.5
            );

            Entity[] entities = sapi.World.GetEntitiesAround(
                center,
                EntityGaiaGuardian.ArenaRadius + 6f,
                32f,
                e => e.Alive && e.Code?.ToString() == GaiaEntityCode
            );

            return entities != null && entities.Length > 0;
        }

        private bool SpawnGaia(BlockPos beacon, int gaiaLevel)
        {
            EntityProperties type =
                sapi.World.GetEntityType(new AssetLocation(GaiaEntityCode));

            if (type == null)
            {
                sapi.World.Logger.Error(
                    "[BotaniaStory] Entity type not found: {0}",
                    GaiaEntityCode
                );
                return false;
            }

            Entity gaia = sapi.World.ClassRegistry.CreateEntity(type);
            if (gaia == null) return false;

            gaia.Pos.SetPos(
                beacon.X + 0.5,
                beacon.Y + 1,
                beacon.Z + 0.5
            );

            gaia.WatchedAttributes.SetInt(
                "gaiaPlayerCount",
                CountPlayersInArena(beacon)
            );

            gaia.WatchedAttributes.SetInt(
                "gaiaLevel",
                gaiaLevel
            );

            sapi.World.SpawnEntity(gaia);
            return true;
        }

        private int CountPlayersInArena(BlockPos beacon)
        {
            double cx = beacon.X + 0.5;
            double cz = beacon.Z + 0.5;
            const float countRadius = EntityGaiaGuardian.ArenaRadius;

            int count = 0;

            foreach (IPlayer p in sapi.World.AllOnlinePlayers)
            {
                EntityPlayer pe = p.Entity;
                if (pe == null || !pe.Alive) continue;

                EnumGameMode mode =
                    p.WorldData?.CurrentGameMode ?? EnumGameMode.Survival;

                if (mode == EnumGameMode.Spectator ||
                    (!EntityGaiaGuardian.AllowCreativeParticipants && mode == EnumGameMode.Creative))
                {
                    continue;
                }

                double dx = pe.Pos.X - cx;
                double dz = pe.Pos.Z - cz;

                if (dx * dx + dz * dz <= countRadius * countRadius)
                    count++;
            }

            return Math.Max(1, count);
        }
        public bool TryStartRitual(
            IServerPlayer player,
            BlockPos beacon,
            int gaiaLevel)
        {
            if (!IsBeaconActive(beacon))
            {
                SendRitualMessage(
                    player,
                    "botaniastory:gaia-ritual-beacon-inactive"
                );

                return false;
            }

            if (!IsStructureValid(beacon))
            {
                SendRitualMessage(
                    player,
                    "botaniastory:gaia-ritual-structure-invalid"
                );

                return false;
            }

            if (HasActiveGaia(beacon))
            {
                SendRitualMessage(
                    player,
                    "botaniastory:gaia-ritual-already-active"
                );

                return false;
            }

            if (!SpawnGaia(beacon, gaiaLevel))
            {
                SendRitualMessage(
                    player,
                    "botaniastory:gaia-ritual-spawn-failed"
                );

                return false;
            }

            ItemSlot slot =
                player.InventoryManager.ActiveHotbarSlot;

            if (player.WorldData.CurrentGameMode !=
                EnumGameMode.Creative)
            {
                slot.TakeOut(1);
                slot.MarkDirty();
            }

            SendRitualMessage(
                player,
                gaiaLevel >= 2
                    ? "botaniastory:gaia-ritual-started-level2"
                    : "botaniastory:gaia-ritual-started-level1"
            );

            return true;
        }
        private bool MatchBlock(BlockPos pos, string code)
        {
            Block block = sapi.World.BlockAccessor.GetBlock(pos);
            if (block?.Code == null) return false;

            string full = block.Code.ToString();

            return full == code ||
                   full.StartsWith(code + "-", StringComparison.Ordinal);
        }
        private static void SendRitualMessage(
     IServerPlayer player,
     string langKey)
        {
            player.SendLocalisedMessage(
                GlobalConstants.GeneralChatGroup,
                langKey
            );
        }
    }
}
