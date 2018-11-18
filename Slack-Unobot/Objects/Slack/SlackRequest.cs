using Newtonsoft.Json;

namespace SlackUnobot.Objects.Slack
{
	public class SlackRequest
	{
		[JsonProperty("response_url")]
		public string ResponseUrl { get; set; }

		[JsonProperty("trigger_id")]
		public string TriggerId { get; set; }
		public string Token { get; set; }
		[JsonProperty("team_id")]
		public string TeamId { get; set; }

		[JsonProperty("team_domain")]
		public string TeamDomain { get; set; }

		[JsonProperty("channel_id")]
		public string ChannelId { get; set; }

		[JsonProperty("channel_name")]
		public string ChannelName { get; set; }

		[JsonProperty("user_id")]
		public string UserId { get; set; }

		[JsonProperty("user_name")]

		public string UserName { get; set; }

		public string Command { get; set; }

		public string Text { get; set; }
	}
}