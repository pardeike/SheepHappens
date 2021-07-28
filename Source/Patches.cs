using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SheepHappens
{
	// render sheep mask and costume
	//
	[HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnInternal))]
	static class PawnRenderer_RenderPawnInternal_Patch
	{
		static ApparelGraphicRecord? sheepMask = null;
		static readonly Dictionary<Pawn, ApparelGraphicRecord> sheepCostumes = new Dictionary<Pawn, ApparelGraphicRecord>();

		[HarmonyPriority(Priority.First)]
		public static void Prefix(PawnRenderer __instance, ref bool __state)
		{
			var pawn = __instance.graphics.pawn;
			if (pawn.IsColonist == false) return;

			if (pawn.GetState().until == 0) return;

			if (sheepCostumes.TryGetValue(pawn, out var costume) == false)
			{
				costume = GetApparelRecord(pawn, "costume");
				sheepCostumes[pawn] = costume;
			}
			if (sheepMask.HasValue == false)
				sheepMask = GetApparelRecord(null, "mask");

			var apparelGraphics = __instance.graphics.apparelGraphics;
			apparelGraphics.Insert(0, sheepMask.Value);
			apparelGraphics.Insert(0, costume);
			__state = true;
		}

		public static void Postfix(PawnRenderer __instance, bool __state)
		{
			if (__state == false) return;
			if (sheepMask.HasValue)
				_ = __instance.graphics.apparelGraphics.Remove(sheepMask.Value);
			var pawn = __instance.graphics.pawn;
			if (sheepCostumes.ContainsKey(pawn))
				_ = __instance.graphics.apparelGraphics.Remove(sheepCostumes[pawn]);
		}

		static ApparelGraphicRecord GetApparelRecord(Pawn pawn, string name)
		{
			var path = @$"{Main.rootDir}{Path.DirectorySeparatorChar}Private{Path.DirectorySeparatorChar}{name}.xml";
			var xml = File.ReadAllText(path);
			var def = DirectXmlLoader.ItemFromXmlString<ThingDef>(xml, path);
			var apparel = ThingMaker.MakeThing(def) as Apparel;
			_ = ApparelGraphicRecordGetter.TryGetGraphicApparel(apparel, pawn?.story.bodyType ?? BodyTypeDefOf.Male, out var item);
			return item;
		}
	}

	// reset mask wearing after time expires
	//
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.TickRare))]
	static class Pawn_TickRare_Patch
	{
		public static void Postfix(Pawn __instance)
		{
			if (__instance == null || __instance.IsColonist == false) return;
			var state = __instance.GetState();
			if (GenTicks.TicksGame > state.until)
			{
				Tools.SetMaskWearer(__instance, 0);
				__instance.Map?.mapPawns.AllPawnsSpawned
					.Where(p => p.def == Constants.sheepThingDef && p.CurJobDef == JobDefOf.FollowClose)
					.Where(sheep => sheep.CurJob.targetA == __instance)
					.Do(sheep => sheep.jobs.EndCurrentJob(JobCondition.Succeeded));
			}
		}
	}

	// sheep shall follow a sheep mask wearing colonist
	//
	[HarmonyPatch(typeof(Thing))]
	[HarmonyPatch(nameof(Thing.Position), MethodType.Setter)]
	static class Thing_Position_Setter_Patch
	{
		static readonly HashSet<JobDef> ourJobDefs = new HashSet<JobDef>(new[] { JobDefOf.FollowClose, Defs.SheepBomb });

		[HarmonyPriority(Priority.First)]
		public static void Prefix(Thing __instance, IntVec3 value)
		{
			if (!(__instance is Pawn pawn)) return;
			if (pawn.Position == value) return;
			var map = pawn.Map;
			if (map == null) return;
			if (pawn.IsColonist == false) return;

			var state = pawn.GetState();
			if (state.until == 0) return;

			var sheepAboutToGetSlaughtered = map.mapPawns.FreeColonists
				.Select(pawn => pawn.CurJob)
				.OfType<Job>()
				.Select(job => job.targetA.Thing)
				.OfType<Pawn>()
				.ToHashSet();

			GenRadial.RadialDistinctThingsAround(pawn.Position, map, Constants.sheepGatheringRadius, true)
				.OfType<Pawn>()
				.Where(p => p.def == Constants.sheepThingDef && ourJobDefs.Contains(p.CurJobDef) == false)
				.Where(sheep => sheepAboutToGetSlaughtered.Contains(sheep) == false)
				.Do(sheep =>
				{
					var job = JobMaker.MakeJob(JobDefOf.FollowClose, pawn);
					job.expiryInterval = 0;
					job.checkOverrideOnExpire = true;
					job.playerForced = true;
					job.collideWithPawns = true;
					job.followRadius = Constants.sheepFollowRadius;
					sheep.jobs.StartJob(job, JobCondition.InterruptForced, null);
				});
		}
	}

	// nobody should try to extinguish a burning sheep
	//
	[HarmonyPatch(typeof(WorkGiver_FightFires), nameof(WorkGiver_FightFires.FireIsBeingHandled))]
	static class WorkGiver_FightFires_FireIsBeingHandled_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Fire f, ref bool __result)
		{
			var parent = f.parent;
			if (parent == null) return true;
			if (parent.def != Constants.sheepThingDef) return true;

			__result = true;
			return false;
		}
	}

	// burning sheep shall not extinguish themselves
	//
	[HarmonyPatch(typeof(JobDriver_ExtinguishSelf), nameof(JobDriver_ExtinguishSelf.MakeNewToils))]
	static class JobDriver_ExtinguishSelf_MakeNewToils_Patch
	{
		public static IEnumerable<Toil> Postfix(IEnumerable<Toil> toils, JobDriver_ExtinguishSelf __instance)
		{
			var pawn = __instance.pawn;
			if (pawn.def != Constants.sheepThingDef)
			{
				foreach (var toil in toils) yield return toil;
				yield break;
			}
			yield return new Toil();
		}
	}

	// igniting sheep plays a sound
	//
	[HarmonyPatch(typeof(Fire), nameof(Fire.AttachTo))]
	static class Fire_AttachTo_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(Thing parent)
		{
			if (parent == null) return;
			if (parent.def != Constants.sheepThingDef) return;
			if (parent.IsBurning()) return;
			SoundStarter.PlayOneShot(Defs.SheepIgnite, SoundInfo.InMap(parent));
		}
	}

	// burning sheep explode when they get downed
	//
	[HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.MakeDowned))]
	static class Pawn_HealthTracker_MakeDowned_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(Pawn ___pawn)
		{
			if (___pawn == null) return;
			if (___pawn.def != Constants.sheepThingDef) return;
			if (___pawn.IsBurning() == false) return;
			JobDriver_SheepBomb.Sleeplode(___pawn, DamageWorker_ForcedSleep.GetSleepTicks());
		}
	}

	// burning sheep don't flee when they take damage
	//
	[HarmonyPatch(typeof(Pawn_MindState), nameof(Pawn_MindState.StartFleeingBecauseOfPawnAction))]
	static class Pawn_MindState_StartFleeingBecauseOfPawnAction_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Pawn ___pawn)
		{
			if (___pawn == null) return true;
			if (___pawn.def != Constants.sheepThingDef) return true;
			return (___pawn.IsBurning() == false);
		}
	}

	// burning sheep explode on death
	//
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
	static class Pawn_Kill_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Pawn __instance)
		{
			if (__instance.def != Constants.sheepThingDef) return true;
			if (__instance.IsBurning() == false) return true;
			JobDriver_SheepBomb.Sleeplode(__instance, DamageWorker_ForcedSleep.GetSleepTicks());
			return false;
		}
	}

	// sleeping enemies won't wake up until the job expires
	//
	[HarmonyPatch(typeof(Pawn), nameof(Pawn.HearClamor))]
	static class Pawn_HearClamor_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Pawn __instance, ClamorDef type)
		{
			if (__instance.HostileTo(Faction.OfPlayer) == false) return true;
			var job = __instance.CurJob;
			if (job == null) return true;
			if (job.def != JobDefOf.LayDown) return true;
			// if (__instance.Downed)
			// {
			// 	__instance.jobs.EndCurrentJob(JobCondition.Succeeded);
			// 	return true;
			// }
			if (type != ClamorDefOf.Harm && type != ClamorDefOf.Impact) return true;
			return (job.forceSleep == false);
		}
	}
	//
	[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.CheckForJobOverride))]
	static class Pawn_JobTracker_CheckForJobOverride_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Pawn ___pawn)
		{
			if (___pawn.HostileTo(Faction.OfPlayer) == false) return true;
			var job = ___pawn.CurJob;
			if (job == null) return true;
			if (job.def != JobDefOf.LayDown) return true;
			// if (___pawn.Downed)
			// {
			// 	___pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
			// 	return true;
			// }
			return (job.forceSleep == false);
		}
	}

	// wearing a mask makes you non-hostile
	//
	[HarmonyPatch(typeof(GenHostility))]
	[HarmonyPatch(nameof(GenHostility.HostileTo))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(Thing) })]
	static class GenHostility_HostileTo_Thing_Thing_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Thing a, Thing b, ref bool __result)
		{
			if (Tools.IsMaskWearer(a) == false && Tools.IsMaskWearer(b) == false) return true;
			__result = false;
			return false;
		}
	}
	//
	[HarmonyPatch(typeof(GenHostility))]
	[HarmonyPatch(nameof(GenHostility.HostileTo))]
	[HarmonyPatch(new[] { typeof(Thing), typeof(Faction) })]
	static class GenHostility_HostileTo_Thing_Faction_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(Thing t, ref bool __result)
		{
			if (Tools.IsMaskWearer(t) == false) return true;
			__result = false;
			return false;
		}
	}

	// increase chance of taming sheep
	//
	[HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
	static class StatExtension_GetStatValue_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(Thing thing, StatDef stat, ref float __result)
		{
			if (stat != StatDefOf.TameAnimalChance) return;
			if (!(thing is Pawn pawn)) return;
			if (pawn.IsColonist == false) return;
			__result *= Tools.IsMaskWearer(pawn) ? 4f : 2f;
		}
	}

	// increase chance of traders carrying sheep
	//
	[HarmonyPatch(typeof(StockGenerator_Animals), nameof(StockGenerator_Animals.SelectionChance))]
	static class StockGenerator_Animals_SelectionChance_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(PawnKindDef k, ref float __result)
		{
			if (k == Constants.sheepKindDef)
				__result *= 2;
		}
	}

	// make sheep allowed on non extreme map tiles
	//
	[HarmonyPatch(typeof(StockGenerator_Animals), nameof(StockGenerator_Animals.PawnKindAllowed))]
	static class StockGenerator_Animals_PawnKindAllowed_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(PawnKindDef kind, int forTile, ref bool __result)
		{
			if (kind != Constants.sheepKindDef) return true;
			var map = Current.Game.FindMap(forTile);
			if (map == null) return true;
			if (map.Biome.isExtremeBiome) return true;
			__result = true;
			return false;
		}
	}

	// increase chance of sheep spawning wildly (in vanilla they don't)
	//
	[HarmonyPatch(typeof(WildAnimalSpawner), nameof(WildAnimalSpawner.SpawnRandomWildAnimalAt))]
	static class WildAnimalSpawner_SpawnRandomWildAnimalAt_Patch
	{
		[HarmonyPriority(Priority.First)]
		public static bool Prefix(IntVec3 loc, Map ___map, ref bool __result)
		{
			if (___map == null || Rand.Chance(Constants.wildSheepSpawnChance) == false) return true;
			if (___map.Biome.isExtremeBiome) return true;
			if (___map.mapPawns.AllPawnsSpawned.Count(pawn => pawn.def == Constants.sheepThingDef) >= Constants.desiredSheepCount) return true;
			var loc2 = CellFinder.RandomClosewalkCellNear(loc, ___map, 6, null);
			if (GenSpawn.Spawn(PawnGenerator.GeneratePawn(Constants.sheepKindDef, null), loc2, ___map, WipeMode.Vanish) == null) return true;
			__result = true;
			return false;
		}
	}
}
