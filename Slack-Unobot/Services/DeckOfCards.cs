using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SlackUnobot.Objects.DeckOfCardsApi;

namespace SlackUnobot.Services
{
	public class DeckOfCards
	{
		private const string DECK_OF_CARDS_API = "http://deckofcardsapi.com/api";

		public async Task<Draw> DrawCards(string deckId, int count)
		{
			using (var client = new HttpClient())
			{
				var req = await client.GetStringAsync($"{DECK_OF_CARDS_API}/deck/{deckId}/draw/?count={count}");
				var res = JsonConvert.DeserializeObject<Draw>(req);

				return res;
			}
		}

		public async Task<Shuffle> ShuffleDeck(string deckId)
		{
			using (var client = new HttpClient())
			{
				var req = await client.GetStringAsync($"{DECK_OF_CARDS_API}/deck/{deckId}/shuffle");
				var res = JsonConvert.DeserializeObject<Shuffle>(req);

				return res;
			}
		}

		public async Task<string> NewDeck()
		{
			using (var client = new HttpClient())
			{
				var req = await client.GetStringAsync($"{DECK_OF_CARDS_API}/deck/new/shuffle/?deck_count=2");
				var res = JsonConvert.DeserializeObject<Shuffle>(req);

				return res.DeckId;
			}
		}
	}
}