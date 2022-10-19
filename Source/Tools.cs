using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace SheepHappens
{
	public static class Tools
	{
		public static Dictionary<BodyTypeDef, Graphic> GetBodyGraphics(string path, ShaderTypeDef shaderDef, Vector2 size = default)
		{
			if (size.x <= 0 || size.y <= 0)
				size = new Vector2(1.5f, 1.5f);

			KeyValuePair<BodyTypeDef, Graphic>? Get(FieldInfo field)
			{
				if (!(field.GetValue(null) is BodyTypeDef bodyTypeDef))
					return null;
				var graphic = GraphicDatabase.Get<Graphic_Multi>($"{path}_{field.Name}", shaderDef.Shader, size, Color.white);
				return new KeyValuePair<BodyTypeDef, Graphic>(bodyTypeDef, graphic);
			}

			return AccessTools.GetDeclaredFields(typeof(BodyTypeDefOf))
				.Select(Get)
				.Where(pair => pair.HasValue)
				.ToDictionary(pair => pair.Value.Key, pair => pair.Value.Value);
		}

		public static int FindIndexBefore<T>(this List<T> list, int index, Predicate<T> match)
		{
			for (var i = index; i >= 0; i--)
				if (match(list[i]))
					return i;
			return -1;
		}

		public static int FindIndexAfter<T>(this List<T> list, int index, Predicate<T> match)
		{
			for (var i = index; i < list.Count; i++)
				if (match(list[i]))
					return i;
			return -1;
		}

		public static IEnumerable<CodeInstruction> Error(this IEnumerable<CodeInstruction> instructions, string message)
		{
			Log.Error(message);
			return instructions;
		}

		public static bool EveryNTick(this Thing thing, int ticks)
		{
			return (Find.TickManager.TicksGame + thing.thingIDNumber.HashOffset()) % ticks == 0;
		}

		public static void SetMaskWearer(Pawn pawn, int ticks)
		{
			if (pawn == null) return;
			pawn.GetState().until = ticks == 0 ? 0 : GenTicks.TicksGame + ticks;
			pawn.Map?.attackTargetsCache?.UpdateTarget(pawn);
			_ = GlobalTextureAtlasManager.TryMarkPawnFrameSetDirty(pawn);
		}

		public static bool IsMaskWearer(Thing thing)
		{
			if (!(thing is Pawn pawn)) return false;
			if (pawn.IsColonist == false) return false;
			return (pawn.GetState().until > 0);
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
