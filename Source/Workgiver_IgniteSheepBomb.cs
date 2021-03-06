﻿using RimWorld;
using System.Collections.Generic;
using System.Linq;
using Verse;
using Verse.AI;

namespace SheepHappens
{
	public class Workgiver_IgniteSheepBomb : WorkGiver_InteractAnimal
	{
		public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
		{
			return pawn.Map.mapPawns.AllPawnsSpawned.Where(p => p.Faction == Faction.OfPlayer && p.def == Constants.sheepThingDef);
		}

		public override bool HasJobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			if (!(thing is Pawn pawn2) || pawn2.Faction != Faction.OfPlayer)
				return false;
			if (CanInteractWithAnimal(pawn, pawn2, forced) == false)
				return false;
			return true;
		}

		public override Job JobOnThing(Pawn pawn, Thing thing, bool forced = false)
		{
			if (!(thing is Pawn pawn2) || pawn2.Faction != Faction.OfPlayer)
				return null;
			if (CanInteractWithAnimal(pawn, pawn2, forced) == false)
				return null;
			return JobMaker.MakeJob(Defs.IgniteSheepBomb, thing);
		}
	}
}
