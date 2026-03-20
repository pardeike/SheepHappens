using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SheepHappens
{
	class JobDriver_IgniteSheepBomb : JobDriver
	{
		protected Pawn Victim => (Pawn)job.targetA.Thing;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			if (pawn.InMentalState)
				return false;
			return pawn.Reserve(Victim, job, 1, -1, null, errorOnFailed);
		}

		IntVec3 FindBombTargetCell()
		{
			return Tools.BestEnemyPosition(Victim);
		}

		Toil CreateIgniteToil()
		{
			var toil = Toils_General.WaitWith(TargetIndex.A, Constants.igniteSheepBombDuration, false, false);
			toil.WithAlwaysVisibleProgressBarToilDelay(this, TargetIndex.A, Constants.igniteSheepBombDuration);
			return toil;
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			_ = this.FailOnAggroMentalState(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return CreateIgniteToil();
			yield return Toils_General.Do(delegate
			{
				var targetCell = FindBombTargetCell();
				if (targetCell.IsValid == false)
				{
					EndJobWith(JobCondition.Incompletable);
					return;
				}

				SoundStarter.PlayOneShot(Defs.SheepIgnite, SoundInfo.InMap(Victim));
				TargetThingA.TryAttachFire(Constants.sheepBombFireAmount, pawn);

				var job = JobMaker.MakeJob(Defs.SheepBomb, targetCell);
				job.checkOverrideOnExpire = true;
				job.collideWithPawns = false;
				job.expiryInterval = 0;
				job.playerForced = true;
				Victim.jobs.StartJob(job, JobCondition.InterruptForced);
			});
		}
	}
}
