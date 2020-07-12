using Verse;

namespace SheepHappens
{
	class Constants
	{
		public const float sheepGatheringRadius = 8f;
		public const float sheepFollowRadius = 2f;

		public const int igniteSheepBombDuration = 240;
		public const int enemiesToConsiderForBomb = 10;
		public const float sheepBombFireAmount = 0.5f;
		public const float sheepBombExplosionRadius = 9.9f;
		public const float sheepBombStunAmount = 50f;

		public const int desiredSheepCount = 6;
		public const float wildSheepSpawnChance = 0.4f;

		public static readonly ThingDef sheepThingDef = ThingDef.Named("Sheep");
		public static readonly PawnKindDef sheepKindDef = PawnKindDef.Named("Sheep");
	}
}
