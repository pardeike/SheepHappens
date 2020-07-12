using HarmonyLib;
using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using static HarmonyLib.AccessTools;

namespace SheepHappens
{
	class JobDriver_SheepDisguise : JobDriver
	{
		public int sacrifiesTicks = -1;
		const int totalDuration = (int)(60 * 17.108f);
		static readonly int[] lightningStart = new float[] { 10.594f, 11.246f, 11.675f }.Select(sec => (int)(sec * 60)).ToArray();
		static readonly FieldRef<TimeSlower, int> forceNormalSpeedUntilRef = AccessTools.FieldRefAccess<TimeSlower, int>("forceNormalSpeedUntil");

		protected Pawn Victim => (Pawn)job.targetA.Thing;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref sacrifiesTicks, "sacrifiesTicks", defaultValue: -1);
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn.InMentalState)
				return false;
			return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
		}

		void LightningStrike(int i)
		{
			var tick = lightningStart[i - 1];
			if (sacrifiesTicks == tick)
			{
				var loc = Victim.Position.ToVector3Shifted();
				for (var j = 0; j < 3 * i; j++)
				{
					MoteMaker.ThrowSmoke(loc, Map, i * 2f);
					MoteMaker.ThrowMicroSparks(loc, Map);
					MoteMaker.ThrowLightningGlow(loc, Map, i * 2f);
				}
			}
			if (sacrifiesTicks >= tick && sacrifiesTicks < tick + 6)
			{
				var intensity = (tick + 6 - sacrifiesTicks) / 6f;
				var boltMesh = LightningBoltMeshPool.RandomBoltMesh;
				UnityEngine.Graphics.DrawMesh(boltMesh, Victim.PositionHeld.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather), Quaternion.identity, FadedMaterialPool.FadedVersionOf(Graphics.LightningMat, intensity), 0);
			}
		}

		void ForceNormalSpeedUntil(int delta)
		{
			var slower = Find.TickManager.slower;
			forceNormalSpeedUntilRef(slower) = Mathf.Max(forceNormalSpeedUntilRef(slower), Find.TickManager.TicksGame + delta);
		}

		void ActivateMask()
		{
			var duration = GenDate.TicksPerHour / 4 + (int)(GenDate.TicksPerHour * 4f * pawn.skills.GetSkill(SkillDefOf.Crafting).Level / 20);
			if (pawn.HasPsylink)
				duration = (int)(duration * 4f * pawn.GetPsylinkLevel() / pawn.GetMaxPsylinkLevel());
			Tools.SetMaskWearer(pawn, duration);
		}

		protected override IEnumerable<Toil> MakeNewToils()
		{
			_ = this.FailOnAggroMentalState(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Do(delegate
			{
				var map = pawn.Map;
				var gameCondition = (GameCondition_Sacrifice)GameConditionMaker.MakeCondition(Defs.Sacrifice, totalDuration);
				gameCondition.suppressEndMessage = true;
				gameCondition.conditionCauser = pawn;
				map.gameConditionManager.RegisterCondition(gameCondition);
			});
			var sacrifies = Toils_General.WaitWith(TargetIndex.A, totalDuration, true, false);
			sacrifies.tickAction = () =>
			{
				sacrifiesTicks++;
				if (sacrifiesTicks == 0)
				{
					ForceNormalSpeedUntil(totalDuration - 60);
					Defs.SacrificeSheep.PlayOneShotOnCamera(pawn.Map);
				}
				for (var i = 1; i <= 3; i++) LightningStrike(i);
				if (sacrifiesTicks == lightningStart[2])
				{
					var sheep = Victim;
					ExecutionUtility.DoExecutionByCut(pawn, sheep);
					var rottable = sheep.Corpse.TryGetComp<CompRottable>();
					rottable.RotProgress = rottable.PropsRot.TicksToDessicated;
					ActivateMask();
					pawn.records.Increment(RecordDefOf.AnimalsSlaughtered);
				}
			};
			yield return sacrifies;
		}
	}
}