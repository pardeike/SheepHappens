using RimBridgeServer.Annotations;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace SheepHappens
{
    public sealed class SheepHappensBridgeTools
    {
        static Map CurrentMap => Find.CurrentMap;

        static string StableThingId(Thing thing)
        {
            return thing == null ? null : $"Thing_{thing.ThingID}";
        }

        static object DescribeCell(IntVec3 cell)
        {
            if (cell.IsValid == false)
                return null;

            return new
            {
                x = cell.x,
                z = cell.z
            };
        }

        static bool TryParseRotation(string direction, out Rot4 rotation)
        {
            rotation = Rot4.Invalid;
            if (string.IsNullOrWhiteSpace(direction))
                return false;

            switch (direction.Trim().ToLowerInvariant())
            {
                case "north":
                case "n":
                    rotation = Rot4.North;
                    return true;
                case "east":
                case "e":
                    rotation = Rot4.East;
                    return true;
                case "south":
                case "s":
                    rotation = Rot4.South;
                    return true;
                case "west":
                case "w":
                    rotation = Rot4.West;
                    return true;
                default:
                    return false;
            }
        }

        static object DescribePawn(Pawn pawn)
        {
            var state = pawn != null && pawn.IsColonist ? pawn.GetState() : null;
            var bestEnemyPosition = pawn != null && pawn.def == Constants.sheepThingDef ? Tools.BestEnemyPosition(pawn) : IntVec3.Invalid;
            var apparel = pawn?.apparel?.WornApparel?
                .Select(item => new
                {
                    pawnId = StableThingId(item),
                    defName = item.def.defName,
                    label = item.LabelCap,
                    locked = pawn.apparel.IsLocked(item)
                })
                .ToArray() ?? Array.Empty<object>();

            return new
            {
                pawnId = StableThingId(pawn),
                thingId = pawn?.ThingID,
                defName = pawn?.def?.defName,
                label = pawn?.LabelCap,
                shortLabel = pawn?.LabelShortCap,
                name = pawn?.Name?.ToStringShort ?? pawn?.LabelShort,
                faction = pawn?.Faction?.Name,
                factionDef = pawn?.Faction?.def?.defName,
                isPlayerOwned = pawn?.Faction == Faction.OfPlayer,
                isColonist = pawn?.IsColonist ?? false,
                spawned = pawn?.Spawned ?? false,
                dead = pawn?.Dead ?? false,
                downed = pawn?.Downed ?? false,
                drafted = pawn?.Drafted ?? false,
                burning = pawn?.IsBurning() ?? false,
                rotation = pawn?.Rotation.AsInt switch
                {
                    0 => "north",
                    1 => "east",
                    2 => "south",
                    3 => "west",
                    _ => null
                },
                position = pawn == null ? null : DescribeCell(pawn.Position),
                currentJob = pawn?.CurJobDef?.defName,
                currentJobReport = pawn?.CurJob?.GetReport(pawn),
                maskUntilTick = state?.until ?? 0,
                maskActive = pawn != null && Tools.IsMaskWearer(pawn),
                bestEnemyPosition = DescribeCell(bestEnemyPosition),
                apparel
            };
        }

        static Pawn FindPawn(string pawnId)
        {
            var map = CurrentMap;
            if (map == null)
                return null;

            if (string.IsNullOrWhiteSpace(pawnId))
                return Find.Selector.SingleSelectedThing as Pawn;

            return map.mapPawns.AllPawnsSpawned.FirstOrDefault(pawn =>
                string.Equals(pawn.ThingID, pawnId, StringComparison.Ordinal)
                || string.Equals(StableThingId(pawn), pawnId, StringComparison.Ordinal)
                || string.Equals(pawn.GetUniqueLoadID(), pawnId, StringComparison.Ordinal)
                || string.Equals(pawn.Name?.ToStringShort, pawnId, StringComparison.OrdinalIgnoreCase)
                || string.Equals(pawn.LabelShort, pawnId, StringComparison.OrdinalIgnoreCase));
        }

        static bool TryFindSpawnCell(int x, int z, out Map map, out IntVec3 cell, out string error)
        {
            map = CurrentMap;
            cell = IntVec3.Invalid;
            error = null;

            if (map == null)
            {
                error = "No current map is loaded.";
                return false;
            }

            var root = new IntVec3(x, 0, z);
            if (root.InBounds(map) == false)
            {
                error = $"Cell ({x}, {z}) is outside the current map.";
                return false;
            }

            foreach (var candidate in GenRadial.RadialCellsAround(root, 6f, true))
            {
                if (candidate.InBounds(map) == false)
                    continue;
                if (candidate.Standable(map) == false)
                    continue;

                cell = candidate;
                return true;
            }

            error = $"No standable cell was found near ({x}, {z}).";
            return false;
        }

        static Faction FindHostileHumanlikeFaction()
        {
            return Find.FactionManager?.AllFactionsVisible?
                .Where(faction => faction != null)
                .Where(faction => faction.IsPlayer == false)
                .Where(faction => faction.def?.humanlikeFaction == true)
                .Where(faction => faction.HostileTo(Faction.OfPlayer))
                .OrderBy(faction => faction.def.defName, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        [Tool("sheephappens/list_sheep", Description = "List spawned sheep on the current map, including stable ids and Sheep Happens state.")]
        public static object ListSheep([ToolParameter(Description = "When true, only return sheep owned by the player.", Required = false, DefaultValue = false)] bool playerOwnedOnly = false)
        {
            var map = CurrentMap;
            if (map == null)
            {
                return new
                {
                    success = false,
                    error = "No current map is loaded."
                };
            }

            var sheep = map.mapPawns.AllPawnsSpawned
                .Where(pawn => pawn.def == Constants.sheepThingDef)
                .Where(pawn => playerOwnedOnly == false || pawn.Faction == Faction.OfPlayer)
                .Select(DescribePawn)
                .ToArray();

            return new
            {
                success = true,
                count = sheep.Length,
                sheep
            };
        }

        [Tool("sheephappens/get_pawn_state", Description = "Read detailed live state for one pawn, or the currently selected pawn when no id is provided.")]
        public static object GetPawnState([ToolParameter(Description = "Optional stable pawn id such as Thing_Human862 or Thing_Sheep1234.", Required = false, DefaultValue = null)] string pawnId = null)
        {
            var pawn = FindPawn(pawnId);
            if (pawn == null)
            {
                return new
                {
                    success = false,
                    error = string.IsNullOrWhiteSpace(pawnId)
                        ? "No single pawn is selected."
                        : $"Pawn '{pawnId}' was not found on the current map."
                };
            }

            return new
            {
                success = true,
                pawn = DescribePawn(pawn)
            };
        }

        [Tool("sheephappens/spawn_sheep", Description = "Spawn one sheep near a target cell, optionally already owned by the player.")]
        public static object SpawnSheep(
            [ToolParameter(Description = "Target cell x coordinate.")] int x,
            [ToolParameter(Description = "Target cell z coordinate.")] int z,
            [ToolParameter(Description = "When true, the sheep will be assigned to the player faction.", Required = false, DefaultValue = true)] bool playerOwned = true)
        {
            if (TryFindSpawnCell(x, z, out var map, out var cell, out var error) == false)
            {
                return new
                {
                    success = false,
                    error
                };
            }

            try
            {
                var pawn = PawnGenerator.GeneratePawn(Constants.sheepKindDef, playerOwned ? Faction.OfPlayer : null);
                _ = GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                if (playerOwned && pawn.Faction != Faction.OfPlayer)
                    pawn.SetFaction(Faction.OfPlayer);

                return new
                {
                    success = true,
                    pawn = DescribePawn(pawn)
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = ex.ToString()
                };
            }
        }

        [Tool("sheephappens/spawn_hostile_human", Description = "Spawn one hostile human pawn near a target cell so Sheep Happens bomb behavior can be exercised deterministically.")]
        public static object SpawnHostileHuman(
            [ToolParameter(Description = "Target cell x coordinate.")] int x,
            [ToolParameter(Description = "Target cell z coordinate.")] int z)
        {
            if (TryFindSpawnCell(x, z, out var map, out var cell, out var error) == false)
            {
                return new
                {
                    success = false,
                    error
                };
            }

            var faction = FindHostileHumanlikeFaction();
            if (faction == null)
            {
                return new
                {
                    success = false,
                    error = "No hostile humanlike faction was available on the current map."
                };
            }

            var kind = faction.RandomPawnKind();
            if (kind == null)
            {
                return new
                {
                    success = false,
                    error = $"Faction '{faction.Name}' could not provide a humanlike pawn kind."
                };
            }

            try
            {
                var pawn = PawnGenerator.GeneratePawn(kind, faction);
                _ = GenSpawn.Spawn(pawn, cell, map, WipeMode.Vanish);

                return new
                {
                    success = true,
                    pawn = DescribePawn(pawn)
                };
            }
            catch (Exception ex)
            {
                return new
                {
                    success = false,
                    error = ex.ToString()
                };
            }
        }

        [Tool("sheephappens/despawn_pawn", Description = "Destroy one spawned pawn by stable id.")]
        public static object DespawnPawn([ToolParameter(Description = "Stable pawn id such as Thing_Sheep1234 or Thing_Human862.")] string pawnId)
        {
            var pawn = FindPawn(pawnId);
            if (pawn == null)
            {
                return new
                {
                    success = false,
                    error = $"Pawn '{pawnId}' was not found on the current map."
                };
            }

            var snapshot = DescribePawn(pawn);
            pawn.Destroy(DestroyMode.Vanish);
            return new
            {
                success = true,
                pawn = snapshot
            };
        }

        [Tool("sheephappens/set_pawn_rotation", Description = "Force a spawned pawn to face a cardinal direction without moving. When the pawn is in Wait_Combat, the facing is held while drafted.")]
        public static object SetPawnRotation(
            [ToolParameter(Description = "Stable pawn id such as Thing_Human862 or Thing_Sheep1234.")] string pawnId,
            [ToolParameter(Description = "Desired cardinal direction: north, east, south, or west.")] string direction)
        {
            var pawn = FindPawn(pawnId);
            if (pawn == null)
            {
                return new
                {
                    success = false,
                    error = $"Pawn '{pawnId}' was not found on the current map."
                };
            }

            if (pawn.Spawned == false)
            {
                return new
                {
                    success = false,
                    error = $"Pawn '{pawnId}' is not currently spawned."
                };
            }

            if (TryParseRotation(direction, out var rotation) == false)
            {
                return new
                {
                    success = false,
                    error = $"Direction '{direction}' is invalid. Use north, east, south, or west."
                };
            }

            pawn.pather?.StopDead();
            if (pawn.jobs?.curJob != null)
                pawn.jobs.curJob.overrideFacing = rotation;

            pawn.rotationTracker?.FaceCell(pawn.Position + rotation.FacingCell);
            pawn.Rotation = rotation;

            return new
            {
                success = true,
                pawn = DescribePawn(pawn)
            };
        }
    }
}
