using RimWorld;
using UnityEngine;
using Verse;

namespace SheepHappens
{
	public class GameCondition_Sacrifice : GameCondition
	{
		static readonly SkyColorSet EvilDarkness = new SkyColorSet(new Color(0.3f, 0.3f, 0.3f), new Color(0.5f, 0f, 0f), new Color(0.75f, 0.1f, 0.1f), 1f);
		static readonly SkyColorSet EvilDarknessFlicker = new SkyColorSet(new Color(0.2f, 0.2f, 0.2f), new Color(1f, 0f, 0f), new Color(0.65f, 0f, 0f), 1f);
		public static readonly int RampUpDownDuration = 5 * 60;

		public override int TransitionTicks => RampUpDownDuration;

		public override float SkyTargetLerpFactor(Map map)
		{
			return GameConditionUtility.LerpInOutValue(this, TransitionTicks, 1f);
		}

		public override SkyTarget? SkyTarget(Map map)
		{
			return new SkyTarget?(new SkyTarget(0f, Rand.Chance(0.05f) ? EvilDarknessFlicker : EvilDarkness, 1f, 0f));
		}
	}
}