using System.Collections.Generic;
using Verse;

namespace SheepHappens
{
	public class GameState : GameComponent
	{
		internal Dictionary<Pawn, State> pawns;
		readonly Scribe_Dictionary<Pawn, State> pawnsHelper;

		public GameState(Game game) : base()
		{
			_ = game;
			pawns = new Dictionary<Pawn, State>();
			pawnsHelper = new Scribe_Dictionary<Pawn, State>();
		}

		public override void ExposeData()
		{
			base.ExposeData();
			pawnsHelper.Scribe(ref pawns, "pawns");
		}
	}

	public static class GameStateExtension
	{
		public static State GetState(this Pawn pawn)
		{
			var gameState = Current.Game.GetComponent<GameState>();
			if (gameState.pawns.TryGetValue(pawn, out var state) == false)
			{
				state = new State();
				gameState.pawns[pawn] = state;
			}
			return state;
		}
	}
}
