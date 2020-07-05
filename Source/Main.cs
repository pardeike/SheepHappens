using HarmonyLib;
using UnityEngine;
using Verse;

namespace SheepHappens
{
	public class Main : Mod
	{
		public Main(ModContentPack content) : base(content)
		{
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