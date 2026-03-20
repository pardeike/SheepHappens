using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace SheepHappens
{
	public abstract class WorkGiver_PlayerOwnedSheepInteraction : WorkGiver_InteractAnimal
	{
		protected static IEnumerable<Thing> PlayerOwnedSheepOnMap(Pawn pawn)
		{
			return pawn.Map.mapPawns.AllPawnsSpawned.Where(Tools.IsPlayerOwnedSheep).Cast<Thing>();
		}

		protected Pawn GetInteractablePlayerOwnedSheep(Pawn pawn, Thing thing, bool forced)
		{
			if (!(thing is Pawn sheep) || !Tools.IsPlayerOwnedSheep(sheep))
				return null;

			return CanInteractWithAnimal(pawn, sheep, forced) ? sheep : null;
		}
	}
}
