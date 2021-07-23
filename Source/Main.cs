using HarmonyLib;
using System.IO;
using UnityEngine;
using Verse;

namespace SheepHappens
{
	[StaticConstructorOnStartup]
	public class Main : Mod
	{
		public static string rootDir;

		public Main(ModContentPack content) : base(content)
		{
			rootDir = content.RootDir + Path.DirectorySeparatorChar + "1.3";

			var harmony = new Harmony("net.pardeike.sheephappens");
			harmony.PatchAll();
		}

		public override void DoSettingsWindowContents(Rect inRect)
		{
			base.DoSettingsWindowContents(inRect);
		}

		public override string SettingsCategory()
		{
			return base.SettingsCategory();
		}

		public override string ToString()
		{
			return base.ToString();
		}

		public override void WriteSettings()
		{
			base.WriteSettings();
		}
	}
}
