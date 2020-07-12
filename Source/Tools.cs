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

			KeyValuePair<BodyTypeDef, Graphic> Get(FieldInfo field)
			{
				var bodyTypeDef = field.GetValue(null) as BodyTypeDef;
				var graphic = GraphicDatabase.Get<Graphic_Multi>($"{path}_{field.Name}", shaderDef.Shader, size, Color.white);
				return new KeyValuePair<BodyTypeDef, Graphic>(bodyTypeDef, graphic);
			}

			return AccessTools.GetDeclaredFields(typeof(BodyTypeDefOf))
				.Select(Get)
				.ToDictionary(pair => pair.Key, pair => pair.Value);
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
			pawn.GetState().until = ticks == 0 ? 0 : GenTicks.TicksGame + ticks;
			pawn.Map.attackTargetsCache.UpdateTarget(pawn);
		}

		public static bool IsMaskWearer(Thing thing)
		{
			var pawn = thing as Pawn;
			if (pawn == null) return false;
			if (pawn.IsColonist == false) return false;
			return (pawn.GetState().until > 0);
		}
	}
}