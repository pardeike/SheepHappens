using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace SheepHappens
{
	public class Workgiver_IgniteSheepBomb : WorkGiver_PlayerOwnedSheepInteraction
	{
		static bool CanBombTarget(Pawn sheep)
		{
			return sheep != null && Tools.BestEnemyPosition(sheep).IsValid;
		}

		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			return PlayerOwnedSheepOnMap(pawn);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			var sheep = GetInteractablePlayerOwnedSheep(pawn, thing, forced);
			return sheep != null && CanBombTarget(sheep);
		}

		public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			var sheep = GetInteractablePlayerOwnedSheep(pawn, thing, forced);
			if (sheep == null || CanBombTarget(sheep) == false)
				return null;
			return JobMaker.MakeJob(Defs.IgniteSheepBomb, sheep);
		}
	}
}
