using System.Collections.Generic;

namespace SlackUnobot.Objects
{
	public class Game
	{
		public bool Initialized { get; set; }
		public bool Started { get; set; }
		public Dictionary<string, Player> Players { get; set; }
		public string DeckId { get; set; }
		public Queue<string> TurnOrder { get; set; }
		public Card CurrentCard { get; set; }
		public string Id { get; set; }
		public string Player1 { get; set; }
		public bool PlayAnything { get; set; }
	}
}