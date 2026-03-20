using RimWorld;
using System.Linq;
using Verse;

namespace SheepHappens
{
	public static class Tools
	{
		public static void SetMaskWearer(Pawn pawn, int ticks)
		{
			if (pawn == null) return;
			pawn.GetState().until = ticks == 0 ? 0 : GenTicks.TicksGame + ticks;
			RefreshDisguiseApparel(pawn);
			pawn.Map?.attackTargetsCache?.UpdateTarget(pawn);
			pawn.Drawer?.renderer?.SetAllGraphicsDirty();
			_ = GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
		}

		public static bool IsSheep(Thing thing)
		{
			return thing?.def == Constants.sheepThingDef;
		}

		public static bool IsPlayerOwnedSheep(Pawn pawn)
		{
			return IsSheep(pawn) && pawn.Faction == Faction.OfPlayer;
		}

		public static bool IsMaskWearer(Thing thing)
		{
			if (!(thing is Pawn pawn)) return false;
			if (pawn.IsColonist == false) return false;
			return pawn.GetState().until > GenTicks.TicksGame;
		}

		static bool IsDisguiseApparel(Apparel apparel)
		{
			return apparel != null && (apparel.def == Defs.DisguiseMask || apparel.def == Defs.DisguiseCostume);
		}

		static Apparel EnsureDisguiseApparel(Pawn pawn, ThingDef def)
		{
			var apparel = pawn.apparel.WornApparel.FirstOrDefault(worn => worn.def == def && worn.Destroyed == false);
			if (apparel != null)
				return apparel;

			apparel = (Apparel)ThingMaker.MakeThing(def);
			pawn.apparel.Wear(apparel, false, locked: true);
			return apparel;
		}

		public static void RefreshDisguiseApparel(Pawn pawn)
		{
			if (pawn?.apparel == null) return;

			if (IsMaskWearer(pawn))
			{
				_ = EnsureDisguiseApparel(pawn, Defs.DisguiseCostume);
				_ = EnsureDisguiseApparel(pawn, Defs.DisguiseMask);
				return;
			}

			var disguiseApparel = pawn.apparel.WornApparel.Where(IsDisguiseApparel).ToList();
			foreach (var apparel in disguiseApparel)
			{
				pawn.apparel.Remove(apparel);
				apparel.Destroy(DestroyMode.Vanish);
			}
		}

		public static IntVec3 BestEnemyPosition(Pawn sheep)
		{
			var enemies = sheep.Map.attackTargetsCache.TargetsHostileToFaction(Faction.OfPlayer).OfType<Pawn>();
			var from = sheep.Position;
			var pos = IntVec2.Zero;
			var count = 0;
			foreach (var enemy in enemies.OrderBy(pawn => pawn.Position.DistanceToSquared(from)))
			{
				var c = enemy.Position;
				pos += new IntVec2(c.x, c.z);
				count++;
				if (count == Constants.enemiesToConsiderForBomb) break;
			}
			if (count == 0) return IntVec3.Invalid;
			pos /= count;
			var cell = pos.ToIntVec3;
			return RCellFinder.BestOrderedGotoDestNear(cell, sheep);
		}
	}
}
