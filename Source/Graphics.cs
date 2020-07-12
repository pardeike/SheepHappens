using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace SheepHappens
{
	[StaticConstructorOnStartup]
	static class Graphics
	{
		public static readonly Dictionary<BodyTypeDef, Graphic> disguiseMasks = Tools.GetBodyGraphics("DisguiseMask/DisguiseMask", ShaderTypeDefOf.Cutout);
		public static readonly Dictionary<BodyTypeDef, Graphic> disguiseCostumes = Tools.GetBodyGraphics("DisguiseCostume/DisguiseCostume", ShaderTypeDefOf.Cutout);

		public static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt", -1);
	}
}