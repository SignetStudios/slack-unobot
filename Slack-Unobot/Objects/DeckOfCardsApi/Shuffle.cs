using Newtonsoft.Json;

namespace SlackUnobot.Objects.DeckOfCardsApi
{
	public class Shuffle : BaseDocApi
	{
		[JsonProperty("shuffled")]
		public bool Shuffled { get; set; }
	}
}