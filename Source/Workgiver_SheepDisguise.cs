using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace SheepHappens
{
	public class Workgiver_SheepDisguise : WorkGiver_PlayerOwnedSheepInteraction
	{
		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			return PlayerOwnedSheepOnMap(pawn);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			return GetInteractablePlayerOwnedSheep(pawn, thing, forced) != null;
		}

		public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			var sheep = GetInteractablePlayerOwnedSheep(pawn, thing, forced);
			return sheep == null ? null : JobMaker.MakeJob(Defs.CreateSheepDisguise, sheep);
		}
	}
}
