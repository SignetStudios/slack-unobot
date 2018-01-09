using System.Collections.Generic;

namespace SlackUnobot.Objects
{
	public class Player
	{
		public string Name { get; set; }
		public string Id { get; set; }
		public List<Card> Hand { get; set; }
		public int Score { get; set; }
		public bool IsAi { get; set; }
		public string AiType { get; set; }
	}
}