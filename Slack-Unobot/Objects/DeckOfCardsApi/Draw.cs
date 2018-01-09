using System.Collections.Generic;
using Newtonsoft.Json;

namespace SlackUnobot.Objects.DeckOfCardsApi
{
	public class Draw : BaseDocApi
	{
		[JsonProperty("cards")]
		public List<Card> Cards { get; set; }
	}
}