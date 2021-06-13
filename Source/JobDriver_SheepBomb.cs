using RimWorld;
using System.Collections.Generic;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace SheepHappens
{
	class JobDriver_SheepBomb : JobDriver
	{
		Sustainer sustainer;

		public override bool TryMakePreToilReservations(bool errorOnFailed)
		{
			return true;
		}

		public static void Sleeplode(Pawn pawn, int ticks)
		{
			GenExplosion.DoExplosion(pawn.Position, pawn.Map, Constants.sheepBombExplosionRadius, Defs.ForcedSleep, pawn, ticks, damageFalloff: true);
			pawn.Destroy(DestroyMode.Vanish);
		}

		Toil BombToil()
		{
			var toil = new Toil();
			toil.initAction = () => toil.actor.pather.StartPath(TargetLocA, PathEndMode.OnCell);
			toil.tickAction = () =>
			{
				if (sustainer == null)
				{
					var info = SoundInfo.InMap(pawn, MaintenanceType.PerTick);
					sustainer = SoundDefOf.HissSmall.TrySpawnSustainer(info);
				}
				sustainer.Maintain();
			};
			toil.AddFinishAction(() => sustainer?.End());
			toil.defaultCompleteMode = ToilCompleteMode.PatherArrival;
			return toil;
		}

		public override IEnumerable<Toil> MakeNewToils()
		{
			yield return BombToil();
			yield return Toils_General.Do(() => Sleeplode(pawn, DamageWorker_ForcedSleep.GetSleepTicks()));
		}
	}
}
