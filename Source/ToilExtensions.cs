using Verse.AI;

namespace SheepHappens
{
	static class ToilExtensions
	{
		public static Toil WithAlwaysVisibleProgressBarToilDelay(this Toil toil, JobDriver driver, TargetIndex targetIndex, int toilDuration, bool interpolateBetweenActorAndTarget = false, float offsetZ = -0.5f)
		{
			return toil.WithProgressBar(targetIndex, () => 1f - (float)driver.ticksLeftThisToil / toilDuration, interpolateBetweenActorAndTarget, offsetZ, true);
		}
	}
}
