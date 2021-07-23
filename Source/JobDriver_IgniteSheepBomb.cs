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

		IntVec3 TargetCell()
		{
			return Tools.BestEnemyPosition(Victim);
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			_ = this.FailOnAggroMentalState(TargetIndex.A);
			yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
			yield return Toils_General.WaitWith(TargetIndex.A, Constants.igniteSheepBombDuration, true, false);
			yield return Toils_General.Do(delegate
			{
				SoundStarter.PlayOneShot(Defs.SheepIgnite, SoundInfo.InMap(Victim));
				TargetThingA.TryAttachFire(Constants.sheepBombFireAmount);

				var job = JobMaker.MakeJob(Defs.SheepBomb, TargetCell());
				job.checkOverrideOnExpire = true;
				job.collideWithPawns = false;
				job.expiryInterval = 0;
				job.playerForced = true;
				Victim.jobs.StartJob(job, JobCondition.InterruptForced);
			});
		}
	}
}
