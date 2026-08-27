using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using BotaniaStory.entities;

namespace BotaniaStory.ritual
{
   
    public class GaiaRitualSystem : ModSystem
    {
        private const string GaiaEntityCode = "botaniastory:gaiaguardian"; 
        private const string PlatformCode = "game:metalblock-new-riveted-copper"; 
        private const string BeaconCode = "game:metalblock-new-riveted-copper";             
        private const string PylonCode = "botaniastory:pylon-gaia";       

        // Смещения пилонов от центра (X,Z): 4 пилона по диагоналям на 4-м блоке.
        private static readonly (int dx, int dz)[] PylonOffsets =
        {
            ( 4,  4),
            ( 4, -4),
            (-4,  4),
            (-4, -4)
        };
        private const int PylonYOffset = 1; // пилоны на уровень выше маяка (уровень 2 = by+1)

        private const int BeaconSearchH = 4; // радиус поиска маяка вокруг игрока (по горизонтали)
        private const int BeaconSearchV = 4; // и по вертикали

        private ICoreServerAPI sapi;

        public override void StartServerSide(ICoreServerAPI api)
        {
            this.sapi = api;

            api.ChatCommands.Create("spawngaia")
                .WithDescription("Призвать Гайю: встань в центр структуры арены и выполни команду")
                .RequiresPrivilege(Privilege.controlserver)
                .RequiresPlayer()
                .HandleWith(OnSpawnGaia);
        }

        private TextCommandResult OnSpawnGaia(TextCommandCallingArgs args)
        {
            IServerPlayer sp = args.Caller.Player as IServerPlayer;
            if (sp?.Entity == null) return TextCommandResult.Success();

            BlockPos beacon = FindBeaconNear(sp.Entity.Pos.AsBlockPos);
            if (beacon == null) return TextCommandResult.Success();

            if (!IsStructureValid(beacon)) return TextCommandResult.Success();

            SpawnGaia(beacon);
            return TextCommandResult.Success();
        }

        // Ищем ближайший к игроку блок-маяк в небольшом объёме
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
                        if (distSq < bestSq) { bestSq = distSq; best = p; }
                    }
                }
            }
            return best;
        }

        private bool IsStructureValid(BlockPos beacon)
        {
            // Платформа 3x3 на слое by-1
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dz = -1; dz <= 1; dz++)
                {
                    if (!MatchBlock(beacon.AddCopy(dx, -1, dz), PlatformCode)) return false;
                }
            }

            // Пилоны на слое by+1 по диагоналям
            foreach (var off in PylonOffsets)
            {
                if (!MatchBlock(beacon.AddCopy(off.dx, PylonYOffset, off.dz), PylonCode)) return false;
            }

            return true;
        }

        private void SpawnGaia(BlockPos beacon)
        {
            EntityProperties type = sapi.World.GetEntityType(new AssetLocation(GaiaEntityCode));
            if (type == null) return;

            Entity gaia = sapi.World.ClassRegistry.CreateEntity(type);

            // Центр по X/Z = маяк, по Y = на маяке (by+1). Барьер сам опустит огонь к полу (FloorOffset = -1).
            gaia.Pos.SetPos(beacon.X + 0.5, beacon.Y + 1, beacon.Z + 0.5);

            // Считаем живых игроков в зоне арены В МОМЕНТ ПРИЗЫВА -> от этого зависят HP/урон/лут Гайи
            gaia.WatchedAttributes.SetInt("gaiaPlayerCount", CountPlayersInArena(beacon));

            sapi.World.SpawnEntity(gaia);
        }

        // Живые игроки в радиусе арены (+3 запаса) от маяка
        private int CountPlayersInArena(BlockPos beacon)
        {
            double cx = beacon.X + 0.5;
            double cz = beacon.Z + 0.5;
            const float countRadius = EntityGaiaGuardian.ArenaRadius + 3f;

            int count = 0;
            foreach (IPlayer p in sapi.World.AllOnlinePlayers)
            {
                EntityPlayer pe = p.Entity;
                if (pe == null || !pe.Alive) continue;

                double dx = pe.Pos.X - cx;
                double dz = pe.Pos.Z - cz;
                if (dx * dx + dz * dz <= countRadius * countRadius) count++;
            }
            return Math.Max(1, count);
        }

        // Совпадение блока: точное, либо префикс с "-" (ловит варианты ориентации, напр. table-normal-north)
        private bool MatchBlock(BlockPos pos, string code)
        {
            Block b = sapi.World.BlockAccessor.GetBlock(pos);
            if (b?.Code == null) return false;

            string full = b.Code.ToString();
            return full == code || full.StartsWith(code + "-", StringComparison.Ordinal);
        }
    }
}