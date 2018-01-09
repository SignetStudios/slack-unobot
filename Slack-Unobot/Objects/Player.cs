using System.Collections.Generic;

namespace SlackUnobot.Objects
{
	public class Player
	{
		public List<Card> Hand { get; set; }
		public int Score { get; set; }
		public bool IsAi { get; set; }
		public string AiName { get; set; }
	}
}