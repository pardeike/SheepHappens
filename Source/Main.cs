using HarmonyLib;
using Verse;

namespace SheepHappens
{
	public sealed class Main : Mod
	{
		const string HarmonyId = "net.pardeike.sheephappens";

		public Main(ModContentPack content) : base(content)
		{
			new Harmony(HarmonyId).PatchAll();
		}
	}
}
