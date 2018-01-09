using Newtonsoft.Json;

namespace SlackUnobot.Objects.DeckOfCardsApi
{
	public class BaseDocApi
	{
		[JsonProperty("success")]
		public bool Success { get; set; }
		[JsonProperty("deck_id")]
		public string DeckId { get; set; }
		[JsonProperty("remaining")]
		public int Remaining { get; set; }
	}
}