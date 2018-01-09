using Newtonsoft.Json;
using SlackUnobot.Objects.Slack;
using System.Net.Http;
using System.Threading.Tasks;

namespace SlackUnobot.Services
{
	public interface ISlackClient
	{
		SlackResponse PostMessage(SlackMessage message);
		SlackResponse PostEphemeral(SlackMessage message);
		Task<SlackResponse> PostMessageAsync(SlackMessage message);
		Task<SlackResponse> PostEphemeralAsync(SlackMessage message);
	}

	public class SlackClient : ISlackClient
	{
		private readonly string _webhookUrl;

		public SlackClient(string webhookUrl)
		{
			_webhookUrl = webhookUrl;
		}


		public SlackResponse PostMessage(SlackMessage message)
		{
			return PostMessageAsync(message).Result;
		}

		public SlackResponse PostEphemeral(SlackMessage message)
		{
			return PostEphemeralAsync(message).Result;
		}

		public async Task<SlackResponse> PostMessageAsync(SlackMessage message)
		{
			using (var client = new HttpClient())
			{
				var request = await client.PostAsync($"{_webhookUrl}/chat.postMessage", new StringContent(JsonConvert.SerializeObject(message)));
				return JsonConvert.DeserializeObject<SlackResponse>(await request.Content.ReadAsStringAsync());
			}
		}

		public async Task<SlackResponse> PostEphemeralAsync(SlackMessage message)
		{
			using (var client = new HttpClient())
			{
				var request = await client.PostAsync($"{_webhookUrl}/chat.postEphemeral", new StringContent(JsonConvert.SerializeObject(message)));
				return JsonConvert.DeserializeObject<SlackResponse>(await request.Content.ReadAsStringAsync());
			}
		}
	}
}
