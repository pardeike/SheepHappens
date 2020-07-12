using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SheepHappens
{
	// render sheep mask and costume
	//
	[HarmonyPatch(typeof(PawnRenderer))]
	[HarmonyPatch("RenderPawnInternal")]
	[HarmonyPatch(new Type[] { typeof(Vector3), typeof(float), typeof(bool), typeof(Rot4), typeof(Rot4), typeof(RotDrawMode), typeof(bool), typeof(bool), typeof(bool) })]
	static class PawnRenderer_RenderPawnInternal_Patch
	{
		[HarmonyPriority(Priority.Last)]
		public static void Postfix(PawnRenderer __instance, Vector3 rootLoc, float angle, bool renderBody, Rot4 bodyFacing, Rot4 headFacing, RotDrawMode bodyDrawType, bool portrait, bool headStump, bool invisible)
		{
			if (bodyDrawType == RotDrawMode.Dessicated) return;
			if (invisible) return;

			var pawn = __instance.graphics.pawn;
			if (pawn.IsColonist == false) return;
			if (pawn.GetState().until == 0) return;

			var quaternion = Quaternion.AngleAxis(angle, Vector3.up);

			// costume

			var graphic = Graphics.disguiseCostumes[pawn.story.bodyType];
			var mesh = graphic.MeshAt(bodyFacing);
			var mat = graphic.MatAt(bodyFacing);

			var loc = rootLoc;
			loc.y += 0.007575758f;

			var n = __instance.graphics.MatsBodyBaseAt(bodyFacing, bodyDrawType).Count;
			loc.y += 0.003787879f * n;

			if (renderBody && portrait == false && headStump == false)
				GenDraw.DrawMeshNowOrLater(mesh, loc, quaternion, mat, portrait);

			// head

			graphic = Graphics.disguiseMasks[pawn.story.bodyType];
			mesh = graphic.MeshAt(headFacing);
			mat = graphic.MatAt(headFacing);

			var a = rootLoc;
			if (bodyFacing != Rot4.North)
				a.y += 0.0265151523f;
			else
				a.y += 0.0227272734f;

			var b = quaternion * __instance.BaseHeadOffsetAt(headFacing);
			b.y += 0.002f;
			GenDraw.DrawMeshNowOrLater(mesh, a + b, quaternion, mat, portrait);
		}
	}

	// reset mask wearing after time expires
	//
	[HarmonyPatch(typeof(Pawn))]
	[HarmonyPatch(nameof(Pawn.TickRare))]
	static class Pawn_TickRare_Patch
	{
		public static void Postfix(Pawn __instance)
		{
			if (__instance.IsColonist == false) return;
			var state = __instance.GetState();
			if (GenTicks.TicksGame > state.until)
			{
				Tools.SetMaskWearer(__instance, 0);
				__instance.Map.mapPawns.AllPawnsSpawned
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
			var pawn = __instance as Pawn;
			if (pawn == null) return;
			if (pawn.Position == value) return;
			var map = pawn.Map;
			if (map == null) return;
			if (pawn.IsColonist == false) return;

			var state = pawn.GetState();
			if (state.until == 0) return;

			GenRadial.RadialDistinctThingsAround(pawn.Position, map, Constants.sheepGatheringRadius, true)
				.OfType<Pawn>()
				.Where(p => p.def == Constants.sheepThingDef && ourJobDefs.Contains(p.CurJobDef) == false)
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
	[HarmonyPatch]
	static class WorkGiver_FightFires_FireIsBeingHandled_Patch
	{
		public static MethodInfo TargetMethod()
		{
			return AccessTools.Method("RimWorld.WorkGiver_FightFires:FireIsBeingHandled");
		}

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
	[HarmonyPatch(typeof(JobDriver_ExtinguishSelf))]
	[HarmonyPatch("MakeNewToils")]
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

		// igniting sheep plays a sound
		//
		[HarmonyPatch(typeof(Fire))]
		[HarmonyPatch(nameof(Fire.AttachTo))]
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
		[HarmonyPatch(typeof(Pawn_HealthTracker))]
		[HarmonyPatch("MakeDowned")]
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
		[HarmonyPatch(typeof(Pawn_MindState))]
		[HarmonyPatch(nameof(Pawn_MindState.StartFleeingBecauseOfPawnAction))]
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
		[HarmonyPatch(typeof(Pawn))]
		[HarmonyPatch(nameof(Pawn.Kill))]
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
		[HarmonyPatch(typeof(Pawn))]
		[HarmonyPatch(nameof(Pawn.HearClamor))]
		static class Pawn_HearClamor_Patch
		{
			[HarmonyPriority(Priority.First)]
			public static bool Prefix(Pawn __instance, ClamorDef type)
			{
				var job = __instance.CurJob;
				if (job == null) return true;
				if (job.def != JobDefOf.LayDown) return true;
				if (__instance.Downed)
				{
					__instance.jobs.EndCurrentJob(JobCondition.Succeeded);
					return true;
				}
				if (type != ClamorDefOf.Harm && type != ClamorDefOf.Impact) return true;
				return (job.forceSleep == false);
			}
		}
		//
		[HarmonyPatch(typeof(Pawn_JobTracker))]
		[HarmonyPatch(nameof(Pawn_JobTracker.CheckForJobOverride))]
		static class Pawn_JobTracker_CheckForJobOverride_Patch
		{
			[HarmonyPriority(Priority.First)]
			public static bool Prefix(Pawn ___pawn)
			{
				var job = ___pawn.CurJob;
				if (job == null) return true;
				if (job.def != JobDefOf.LayDown) return true;
				if (___pawn.Downed)
				{
					___pawn.jobs.EndCurrentJob(JobCondition.Succeeded);
					return true;
				}
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
		[HarmonyPatch(typeof(StatExtension))]
		[HarmonyPatch(nameof(StatExtension.GetStatValue))]
		static class StatExtension_GetStatValue_Patch
		{
			[HarmonyPriority(Priority.Last)]
			public static void Postfix(Thing thing, StatDef stat, ref float __result)
			{
				if (stat != StatDefOf.TameAnimalChance) return;
				var pawn = thing as Pawn;
				if (pawn == null) return;
				if (pawn.IsColonist == false) return;
				__result *= Tools.IsMaskWearer(pawn) ? 4f : 2f;
			}
		}

		// increase chance of traders carrying sheep
		//
		[HarmonyPatch(typeof(StockGenerator_Animals))]
		[HarmonyPatch("SelectionChance")]
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
		[HarmonyPatch(typeof(StockGenerator_Animals))]
		[HarmonyPatch("PawnKindAllowed")]
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
		[HarmonyPatch(typeof(WildAnimalSpawner))]
		[HarmonyPatch(nameof(WildAnimalSpawner.SpawnRandomWildAnimalAt))]
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
}