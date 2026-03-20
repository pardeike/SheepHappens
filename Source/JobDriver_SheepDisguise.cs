using RimWorld;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SheepHappens
{
	class JobDriver_SheepDisguise : JobDriver
	{
		int sacrificeTicks = -1;
		const int TotalDurationTicks = (int)(60 * 17.108f);
		static readonly int[] LightningStartTicks = new float[] { 10.594f, 11.246f, 11.675f }.Select(sec => (int)(sec * 60)).ToArray();

		protected Pawn Victim => (Pawn)job.targetA.Thing;

		public override void ExposeData()
		{
			base.ExposeData();
			Scribe_Values.Look(ref sacrificeTicks, "sacrifiesTicks", defaultValue: -1);
		}

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn.InMentalState)
				return false;
			return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
		}

		void LightningStrike(int i)
		{
			var tick = LightningStartTicks[i - 1];
			if (sacrificeTicks == tick)
			{
				var loc = Victim.Position.ToVector3Shifted();
				for (var j = 0; j < 3 * i; j++)
				{
					FleckMaker.ThrowSmoke(loc, Map, i * 2f);
					FleckMaker.ThrowMicroSparks(loc, Map);
					FleckMaker.ThrowLightningGlow(loc, Map, i * 2f);
				}
			}
			if (sacrificeTicks >= tick && sacrificeTicks < tick + 6)
			{
				var intensity = (tick + 6 - sacrificeTicks) / 6f;
				var boltMesh = LightningBoltMeshPool.RandomBoltMesh;
				UnityEngine.Graphics.DrawMesh(boltMesh, Victim.PositionHeld.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather), Quaternion.identity, FadedMaterialPool.FadedVersionOf(SheepGraphics.LightningMat, intensity), 0);
			}
		}

		void ForceNormalSpeedFor(int ticks)
		{
			var slower = Find.TickManager.slower;
			slower.forceNormalSpeedUntil = Mathf.Max(slower.forceNormalSpeedUntil, Find.TickManager.TicksGame + ticks);
		}

		void ActivateMask()
		{
			var duration = GenDate.TicksPerHour / 4 + (int)(GenDate.TicksPerHour * 4f * pawn.skills.GetSkill(SkillDefOf.Crafting).Level / 20);
			if (pawn.HasPsylink)
				duration = (int)(duration * 4f * pawn.GetPsylinkLevel() / pawn.GetMaxPsylinkLevel());
			Tools.SetMaskWearer(pawn, duration);
		}

		void RegisterSacrificeCondition()
		{
			var map = pawn.Map;
			var gameCondition = (GameCondition_Sacrifice)GameConditionMaker.MakeCondition(Defs.Sacrifice, TotalDurationTicks);
			gameCondition.suppressEndMessage = true;
			gameCondition.conditionCauser = pawn;
			map.gameConditionManager.RegisterCondition(gameCondition);
		}

		void CompleteSacrifice()
		{
			var sheep = Victim;
			ExecutionUtility.DoExecutionByCut(pawn, sheep);
			var rottable = sheep.Corpse.TryGetComp<CompRottable>();
			rottable.RotProgress = rottable.PropsRot.TicksToDessicated;
			ActivateMask();
			pawn.records.Increment(RecordDefOf.AnimalsSlaughtered);
		}

		Toil CreateSacrificeToil()
		{
			var toil = Toils_General.WaitWith(TargetIndex.A, TotalDurationTicks, false, false);
			toil.WithAlwaysVisibleProgressBarToilDelay(this, TargetIndex.A, TotalDurationTicks);
			toil.tickAction = () =>
			{
				sacrificeTicks++;
				if (sacrificeTicks == 0)
				{
					ForceNormalSpeedFor(TotalDurationTicks - 60);
					Defs.SacrificeSheep.PlayOneShotOnCamera(pawn.Map);
				}
				for (var i = 1; i <= 3; i++) LightningStrike(i);
				if (sacrificeTicks == LightningStartTicks[2])
					CompleteSacrifice();
			};
			return toil;
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			_ = this.FailOnAggroMentalState(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.Do(RegisterSacrificeCondition);
			yield return CreateSacrificeToil();
		}
	}
}
