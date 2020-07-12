using Verse;

namespace SheepHappens
{
	public class State : IExposable
	{
		public int until = 0;

		public void ExposeData()
		{
			Scribe_Values.Look(ref until, "until", 0);
		}
	}
}