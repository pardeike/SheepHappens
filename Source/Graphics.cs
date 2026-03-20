using UnityEngine;
using Verse;

namespace SheepHappens
{
	[StaticConstructorOnStartup]
	static class SheepGraphics
	{
		public static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt", -1);
	}
}
