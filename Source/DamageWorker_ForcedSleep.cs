using RimWorld;
using Verse;
using Verse.AI;

namespace SheepHappens
{
	class DamageWorker_ForcedSleep : DamageWorker
	{
		public static int GetSleepTicks()
		{
			var hours = GenMath.LerpDoubleClamped(0, 5, 4f, 0.2f, Find.Storyteller.difficulty.difficulty);
			return (int)(GenDate.TicksPerHour * hours);
		}

		void PutToSleep(Pawn pawn, float amount)
		{
			var compCanBeDormant = pawn.TryGetComp<CompCanBeDormant>();
			if (compCanBeDormant != null)
			{
				compCanBeDormant.ToSleep();
				return;
			}
			var job = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
			job.expiryInterval = (int)amount;
			job.forceSleep = true;
			pawn.jobs.StartJob(job, JobCondition.Incompletable);
		}

		public override DamageResult Apply(DamageInfo dinfo, Thing victim)
		{
			if (victim is Pawn pawn) PutToSleep(pawn, dinfo.Amount);
			return base.Apply(dinfo, victim);
		}
	}
}